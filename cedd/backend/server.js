const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');
const pythonClient = require('./pythonClient');

const app = express();
const PORT = process.env.PORT || 3000;

// Middleware
app.use(cors());
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));

// In-memory data storage
const componentStore = new Map(); // Map<componentName, componentData>
const commandQueue = new Map(); // Map<commandId, command>
let lastUpdateTime = null;

// AI/Telemetry data storage
const telemetryBuffer = new Map(); // Map<componentName, Array<{timestamp, value, status}>>
const TELEMETRY_MAX_POINTS = 1000; // Keep last 1000 data points per component
const componentHealthStore = new Map(); // Map<componentName, { lastSeen, status, metadata }>
const offlineAlertCache = { alerts: [], updatedAt: null, error: null };
const parameterRiskCache = { warnings: [], updatedAt: null, error: null };

const AI_ENABLED = process.env.AI_ENABLED !== 'false';
const AI_ANOMALY_INTERVAL_MS = parseInt(process.env.AI_ANOMALY_INTERVAL_MS || '5000', 10);
const AI_MAINTENANCE_INTERVAL_MS = parseInt(process.env.AI_MAINTENANCE_INTERVAL_MS || '3600000', 10);
const AI_OPTIMIZATION_INTERVAL_MS = parseInt(process.env.AI_OPTIMIZATION_INTERVAL_MS || '300000', 10);
const AI_PARAMETER_AUDIT_INTERVAL_MS = parseInt(process.env.AI_PARAMETER_AUDIT_INTERVAL_MS || '15000', 10);
const AI_OFFLINE_INTERVAL_MS = parseInt(process.env.AI_OFFLINE_INTERVAL_MS || '4000', 10);
const OFFLINE_HEARTBEAT_TIMEOUT_MS = parseInt(process.env.AI_OFFLINE_HEARTBEAT_TIMEOUT_MS || '4000', 10);
const OFFLINE_ALERT_THRESHOLD_MS = parseInt(process.env.AI_OFFLINE_ALERT_THRESHOLD_MS || '8000', 10);
const OFFLINE_CLEAR_THRESHOLD_MS = parseInt(process.env.AI_OFFLINE_CLEAR_THRESHOLD_MS || '1500', 10);
const PARAMETER_TOLERANCE = parseFloat(process.env.AI_PARAMETER_TOLERANCE || '0.15');
const AI_AUTO_EMERGENCY = process.env.AI_AUTO_EMERGENCY === 'true';

const aiCache = {
    anomalies: { data: [], updatedAt: null, error: null },
    maintenance: { data: [], updatedAt: null, error: null },
    optimization: { data: [], updatedAt: null, error: null, dismissed: [] },
    status: { healthy: false, lastCheck: null, lastError: null }
};
aiCache.status.pythonService = pythonClient.health;
const recentRangeAlerts = [];
const manualOfflineOverrides = new Map(); // Map<componentName, { reason, since, source }>
const MANUAL_OFFLINE_PARAMETERS = new Set((process.env.AI_MANUAL_OFFLINE_PARAMS || 'Enabled').split(',').map(p => p.trim()).filter(Boolean));

// Emergency stop state with control mode
let emergencyStopState = {
    active: false,
    reason: null,
    category: null,
    timestamp: null,
    resumedAt: null,
    controlMode: 'Manual',  // 'Manual', 'EmergencyStopped', 'PlcRecovery'
    plcRecoveryEndTime: null  // When PLC recovery mode ends
};

const PLC_RECOVERY_DURATION_MS = 30000; // 30 seconds in milliseconds

const COMPONENT_CRITICALITY = {
    Drive: 'high',
    Axis: 'high',
    Source: 'medium',
    Sensor: 'medium',
    Grip: 'medium',
    Sink: 'low',
    Lamp: 'low'
};

const PARAMETER_PROFILES = {
    Drive: {
        critical: true,
        valueField: {
            parameter: 'TargetSpeed',
            min: 0,
            max: 500,
            default: 100,
            unit: 'mm/s',
            tolerance: 0.2
        },
        controls: {
            TargetSpeed: { min: 0, max: 500, default: 100, unit: 'mm/s', tolerance: 0.2 },
            SpeedOverride: { min: 0.2, max: 2.5, default: 1, unit: 'x', tolerance: 0.25 },
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    },
    Axis: {
        critical: true,
        valueField: {
            parameter: 'TargetPosition',
            min: -1000,
            max: 1000,
            default: 0,
            unit: 'mm',
            tolerance: 0.1
        },
        controls: {
            TargetPosition: { min: -1000, max: 1000, default: 0, unit: 'mm', tolerance: 0.1 },
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    },
    Sensor: {
        critical: 'medium',
        controls: {
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    },
    Source: {
        critical: 'medium',
        controls: {
            AutomaticGeneration: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 },
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    },
    Grip: {
        critical: 'medium',
        controls: {
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    },
    Lamp: {
        critical: 'low',
        controls: {
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    },
    Sink: {
        critical: 'low',
        controls: {
            Enabled: { min: 0, max: 1, default: 1, unit: 'state', tolerance: 0 }
        }
    }
};

function setManualOfflineOverride(componentName, isOffline, reason = 'Manual override', source = 'command') {
    if (!componentName) return;
    if (isOffline) {
        manualOfflineOverrides.set(componentName, {
            reason,
            since: new Date().toISOString(),
            source
        });
    } else {
        manualOfflineOverrides.delete(componentName);
    }
    const health = componentHealthStore.get(componentName);
    if (health) {
        const metadata = { ...(health.metadata || {}) };
        if (isOffline) {
            metadata.manualOffline = true;
            metadata.manualOfflineReason = reason;
            metadata.manualOfflineSource = source;
            metadata.manualOfflineSince = metadata.manualOfflineSince || new Date().toISOString();
        } else {
            delete metadata.manualOffline;
            delete metadata.manualOfflineReason;
            delete metadata.manualOfflineSource;
            delete metadata.manualOfflineSince;
        }
        componentHealthStore.set(componentName, {
            ...health,
            metadata
        });
    }
    const storedComponent = componentStore.get(componentName);
    if (storedComponent) {
        if (isOffline) {
            applyManualOverrideMetadata(componentName, storedComponent);
        } else if (storedComponent.metadata) {
            const prevStatus = storedComponent.metadata.manualOfflinePrevStatus;
            delete storedComponent.metadata.manualOffline;
            delete storedComponent.metadata.manualOfflineReason;
            delete storedComponent.metadata.manualOfflineSource;
            delete storedComponent.metadata.manualOfflineSince;
            delete storedComponent.metadata.manualOfflinePrevStatus;
            if (prevStatus) {
                storedComponent.status = prevStatus;
            }
        }
    }
}

function applyManualOverrideMetadata(componentName, component) {
    if (!component || !componentName) return component;
    const override = manualOfflineOverrides.get(componentName);
    if (!override) return component;
    component.metadata = component.metadata || {};
    if (!component.metadata.manualOfflinePrevStatus && component.status) {
        component.metadata.manualOfflinePrevStatus = component.status;
    }
    component.metadata.manualOffline = true;
    component.metadata.manualOfflineReason = override.reason;
    component.metadata.manualOfflineSince = override.since;
    component.metadata.manualOfflineSource = override.source;
    component.status = component.status || 'Manual Offline';
    return component;
}

// HMI Panel state
let hmiState = {
    mode: 'auto', // 'auto' or 'manual'
    warning: false,
    warningMessage: null
};

// Helper function to convert Color to hex
function colorToHex(color) {
    if (!color) return '#FFFFFF';
    const r = Math.round(color.r * 255);
    const g = Math.round(color.g * 255);
    const b = Math.round(color.b * 255);
    return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
}

function getComponentCriticality(type) {
    return COMPONENT_CRITICALITY[type] || 'low';
}

function computeRecommendedBounds(profile = {}) {
    const tolerance = profile.tolerance !== undefined ? profile.tolerance : PARAMETER_TOLERANCE;
    let recommendedMin = profile.default !== undefined ? profile.default * (1 - tolerance) : profile.min;
    let recommendedMax = profile.default !== undefined ? profile.default * (1 + tolerance) : profile.max;

    if (profile.min !== undefined && (recommendedMin === undefined || recommendedMin < profile.min)) {
        recommendedMin = profile.min;
    }
    if (profile.max !== undefined && (recommendedMax === undefined || recommendedMax > profile.max)) {
        recommendedMax = profile.max;
    }

    return {
        recommendedMin,
        recommendedMax
    };
}
function normalizeProposedValue(value) {
    if (typeof value === 'boolean') {
        return value ? 1 : 0;
    }
    if (typeof value === 'string') {
        const lower = value.toLowerCase();
        if (lower === 'true') return 1;
        if (lower === 'false') return 0;
    }
    const num = Number(value);
    if (Number.isFinite(num)) {
        return num;
    }
    return null;
}

function toBoolean(value) {
    if (typeof value === 'boolean') return value;
    if (typeof value === 'string') {
        const lower = value.toLowerCase();
        if (lower === 'true') return true;
        if (lower === 'false') return false;
    }
    if (typeof value === 'number') {
        return value !== 0;
    }
    return null;
}

function resolveParameterProfile(component, parameter) {
    if (!component) return null;
    const profile = PARAMETER_PROFILES[component.type];
    if (!profile) return null;
    if (parameter === '__value__') {
        return profile.valueField || null;
    }
    if (!profile.controls) return null;
    return profile.controls[parameter] || null;
}

function updateComponentHealth(component) {
    if (!component || !component.name) return;
    componentHealthStore.set(component.name, {
        lastSeen: Date.now(),
        status: component.status,
        active: component.active,
        metadata: component.metadata || {},
        type: component.type,
        category: component.category || 'other'
    });
}

function buildParameterEvaluationPayload(component, parameter, proposedValue, source = 'dashboard') {
    if (!component || !component.name || parameter === undefined || parameter === null) {
        return null;
    }
    const profile = resolveParameterProfile(component, parameter) || {};
    const bounds = computeRecommendedBounds(profile);
    const normalizedValue = normalizeProposedValue(proposedValue);
    if (normalizedValue === null) {
        return null;
    }
    return {
        componentId: component.name,
        componentType: component.type,
        parameter,
        proposedValue: normalizedValue,
        currentValue: component.value,
        defaultValue: profile.default,
        minValue: profile.min,
        maxValue: profile.max,
        recommendedMin: bounds.recommendedMin,
        recommendedMax: bounds.recommendedMax,
        metadata: {
            ...(component.metadata || {}),
            rawCommandValue: proposedValue,
            parameterSource: source,
            parameterProfile: profile,
            isBooleanParameter: typeof proposedValue === 'boolean' || (profile.max === 1 && profile.min === 0)
        },
        context: {
            source,
            telemetryValue: component.value,
            status: component.status,
            category: component.category || 'other'
        }
    };
}

function mergeParameterWarnings(newWarnings = []) {
    if (!Array.isArray(newWarnings) || newWarnings.length === 0) return;
    const existing = parameterRiskCache.warnings || [];
    const warningMap = new Map(existing.map(w => [`${w.componentId}:${w.parameter}`, w]));
    newWarnings.forEach(w => {
        const key = `${w.componentId}:${w.parameter}`;
        warningMap.set(key, w);
    });
    parameterRiskCache.warnings = Array.from(warningMap.values()).slice(-200);
    parameterRiskCache.updatedAt = new Date().toISOString();
    parameterRiskCache.error = null;
}

function queueParameterEvaluation(component, parameter, rawValue, source = 'command') {
    if (!AI_ENABLED || !component) return;
    const evaluation = buildParameterEvaluationPayload(component, parameter, rawValue, source);
    if (!evaluation) return;
    pythonClient.post('/ai/parameter/evaluate', { evaluations: [evaluation] })
        .then(response => {
            if (response?.warnings) {
                mergeParameterWarnings(response.warnings);
            }
        })
        .catch(err => {
            parameterRiskCache.error = err?.message || 'Parameter evaluation error';
        });
}

// Helper function to sanitize component data for API
function sanitizeComponent(comp) {
    return {
        name: comp.name || comp.ComponentName || '',
        type: comp.type || comp.ComponentType || '',
        active: comp.active !== undefined ? comp.active : comp.IsActive,
        status: comp.status || comp.Status || '',
        value: comp.value !== undefined ? comp.value : comp.Value || 0,
        unit: comp.unit || comp.Unit || '',
        color: comp.color || (comp.StatusColor ? colorToHex(comp.StatusColor) : '#FFFFFF'),
        category: comp.category || 'other',
        hasTransportSurface: comp.hasTransportSurface || false,
        parentRobot: comp.parentRobot || null,
        isRobotAxis: comp.isRobotAxis || false,
        isRobotGrip: comp.isRobotGrip || false,
        timestamp: comp.timestamp || new Date().toISOString(),
        metadata: comp.metadata && typeof comp.metadata === 'object'
            ? JSON.parse(JSON.stringify(comp.metadata))
            : {}
    };
}

// Helper function to filter components by category
function filterByCategory(components, category) {
    if (category === 'all') return components;

    switch (category) {
        case 'sensors':
            return components.filter(c => c.type === 'Sensor');
        case 'conveyors':
            return components.filter(c =>
                c.type === 'Drive' &&
                (c.name.toLowerCase().includes('conveyor') ||
                    c.name.toLowerCase().includes('belt') ||
                    c.name.toLowerCase().includes('transport') ||
                    c.hasTransportSurface === true)
            );
        case 'robots':
            return components.filter(c =>
                (c.type === 'Axis' || c.type === 'Grip') &&
                c.parentRobot !== null
            );
        case 'lights':
            return components.filter(c => c.type === 'Lamp');
        case 'sources':
            return components.filter(c => c.type === 'Source');
        case 'axes':
            return components.filter(c => c.type === 'Axis' && c.parentRobot === null);
        case 'grippers':
            return components.filter(c => c.type === 'Grip' && c.parentRobot === null);
        case 'sinks':
            return components.filter(c => c.type === 'Sink');
        default:
            return components;
    }
}

function collectLatestTelemetry(limit = 250) {
    const snapshot = [];
    telemetryBuffer.forEach((buffer, name) => {
        if (!buffer || buffer.length === 0) return;
        const latest = buffer[buffer.length - 1];
        const component = componentStore.get(name);
        snapshot.push({
            name,
            type: component?.type || component?.ComponentType || latest.type || '',
            value: latest.value,
            status: latest.status,
            metadata: component?.metadata || {}
        });
    });
    return snapshot.slice(-limit);
}

function updateAiStatus(healthy, errorMessage = null) {
    aiCache.status = {
        healthy,
        lastCheck: Date.now(),
        lastError: healthy ? null : (errorMessage || 'Unknown error'),
        pythonService: pythonClient.health
    };
}

async function runAnomalyWorker() {
    if (!AI_ENABLED) return;
    const components = collectLatestTelemetry();
    if (!components.length) return;
    try {
        const response = await pythonClient.post('/ai/anomaly/detect', { components });
        aiCache.anomalies = {
            data: response.anomalies || [],
            updatedAt: response.timestamp || new Date().toISOString(),
            error: null
        };
        updateAiStatus(true);
    } catch (error) {
        aiCache.anomalies.error = error.message || 'Anomaly worker error';
        updateAiStatus(false, error.message);
    }
}

async function runMaintenanceWorker() {
    if (!AI_ENABLED) return;
    const components = collectLatestTelemetry();
    if (!components.length) return;
    try {
        const response = await pythonClient.post('/ai/maintenance/predict', { components });
        aiCache.maintenance = {
            data: response.predictions || [],
            updatedAt: response.timestamp || new Date().toISOString(),
            error: null
        };
        updateAiStatus(true);
    } catch (error) {
        aiCache.maintenance.error = error.message || 'Maintenance worker error';
        updateAiStatus(false, error.message);
    }
}

async function runOptimizationWorker() {
    if (!AI_ENABLED) return;
    const components = collectLatestTelemetry();
    if (!components.length) return;
    try {
        const response = await pythonClient.post('/ai/optimize', { components });
        aiCache.optimization = {
            ...aiCache.optimization,
            data: response.suggestions || [],
            updatedAt: response.timestamp || new Date().toISOString(),
            error: null
        };
        updateAiStatus(true);
    } catch (error) {
        aiCache.optimization.error = error.message || 'Optimization worker error';
        updateAiStatus(false, error.message);
    }
}

function buildOfflineEvaluationPayload() {
    const now = Date.now();
    const payload = [];
    componentStore.forEach((component, name) => {
        const health = componentHealthStore.get(name);
        if (!health) return;
        const gap = now - (health.lastSeen || 0);
        const metadata = {
            ...(component.metadata || {}),
            ...(health.metadata || {})
        };
        const override = manualOfflineOverrides.get(name);
        if (override) {
            metadata.manualOffline = true;
            metadata.manualOfflineReason = override.reason;
            metadata.manualOfflineSince = override.since;
            metadata.manualOfflineSource = override.source;
        }
        const manualOffline = metadata.manualOffline === true;
        if (!manualOffline && gap < OFFLINE_HEARTBEAT_TIMEOUT_MS) {
            return;
        }
        payload.push({
            componentId: name,
            type: component.type,
            status: component.status,
            gapMs: gap,
            metadata,
            criticality: getComponentCriticality(component.type),
            manualOffline,
            lastHeartbeat: metadata.lastHeartbeat || component.timestamp,
            thresholdMs: OFFLINE_ALERT_THRESHOLD_MS,
            heartbeatTimeoutMs: OFFLINE_HEARTBEAT_TIMEOUT_MS,
            manualOverride: !!override
        });
    });
    return payload;
}

async function runOfflineWorker() {
    if (!AI_ENABLED) return;
    const payload = buildOfflineEvaluationPayload();
    if (!payload.length) {
        offlineAlertCache.alerts = [];
        offlineAlertCache.updatedAt = new Date().toISOString();
        offlineAlertCache.error = null;
        return;
    }

    try {
        const response = await pythonClient.post('/ai/offline/evaluate', { components: payload });
        offlineAlertCache.alerts = response.alerts || [];
        offlineAlertCache.updatedAt = response.timestamp || new Date().toISOString();
        offlineAlertCache.error = null;
        updateAiStatus(true);
    } catch (error) {
        offlineAlertCache.error = error.message || 'Offline worker error';
        updateAiStatus(false, error.message);
    }
}

function buildParameterAuditPayload() {
    const evaluations = [];
    componentStore.forEach(component => {
        if (typeof component.value !== 'number') return;
        const profile = resolveParameterProfile(component, '__value__');
        if (!profile) return;
        const bounds = computeRecommendedBounds(profile);
        const value = component.value;
        if (bounds.recommendedMin !== undefined && bounds.recommendedMax !== undefined) {
            if (value >= bounds.recommendedMin && value <= bounds.recommendedMax) {
                return;
            }
        }
        evaluations.push({
            componentId: component.name,
            componentType: component.type,
            parameter: profile.parameter || 'Value',
            proposedValue: value,
            currentValue: value,
            defaultValue: profile.default,
            minValue: profile.min,
            maxValue: profile.max,
            recommendedMin: bounds.recommendedMin,
            recommendedMax: bounds.recommendedMax,
            metadata: component.metadata || {},
            context: {
                source: 'telemetry',
                telemetryValue: component.value,
                status: component.status,
                category: component.category || 'other'
            }
        });
    });
    return evaluations;
}

async function runParameterAuditWorker() {
    if (!AI_ENABLED) return;
    const evaluations = buildParameterAuditPayload();
    if (!evaluations.length) {
        parameterRiskCache.warnings = [];
        parameterRiskCache.updatedAt = new Date().toISOString();
        parameterRiskCache.error = null;
        return;
    }

    try {
        const response = await pythonClient.post('/ai/parameter/evaluate', { evaluations });
        mergeParameterWarnings(response.warnings || []);
        parameterRiskCache.updatedAt = response.timestamp || new Date().toISOString();
        parameterRiskCache.error = null;
        updateAiStatus(true);
    } catch (error) {
        parameterRiskCache.error = error.message || 'Parameter audit error';
        updateAiStatus(false, error.message);
    }
}

function initializeAIWorkers() {
    if (!AI_ENABLED) return;
    runAnomalyWorker();
    runMaintenanceWorker();
    runOptimizationWorker();
    runOfflineWorker();
    runParameterAuditWorker();
    setInterval(runAnomalyWorker, AI_ANOMALY_INTERVAL_MS);
    setInterval(runMaintenanceWorker, AI_MAINTENANCE_INTERVAL_MS);
    setInterval(runOptimizationWorker, AI_OPTIMIZATION_INTERVAL_MS);
    setInterval(runOfflineWorker, AI_OFFLINE_INTERVAL_MS);
    setInterval(runParameterAuditWorker, AI_PARAMETER_AUDIT_INTERVAL_MS);
}

async function recordRangeAlert(alert) {
    recentRangeAlerts.push({ ...alert, recordedAt: new Date().toISOString() });
    if (recentRangeAlerts.length > 200) {
        recentRangeAlerts.shift();
    }
    if (!AI_ENABLED) return;
    try {
        await pythonClient.post('/ai/alerts/range', alert);
        updateAiStatus(true);
    } catch (error) {
        updateAiStatus(false, error.message);
    }
}

// Health check endpoint
app.get('/api/health', (req, res) => {
    res.json({
        status: 'healthy',
        uptime: process.uptime(),
        timestamp: new Date().toISOString()
    });
});

// POST /api/components - Bulk update/create components
app.post('/api/components', (req, res) => {
    try {
        const { components } = req.body;

        if (!Array.isArray(components)) {
            return res.status(400).json({ error: 'components must be an array' });
        }

        let updatedCount = 0;
        components.forEach(comp => {
            const sanitized = sanitizeComponent(comp);
            if (sanitized.name) {
                componentStore.set(sanitized.name, sanitized);
                updatedCount++;
            }
        });

        lastUpdateTime = new Date().toISOString();

        res.json({
            success: true,
            count: updatedCount,
            timestamp: lastUpdateTime
        });
    } catch (error) {
        console.error('Error updating components:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// PUT /api/components/:id - Update single component
app.put('/api/components/:id', (req, res) => {
    try {
        const componentId = req.params.id;
        const sanitized = sanitizeComponent(req.body);
        sanitized.name = componentId;

        componentStore.set(componentId, sanitized);
        lastUpdateTime = new Date().toISOString();

        res.json({
            success: true,
            component: sanitized
        });
    } catch (error) {
        console.error('Error updating component:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/components - Get all components
app.get('/api/components', (req, res) => {
    try {
        const { active, type, category } = req.query;
        let components = Array.from(componentStore.values());

        // Filter by category
        if (category) {
            components = filterByCategory(components, category);
        }

        // Filter by active status
        if (active === 'true') {
            components = components.filter(c => c.active === true);
        } else if (active === 'false') {
            components = components.filter(c => c.active === false);
        }

        // Filter by type
        if (type) {
            components = components.filter(c => c.type === type);
        }

        res.json({
            success: true,
            components: components,
            count: components.length,
            category: category || 'all',
            timestamp: lastUpdateTime || new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting components:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/components/:id - Get single component
app.get('/api/components/:id', (req, res) => {
    try {
        const componentId = req.params.id;
        const component = componentStore.get(componentId);

        if (!component) {
            return res.status(404).json({ error: 'Component not found' });
        }

        res.json(component);
    } catch (error) {
        console.error('Error getting component:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/summary - Get summary statistics
app.get('/api/summary', (req, res) => {
    try {
        const components = Array.from(componentStore.values());
        const activeCount = components.filter(c => c.active === true).length;

        // Count by type
        const counts = {};
        components.forEach(comp => {
            const type = comp.type || 'Unknown';
            counts[type] = (counts[type] || 0) + 1;
        });

        const dataAge = lastUpdateTime
            ? Date.now() - new Date(lastUpdateTime).getTime()
            : null;

        res.json({
            total: components.length,
            active: activeCount,
            counts: counts,
            lastUpdate: lastUpdateTime,
            dataAge: dataAge // milliseconds since last update
        });
    } catch (error) {
        console.error('Error getting summary:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/categories - Get category counts
// FIXED: Properly counts categories without double-counting
app.get('/api/categories', (req, res) => {
    try {
        const components = Array.from(componentStore.values());

        // Count each component type first
        const sensors = components.filter(c => c.type === 'Sensor');
        const drives = components.filter(c => c.type === 'Drive');
        const axes = components.filter(c => c.type === 'Axis');
        const grips = components.filter(c => c.type === 'Grip');
        const lamps = components.filter(c => c.type === 'Lamp');
        const sources = components.filter(c => c.type === 'Source');
        const sinks = components.filter(c => c.type === 'Sink');

        // Calculate conveyors (subset of drives - NOT counted separately in total)
        const conveyors = drives.filter(c =>
            c.name.toLowerCase().includes('conveyor') ||
            c.name.toLowerCase().includes('belt') ||
            c.name.toLowerCase().includes('transport') ||
            c.hasTransportSurface === true
        );

        // Calculate robots (axes/grips with parentRobot - NOT counted separately)
        const robotAxes = axes.filter(c => c.parentRobot !== null && c.parentRobot !== '');
        const robotGrips = grips.filter(c => c.parentRobot !== null && c.parentRobot !== '');
        const robots = robotAxes.length + robotGrips.length;

        // Calculate standalone axes (not part of robots)
        const standaloneAxes = axes.filter(c => !c.parentRobot || c.parentRobot === null || c.parentRobot === '');

        // Calculate standalone grippers (not part of robots)
        const standaloneGrippers = grips.filter(c => !c.parentRobot || c.parentRobot === null || c.parentRobot === '');

        const allCategories = {
            all: components.length, // Total unique components
            sensors: sensors.length,
            conveyors: conveyors.length, // Subset of drives
            robots: robots, // Combined count of robot axes and grips
            lights: lamps.length,
            sources: sources.length,
            axes: standaloneAxes.length, // Axes not part of robots
            grippers: standaloneGrippers.length, // Grips not part of robots
            sinks: sinks.length
        };

        // Filter out categories with 0 components (except 'all' which always shows)
        const categories = {};
        Object.entries(allCategories).forEach(([key, count]) => {
            if (key === 'all' || count > 0) {
                categories[key] = count;
            }
        });

        res.json({
            success: true,
            categories: categories,
            hiddenCategories: Object.keys(allCategories).filter(k => k !== 'all' && allCategories[k] === 0),
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting categories:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/categories/:category - Get components in category
app.get('/api/categories/:category', (req, res) => {
    try {
        const category = req.params.category;
        let components = Array.from(componentStore.values());

        components = filterByCategory(components, category);

        res.json({
            success: true,
            components: components,
            count: components.length,
            category: category,
            timestamp: lastUpdateTime || new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting category components:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/categories/:category/summary - Get category summary
app.get('/api/categories/:category/summary', (req, res) => {
    try {
        const category = req.params.category;
        let components = Array.from(componentStore.values());

        components = filterByCategory(components, category);
        const activeCount = components.filter(c => c.active === true).length;

        res.json({
            success: true,
            total: components.length,
            active: activeCount,
            inactive: components.length - activeCount,
            category: category,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting category summary:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/components/:id/parameters - Get editable parameters
app.get('/api/components/:id/parameters', (req, res) => {
    try {
        const componentId = req.params.id;
        const component = componentStore.get(componentId);

        if (!component) {
            return res.status(404).json({ error: 'Component not found' });
        }

        // Return parameter definitions based on component type
        // This is a simplified version - in production, you'd load from ParameterDefinitionProvider
        const parameterDefinitions = {
            'Drive': {
                'TargetSpeed': { min: 0, max: 500, unit: 'mm/s', type: 'float', editable: true },
                'JogForward': { type: 'boolean', editable: true },
                'JogBackward': { type: 'boolean', editable: true },
                'TargetPosition': { min: -1000, max: 1000, unit: 'mm', type: 'float', editable: true },
                'TargetStartMove': { type: 'boolean', editable: true },
                'SpeedOverride': { min: 0.1, max: 5.0, unit: 'multiplier', type: 'float', editable: true },
                'Enabled': { type: 'boolean', editable: true }
            },
            'Sensor': {
                'DisplayStatus': { type: 'boolean', editable: true },
                'LimitSensorToTag': { type: 'string', editable: true },
                'Enabled': { type: 'boolean', editable: true }
            },
            'Lamp': {
                'LampOn': { type: 'boolean', editable: true },
                'Flashing': { type: 'boolean', editable: true },
                'Enabled': { type: 'boolean', editable: true }
            },
            'Source': {
                'Enabled': { type: 'boolean', editable: true },
                'GenerateMU': { type: 'boolean', editable: true },
                'DeleteAllMU': { type: 'boolean', editable: true },
                'AutomaticGeneration': { type: 'boolean', editable: true },
                'SourceGenerate': { type: 'boolean', editable: true },
                'SourceGenerateOnDistance': { type: 'boolean', editable: true }
            },
            'Grip': {
                'PickObjects': { type: 'boolean', editable: true },
                'PlaceObjects': { type: 'boolean', editable: true },
                'Enabled': { type: 'boolean', editable: true }
            },
            'Axis': {
                'TargetPosition': { min: -1000, max: 1000, unit: 'mm', type: 'float', editable: true },
                'TargetStartMove': { type: 'boolean', editable: true },
                'Enabled': { type: 'boolean', editable: true }
            },
            'Sink': {
                'Enabled': { type: 'boolean', editable: true }
            }
        };

        const params = parameterDefinitions[component.type] || {};

        res.json({
            success: true,
            parameters: params
        });
    } catch (error) {
        console.error('Error getting parameters:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/components/:id/ranges - Get parameter ranges for component
app.get('/api/components/:id/ranges', (req, res) => {
    try {
        const componentId = req.params.id;
        const component = componentStore.get(componentId);

        if (!component) {
            return res.status(404).json({ error: 'Component not found' });
        }

        // Default ranges by component type
        const defaultRanges = {
            'Drive': {
                speed: { min: 0, max: 500, default: 100, unit: 'mm/s' },
                position: { min: -1000, max: 1000, default: 0, unit: 'mm' }
            },
            'Axis': {
                position: { min: -1000, max: 1000, default: 0, unit: 'mm' }
            },
            'Source': {
                interval: { min: 0.1, max: 60, default: 2, unit: 's' }
            }
        };

        // Component-specific overrides based on name (can be extended)
        const componentOverrides = {};
        const lowerName = component.name.toLowerCase();

        // Conveyor-specific speeds
        if (lowerName.includes('conveyor') || lowerName.includes('belt')) {
            componentOverrides.speed = { min: 0, max: 300, default: 150, unit: 'mm/s' };
        }

        // Gantry-specific positions
        if (lowerName.includes('gantry') || lowerName.includes('loader')) {
            componentOverrides.position = { min: 0, max: 2000, default: 0, unit: 'mm' };
        }

        const ranges = {
            ...defaultRanges[component.type],
            ...componentOverrides
        };

        res.json({
            success: true,
            componentId: componentId,
            type: component.type,
            ranges: ranges,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting ranges:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/components/:id/control - Send control command
app.post('/api/components/:id/control', (req, res) => {
    try {
        const componentId = req.params.id;
        const { parameter, value, userId } = req.body;

        // Validate input
        if (!parameter || value === undefined) {
            return res.status(400).json({
                success: false,
                error: {
                    code: 'VALIDATION_ERROR',
                    message: 'Parameter and value are required'
                }
            });
        }

        // Check if component exists
        const component = componentStore.get(componentId);
        if (!component) {
            return res.status(404).json({
                success: false,
                error: {
                    code: 'COMPONENT_NOT_FOUND',
                    message: 'Component not found'
                }
            });
        }

        const boolValue = toBoolean(value);
        if (MANUAL_OFFLINE_PARAMETERS.has(parameter) && boolValue !== null) {
            if (boolValue === false) {
                setManualOfflineOverride(
                    componentId,
                    true,
                    `${parameter} set to OFF by ${userId || 'dashboard_user'}`,
                    'command'
                );
            } else if (boolValue === true) {
                setManualOfflineOverride(componentId, false);
            }
        }

        // Check control mode - only allow commands in Manual mode
        if (emergencyStopState.controlMode !== 'Manual') {
            let message = '';
            let details = {};

            if (emergencyStopState.controlMode === 'EmergencyStopped') {
                message = 'Production is EMERGENCY STOPPED. Cannot execute commands. Resume production first.';
                details = {
                    reason: emergencyStopState.reason,
                    category: emergencyStopState.category,
                    haltedAt: emergencyStopState.timestamp
                };
            } else if (emergencyStopState.controlMode === 'PlcRecovery') {
                // Calculate remaining time
                let remaining = 0;
                if (emergencyStopState.plcRecoveryEndTime) {
                    remaining = Math.max(0, (new Date(emergencyStopState.plcRecoveryEndTime).getTime() - Date.now()) / 1000);
                }
                message = `PLC Recovery in progress (${remaining.toFixed(1)}s remaining). Commands blocked until recovery complete.`;
                details = {
                    plcRecoveryEndTime: emergencyStopState.plcRecoveryEndTime,
                    timeRemaining: remaining
                };
            }

            console.log(`[Command] Blocked command for ${componentId}.${parameter} - ${emergencyStopState.controlMode}`);
            return res.status(409).json({
                success: false,
                error: {
                    code: 'STATE_ERROR',
                    message: message,
                    controlMode: emergencyStopState.controlMode,
                    details: details
                }
            });
        }

        // Create command
        const commandId = `cmd_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const command = {
            commandId: commandId,
            componentId: componentId,
            category: component.category || 'other',
            parameter: parameter,
            value: JSON.stringify(value), // Store as JSON string for Unity parsing
            timestamp: new Date().toISOString(),
            userId: userId || 'anonymous',
            status: 'pending',
            isEmergencyCommand: false
        };

        // Add to command queue
        commandQueue.set(commandId, command);

        queueParameterEvaluation(component, parameter, value, 'command');

        // Return immediately (async processing)
        res.status(202).json({
            success: true,
            commandId: commandId,
            message: 'Command queued',
            timestamp: command.timestamp
        });

        // Note: Unity will poll for commands and process them

    } catch (error) {
        console.error('Error processing command:', error);
        res.status(500).json({
            success: false,
            error: {
                code: 'SERVER_ERROR',
                message: 'Internal server error',
                details: { error: error.message }
            }
        });
    }
});

// GET /api/commands/pending - Get pending commands (for Unity polling)
app.get('/api/commands/pending', (req, res) => {
    try {
        // Get pending commands
        const pendingCommands = Array.from(commandQueue.values())
            .filter(cmd => cmd.status === 'pending')
            .sort((a, b) => {
                // Sort: emergency commands first, then by timestamp
                if (a.isEmergencyCommand && !b.isEmergencyCommand) return -1;
                if (!a.isEmergencyCommand && b.isEmergencyCommand) return 1;
                return new Date(a.timestamp) - new Date(b.timestamp);
            })
            .slice(0, 10); // Limit to 10 commands per poll

        if (pendingCommands.length === 0) {
            return res.status(404).json({ commands: [] });
        }

        // CRITICAL: Mark commands as 'processing' immediately when retrieved
        // This prevents duplicate processing if Unity polls again before completing
        pendingCommands.forEach(cmd => {
            cmd.status = 'processing';
            cmd.processingAt = cmd.processingAt || new Date().toISOString();
        });

        // Log command retrieval
        const emergencyCount = pendingCommands.filter(cmd => cmd.isEmergencyCommand).length;
        if (emergencyCount > 0) {
            console.log(`[Command Queue] Returning ${pendingCommands.length} commands (${emergencyCount} emergency) - marked as processing`);
        }

        res.json({
            success: true,
            commands: pendingCommands
        });
    } catch (error) {
        console.error('Error getting pending commands:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/commands/:id/complete - Mark command as completed
app.post('/api/commands/:id/complete', (req, res) => {
    try {
        const commandId = req.params.id;
        const { state } = req.body;

        if (commandQueue.has(commandId)) {
            const command = commandQueue.get(commandId);
            command.status = 'completed';
            command.completedAt = new Date().toISOString();
            command.result = state;

            // Mark as processing was successful (now we know Unity got it)
            command.processingAt = command.processingAt || new Date().toISOString();

            // Log completion
            if (command.isEmergencyCommand) {
                console.log(`[Command Queue] Emergency command completed: ${commandId}`);
            }

            // Update component state if provided
            if (state && state.name) {
                const component = componentStore.get(state.name);
                if (component) {
                    component.status = state.status || component.status;
                    component.value = state.value !== undefined ? state.value : component.value;
                    component.unit = state.unit || component.unit;
                    lastUpdateTime = new Date().toISOString();
                }
            }

            // For emergency stop, update emergency state
            if (command.isEmergencyCommand && command.parameter === 'EmergencyStop') {
                // Emergency stop was executed - state is already set, just confirm
                console.log(`[Emergency Stop] Confirmed executed by Unity: ${commandId}`);
            }

            // Remove from queue after a delay (keep for audit)
            setTimeout(() => {
                commandQueue.delete(commandId);
            }, 60000); // Keep for 1 minute

            res.json({ success: true, message: 'Command marked as completed' });
        } else {
            res.status(404).json({ success: false, error: 'Command not found' });
        }
    } catch (error) {
        console.error('Error completing command:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/commands/:id/error - Mark command as failed
app.post('/api/commands/:id/error', (req, res) => {
    try {
        const commandId = req.params.id;
        const { error } = req.body;

        if (commandQueue.has(commandId)) {
            const command = commandQueue.get(commandId);
            command.status = 'failed';
            command.failedAt = new Date().toISOString();
            command.error = error;

            // Remove from queue after a delay
            setTimeout(() => {
                commandQueue.delete(commandId);
            }, 60000);

            res.json({ success: true, message: 'Command marked as failed' });
        } else {
            res.status(404).json({ success: false, error: 'Command not found' });
        }
    } catch (error) {
        console.error('Error marking command as failed:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/emergency/stop - Emergency stop all production
app.post('/api/emergency/stop', (req, res) => {
    try {
        const { reason, category } = req.body;
        const timestamp = new Date().toISOString();

        emergencyStopState = {
            active: true,
            reason: reason || 'Manual emergency stop',
            category: category || null,
            timestamp: timestamp,
            resumedAt: null,
            controlMode: 'EmergencyStopped',
            plcRecoveryEndTime: null
        };

        // Create emergency stop command for Unity (high priority)
        const commandId = `emergency_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const command = {
            commandId: commandId,
            componentId: 'all',
            category: category || 'all',
            parameter: 'EmergencyStop',
            value: JSON.stringify({
                reason: emergencyStopState.reason,
                category: category || null
            }),
            timestamp: timestamp,
            userId: req.body.userId || 'system',
            status: 'pending',
            isEmergencyCommand: true,
            priority: 'high' // Mark as high priority
        };

        commandQueue.set(commandId, command);

        console.log(`[Emergency Stop] Command created: ${commandId} for category: ${category || 'all'}`);
        console.log(`[Control Mode] Set to: EmergencyStopped`);

        res.json({
            success: true,
            message: 'Emergency stop activated',
            stoppedComponents: 'all',
            timestamp: timestamp,
            controlMode: 'EmergencyStopped'
        });
    } catch (error) {
        console.error('Error activating emergency stop:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/emergency/resume - Resume production
app.post('/api/emergency/resume', (req, res) => {
    try {
        if (!emergencyStopState.active && emergencyStopState.controlMode !== 'EmergencyStopped') {
            return res.status(400).json({
                success: false,
                error: 'Production is not halted'
            });
        }

        // Validate all components are in safe state (simplified - in production, do actual validation)
        const components = Array.from(componentStore.values());
        const invalidComponents = []; // Would check parameters here

        if (invalidComponents.length > 0) {
            return res.status(409).json({
                success: false,
                error: 'Cannot resume: Some parameters are out of safe range',
                invalidComponents: invalidComponents
            });
        }

        const resumedAt = new Date().toISOString();
        const plcRecoveryEndTime = new Date(Date.now() + PLC_RECOVERY_DURATION_MS).toISOString();

        emergencyStopState = {
            active: false,
            reason: null,
            category: null,
            timestamp: null,
            resumedAt: resumedAt,
            controlMode: 'PlcRecovery',
            plcRecoveryEndTime: plcRecoveryEndTime
        };

        // Schedule transition to Manual mode after PLC recovery period
        setTimeout(() => {
            if (emergencyStopState.controlMode === 'PlcRecovery') {
                emergencyStopState.controlMode = 'Manual';
                emergencyStopState.plcRecoveryEndTime = null;
                console.log(`[Control Mode] PLC Recovery complete -> Manual mode`);
            }
        }, PLC_RECOVERY_DURATION_MS);

        // Create resume command for Unity
        const commandId = `resume_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const command = {
            commandId: commandId,
            componentId: 'all',
            category: 'all',
            parameter: 'ResumeProduction',
            value: JSON.stringify({ acknowledgedBy: req.body.acknowledgedBy || 'dashboard_user' }),
            timestamp: resumedAt,
            userId: req.body.acknowledgedBy || 'dashboard_user',
            status: 'pending',
            isEmergencyCommand: false
        };

        commandQueue.set(commandId, command);
        console.log(`[Resume] Command created: ${commandId}`);
        console.log(`[Control Mode] Set to: PlcRecovery (${PLC_RECOVERY_DURATION_MS / 1000}s)`);

        res.json({
            success: true,
            message: 'Production resumed - PLC recovery in progress',
            timestamp: resumedAt,
            controlMode: 'PlcRecovery',
            plcRecoveryEndTime: plcRecoveryEndTime,
            plcRecoveryDuration: PLC_RECOVERY_DURATION_MS / 1000
        });
    } catch (error) {
        console.error('Error resuming production:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/emergency/status - Get emergency stop status
app.get('/api/emergency/status', (req, res) => {
    try {
        // Calculate remaining PLC recovery time
        let plcRecoveryTimeRemaining = 0;
        if (emergencyStopState.controlMode === 'PlcRecovery' && emergencyStopState.plcRecoveryEndTime) {
            const endTime = new Date(emergencyStopState.plcRecoveryEndTime).getTime();
            plcRecoveryTimeRemaining = Math.max(0, (endTime - Date.now()) / 1000);

            // Auto-transition to Manual if recovery time elapsed
            if (plcRecoveryTimeRemaining <= 0) {
                emergencyStopState.controlMode = 'Manual';
                emergencyStopState.plcRecoveryEndTime = null;
            }
        }

        res.json({
            success: true,
            active: emergencyStopState.active,
            reason: emergencyStopState.reason,
            category: emergencyStopState.category,
            timestamp: emergencyStopState.timestamp,
            resumedAt: emergencyStopState.resumedAt,
            controlMode: emergencyStopState.controlMode,
            plcRecoveryEndTime: emergencyStopState.plcRecoveryEndTime,
            plcRecoveryTimeRemaining: plcRecoveryTimeRemaining,
            commandsAllowed: emergencyStopState.controlMode === 'Manual'
        });
    } catch (error) {
        console.error('Error getting emergency status:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// ============ HMI Panel Endpoints ============

// GET /api/hmi/status - Get HMI panel status
app.get('/api/hmi/status', (req, res) => {
    try {
        // Calculate remaining PLC recovery time
        let plcRecoveryTimeRemaining = 0;
        if (emergencyStopState.controlMode === 'PlcRecovery' && emergencyStopState.plcRecoveryEndTime) {
            plcRecoveryTimeRemaining = Math.max(0, (new Date(emergencyStopState.plcRecoveryEndTime).getTime() - Date.now()) / 1000);
        }

        res.json({
            success: true,
            mode: hmiState.mode,
            warning: hmiState.warning,
            warningMessage: hmiState.warningMessage,
            emergencyActive: emergencyStopState.active,
            controlMode: emergencyStopState.controlMode,
            plcRecoveryTimeRemaining: plcRecoveryTimeRemaining,
            commandsAllowed: emergencyStopState.controlMode === 'Manual',
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting HMI status:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/hmi/mode - Set HMI mode (auto/manual)
app.post('/api/hmi/mode', (req, res) => {
    try {
        const { mode } = req.body;

        if (mode !== 'auto' && mode !== 'manual') {
            return res.status(400).json({
                success: false,
                error: 'Invalid mode. Must be "auto" or "manual"'
            });
        }

        hmiState.mode = mode;
        console.log(`[HMI] Mode changed to: ${mode}`);

        // Create command for Unity to handle mode change
        const commandId = `hmi_mode_${Date.now()}`;
        const command = {
            commandId: commandId,
            componentId: 'hmi',
            category: 'system',
            parameter: 'Mode',
            value: JSON.stringify(mode),
            timestamp: new Date().toISOString(),
            userId: 'hmi_panel',
            status: 'pending',
            isEmergencyCommand: false
        };
        commandQueue.set(commandId, command);

        res.json({
            success: true,
            mode: hmiState.mode,
            message: `Mode set to ${mode}`,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error setting HMI mode:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/hmi/warning - Set warning state
app.post('/api/hmi/warning', (req, res) => {
    try {
        const { active, message } = req.body;

        hmiState.warning = active === true;
        hmiState.warningMessage = message || null;

        res.json({
            success: true,
            warning: hmiState.warning,
            message: hmiState.warningMessage,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error setting warning:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// ============ AI-Ready Endpoints ============

// Helper function to store telemetry data
function storeTelemetryPoint(componentName, data) {
    if (!telemetryBuffer.has(componentName)) {
        telemetryBuffer.set(componentName, []);
    }

    const buffer = telemetryBuffer.get(componentName);
    buffer.push({
        timestamp: new Date().toISOString(),
        value: data.value,
        status: data.status,
        active: data.active
    });

    // Keep only the last TELEMETRY_MAX_POINTS
    if (buffer.length > TELEMETRY_MAX_POINTS) {
        buffer.shift();
    }
}

// Auto-collect telemetry when components are updated
const originalSet = componentStore.set.bind(componentStore);
componentStore.set = function (key, value) {
    storeTelemetryPoint(key, value);
    applyManualOverrideMetadata(key, value);
    updateComponentHealth(value);
    return originalSet(key, value);
};

// GET /api/telemetry - Get telemetry data for AI/ML
app.get('/api/telemetry', (req, res) => {
    try {
        const { component, limit, since } = req.query;
        let result = {};

        if (component) {
            // Get telemetry for specific component
            const buffer = telemetryBuffer.get(component) || [];
            let data = [...buffer];

            // Filter by time if 'since' is provided
            if (since) {
                const sinceTime = new Date(since).getTime();
                data = data.filter(d => new Date(d.timestamp).getTime() > sinceTime);
            }

            // Limit results
            if (limit) {
                data = data.slice(-parseInt(limit));
            }

            result[component] = data;
        } else {
            // Get telemetry for all components
            telemetryBuffer.forEach((buffer, name) => {
                let data = [...buffer];

                if (since) {
                    const sinceTime = new Date(since).getTime();
                    data = data.filter(d => new Date(d.timestamp).getTime() > sinceTime);
                }

                if (limit) {
                    data = data.slice(-parseInt(limit));
                }

                result[name] = data;
            });
        }

        res.json({
            success: true,
            telemetry: result,
            totalComponents: telemetryBuffer.size,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting telemetry:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/telemetry/stats - Get telemetry statistics for AI/ML
app.get('/api/telemetry/stats', (req, res) => {
    try {
        const stats = {};

        telemetryBuffer.forEach((buffer, componentName) => {
            if (buffer.length === 0) return;

            const values = buffer.map(d => d.value).filter(v => typeof v === 'number');
            const activeCount = buffer.filter(d => d.active).length;

            stats[componentName] = {
                dataPoints: buffer.length,
                firstTimestamp: buffer[0]?.timestamp,
                lastTimestamp: buffer[buffer.length - 1]?.timestamp,
                activePercentage: (activeCount / buffer.length * 100).toFixed(2),
                valueStats: values.length > 0 ? {
                    min: Math.min(...values),
                    max: Math.max(...values),
                    avg: (values.reduce((a, b) => a + b, 0) / values.length).toFixed(2),
                    latest: values[values.length - 1]
                } : null
            };
        });

        res.json({
            success: true,
            stats: stats,
            totalComponents: telemetryBuffer.size,
            maxPointsPerComponent: TELEMETRY_MAX_POINTS,
            timestamp: new Date().toISOString()
        });
    } catch (error) {
        console.error('Error getting telemetry stats:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/ai/command - AI command interface for batch commands
app.post('/api/ai/command', (req, res) => {
    try {
        const { commands, priority, aiModelId, confidence } = req.body;

        if (!Array.isArray(commands) || commands.length === 0) {
            return res.status(400).json({
                success: false,
                error: 'Commands must be a non-empty array'
            });
        }

        // Validate AI command structure
        const validatedCommands = [];
        const errors = [];

        commands.forEach((cmd, index) => {
            if (!cmd.componentId || !cmd.parameter) {
                errors.push({ index, error: 'Missing componentId or parameter' });
                return;
            }

            // Check if component exists
            if (!componentStore.has(cmd.componentId)) {
                errors.push({ index, error: `Component ${cmd.componentId} not found` });
                return;
            }

            // Check emergency stop
            if (emergencyStopState.active) {
                errors.push({ index, error: 'Production is halted' });
                return;
            }

            // Create command with AI metadata
            const commandId = `ai_${Date.now()}_${index}_${Math.random().toString(36).substr(2, 9)}`;
            const command = {
                commandId: commandId,
                componentId: cmd.componentId,
                category: componentStore.get(cmd.componentId)?.category || 'other',
                parameter: cmd.parameter,
                value: JSON.stringify(cmd.value),
                timestamp: new Date().toISOString(),
                userId: `ai_${aiModelId || 'unknown'}`,
                status: 'pending',
                isEmergencyCommand: false,
                aiMetadata: {
                    modelId: aiModelId || 'unknown',
                    confidence: confidence || null,
                    batchIndex: index,
                    priority: priority || 'normal'
                }
            };

            commandQueue.set(commandId, command);
            validatedCommands.push({ index, commandId });
        });

        res.status(202).json({
            success: errors.length === 0,
            accepted: validatedCommands.length,
            rejected: errors.length,
            commands: validatedCommands,
            errors: errors.length > 0 ? errors : undefined,
            timestamp: new Date().toISOString()
        });

    } catch (error) {
        console.error('Error processing AI commands:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

app.post('/api/ai/parameter/evaluate', async (req, res) => {
    try {
        if (!AI_ENABLED) {
            return res.status(503).json({ success: false, error: 'AI services disabled' });
        }

        const payloads = [];
        let incoming = [];

        if (Array.isArray(req.body?.evaluations)) {
            incoming = req.body.evaluations;
        } else if (req.body && typeof req.body === 'object') {
            incoming = [req.body];
        }

        incoming.forEach(entry => {
            if (!entry) return;
            const componentId = entry.componentId;
            const parameter = entry.parameter;
            const proposedValue = entry.value !== undefined ? entry.value : entry.proposedValue;
            if (!componentId || parameter === undefined || proposedValue === undefined) {
                return;
            }
            const component = componentStore.get(componentId) || {
                name: componentId,
                type: entry.componentType || 'Unknown',
                value: entry.currentValue,
                status: entry.status || null,
                metadata: entry.metadata || {},
                category: entry.category || 'other'
            };
            const evaluation = buildParameterEvaluationPayload(component, parameter, proposedValue, entry.source || 'api');
            if (!evaluation) return;

            if (entry.defaultValue !== undefined && evaluation.defaultValue === undefined) {
                evaluation.defaultValue = entry.defaultValue;
            }
            if (entry.minValue !== undefined && evaluation.minValue === undefined) {
                evaluation.minValue = entry.minValue;
            }
            if (entry.maxValue !== undefined && evaluation.maxValue === undefined) {
                evaluation.maxValue = entry.maxValue;
            }
            if (entry.recommendedMin !== undefined) {
                evaluation.recommendedMin = entry.recommendedMin;
            }
            if (entry.recommendedMax !== undefined) {
                evaluation.recommendedMax = entry.recommendedMax;
            }

            evaluation.context = {
                ...(evaluation.context || {}),
                ...entry.context
            };

            payloads.push(evaluation);
        });

        if (!payloads.length) {
            return res.status(400).json({ success: false, error: 'No valid parameter evaluations provided' });
        }

        const response = await pythonClient.post('/ai/parameter/evaluate', { evaluations: payloads });
        mergeParameterWarnings(response.warnings || []);
        parameterRiskCache.updatedAt = response.timestamp || new Date().toISOString();
        parameterRiskCache.error = null;
        res.json({
            success: true,
            warnings: response.warnings || [],
            timestamp: response.timestamp || new Date().toISOString()
        });
    } catch (error) {
        console.error('Error evaluating parameter risk:', error);
        parameterRiskCache.error = error.message || 'Parameter evaluation error';
        res.status(500).json({ success: false, error: error.message || 'Parameter evaluation failed' });
    }
});

// GET /api/ai/command/status - Get status of AI-generated commands
app.get('/api/ai/command/status', (req, res) => {
    try {
        const { commandId, modelId } = req.query;

        if (commandId) {
            // Get specific command status
            const command = commandQueue.get(commandId);
            if (!command) {
                return res.status(404).json({ success: false, error: 'Command not found' });
            }

            res.json({
                success: true,
                command: {
                    commandId: command.commandId,
                    status: command.status,
                    componentId: command.componentId,
                    parameter: command.parameter,
                    timestamp: command.timestamp,
                    completedAt: command.completedAt,
                    error: command.error,
                    aiMetadata: command.aiMetadata
                }
            });
        } else {
            // Get all AI command statuses
            let aiCommands = Array.from(commandQueue.values())
                .filter(cmd => cmd.userId?.startsWith('ai_'))
                .map(cmd => ({
                    commandId: cmd.commandId,
                    status: cmd.status,
                    componentId: cmd.componentId,
                    parameter: cmd.parameter,
                    timestamp: cmd.timestamp,
                    aiMetadata: cmd.aiMetadata
                }));

            // Filter by model if specified
            if (modelId) {
                aiCommands = aiCommands.filter(cmd => cmd.aiMetadata?.modelId === modelId);
            }

            res.json({
                success: true,
                commands: aiCommands,
                total: aiCommands.length,
                timestamp: new Date().toISOString()
            });
        }
    } catch (error) {
        console.error('Error getting AI command status:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/ai/predict - Ad-hoc anomaly detection via Python service
app.post('/api/ai/predict', async (req, res) => {
    try {
        if (!AI_ENABLED) {
            return res.status(503).json({ success: false, error: 'AI services disabled' });
        }

        const components = req.body?.components || collectLatestTelemetry();
        if (!components.length) {
            return res.status(400).json({ success: false, error: 'No telemetry available for prediction' });
        }

        const response = await pythonClient.post('/ai/anomaly/detect', { components });
        updateAiStatus(true);
        res.json(response);
    } catch (error) {
        console.error('Error in AI predict:', error.message);
        updateAiStatus(false, error.message);
        res.status(500).json({ success: false, error: error.message || 'AI prediction failed' });
    }
});

// GET /api/ai/models - List available AI models via Python service
app.get('/api/ai/models', async (req, res) => {
    try {
        if (!AI_ENABLED) {
            return res.json({
                success: false,
                message: 'AI services disabled',
                models: [],
            timestamp: new Date().toISOString()
        });
        }
        const response = await pythonClient.get('/ai/models/status');
        updateAiStatus(true);
        res.json(response);
    } catch (error) {
        console.error('Error listing AI models:', error.message);
        updateAiStatus(false, error.message);
        res.status(500).json({ success: false, error: error.message || 'Failed to load AI models' });
    }
});

// GET /api/ai/status - Get AI system status
app.get('/api/ai/status', (req, res) => {
    try {
        const aiCommandCount = Array.from(commandQueue.values())
            .filter(cmd => cmd.userId?.startsWith('ai_')).length;
        const latestAlert = recentRangeAlerts[recentRangeAlerts.length - 1] || null;

        res.json({
            success: true,
            enabled: AI_ENABLED,
            timestamp: new Date().toISOString(),
            status: aiCache.status,
            pythonService: pythonClient.health,
            telemetry: {
                components: telemetryBuffer.size,
                maxPoints: TELEMETRY_MAX_POINTS
            },
            queues: {
                aiCommandsInQueue: aiCommandCount
            },
            caches: {
                anomalies: {
                    updatedAt: aiCache.anomalies.updatedAt,
                    error: aiCache.anomalies.error,
                    count: aiCache.anomalies.data.length
                },
                maintenance: {
                    updatedAt: aiCache.maintenance.updatedAt,
                    error: aiCache.maintenance.error,
                    count: aiCache.maintenance.data.length
                },
                optimization: {
                    updatedAt: aiCache.optimization.updatedAt,
                    error: aiCache.optimization.error,
                    count: aiCache.optimization.data?.length || 0
                }
            },
            rangeAlerts: {
                count: recentRangeAlerts.length,
                latest: latestAlert
            },
            endpoints: {
                anomalies: '/api/ai/anomalies',
                maintenance: '/api/ai/maintenance',
                optimization: '/api/ai/optimize',
                predict: '/api/ai/predict',
                models: '/api/ai/models'
            }
        });
    } catch (error) {
        console.error('Error getting AI status:', error);
        res.status(500).json({ success: false, error: error.message });
    }
});

app.get('/api/ai/alerts', (req, res) => {
    res.json({
        success: true,
        timestamp: new Date().toISOString(),
        offline: {
            alerts: offlineAlertCache.alerts,
            updatedAt: offlineAlertCache.updatedAt,
            error: offlineAlertCache.error
        },
        parameter: {
            warnings: parameterRiskCache.warnings,
            updatedAt: parameterRiskCache.updatedAt,
            error: parameterRiskCache.error
        },
        range: {
            recent: recentRangeAlerts.slice(-50)
        }
    });
});

app.get('/api/ai/anomalies', (req, res) => {
    res.json({
        success: true,
        enabled: AI_ENABLED,
        updatedAt: aiCache.anomalies.updatedAt,
        error: aiCache.anomalies.error,
        data: aiCache.anomalies.data
    });
});

app.get('/api/ai/maintenance', (req, res) => {
    res.json({
        success: true,
        enabled: AI_ENABLED,
        updatedAt: aiCache.maintenance.updatedAt,
        error: aiCache.maintenance.error,
        data: aiCache.maintenance.data
    });
});

app.get('/api/ai/optimize', (req, res) => {
    res.json({
        success: true,
        enabled: AI_ENABLED,
        updatedAt: aiCache.optimization.updatedAt,
        error: aiCache.optimization.error,
        data: aiCache.optimization.data || [],
        dismissed: aiCache.optimization.dismissed,
        lastApplied: aiCache.optimization.lastApplied || null
    });
});

app.post('/api/ai/optimize/dismiss', (req, res) => {
    const entry = {
        componentId: req.body?.componentId,
        parameter: req.body?.parameter,
        dismissedAt: new Date().toISOString()
    };
    aiCache.optimization.dismissed.unshift(entry);
    aiCache.optimization.dismissed = aiCache.optimization.dismissed.slice(0, 50);
    res.json({ success: true, entry });
});

app.post('/api/ai/optimize/apply', (req, res) => {
    aiCache.optimization.lastApplied = {
        componentId: req.body?.componentId,
        parameter: req.body?.parameter,
        value: req.body?.value,
        appliedAt: new Date().toISOString()
    };
    res.json({ success: true, entry: aiCache.optimization.lastApplied });
});

app.post('/api/ai/alerts/range', async (req, res) => {
    try {
        await recordRangeAlert(req.body);
        res.json({ success: true });
    } catch (error) {
        res.status(500).json({ success: false, error: error.message || 'Failed to record alert' });
    }
});

// Start server
const server = app.listen(PORT, () => {
    console.log(`[Backend API] Server started on port ${PORT}`);
    console.log(`[Backend API] Health check: http://localhost:${PORT}/api/health`);
    if (AI_ENABLED) {
        console.log('[Backend API] AI workers enabled');
        initializeAIWorkers();
    } else {
        console.log('[Backend API] AI workers disabled (AI_ENABLED=false)');
    }
});

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n[Backend API] Shutting down gracefully...');
    server.close(() => {
        console.log('[Backend API] Server closed');
        process.exit(0);
    });
});

process.on('SIGTERM', () => {
    console.log('\n[Backend API] Shutting down gracefully...');
    server.close(() => {
        console.log('[Backend API] Server closed');
        process.exit(0);
    });
});






