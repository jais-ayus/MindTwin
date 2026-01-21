function shouldEvaluateChange(change) {
    if (!change) return false;
    if (typeof change.value !== 'number') return false;
    return Number.isFinite(change.value);
}

function extractParameterId(change) {
    if (!change) return null;
    if (change.parameter) return change.parameter;
    if (Array.isArray(change.commands) && change.commands.length === 1 && change.commands[0]?.parameter) {
        return change.commands[0].parameter;
    }
    return change.id;
}

function maybeRequestParameterPreview(component, change) {
    if (!component || !change) return;
    if (!shouldEvaluateChange(change)) {
        clearPendingRisk(component.name, change.id);
        return;
    }
    const parameterId = extractParameterId(change);
    if (!parameterId) return;
    const existing = getPendingRisk(component.name, change.id);
    if (existing) {
        if (existing.state === 'loading') return;
        if (existing.state === 'ready' && existing.value === change.value) return;
    }
    const timerKey = `${component.name}:${change.id}`;
    if (pendingRiskTimers.has(timerKey)) {
        clearTimeout(pendingRiskTimers.get(timerKey));
    }
    const timeout = setTimeout(() => {
        pendingRiskTimers.delete(timerKey);
        fetchParameterRisk(component, change, parameterId);
    }, PARAMETER_RISK_DEBOUNCE_MS);
    pendingRiskTimers.set(timerKey, timeout);
}

async function fetchParameterRisk(component, change, parameterId) {
    setPendingRisk(component.name, change.id, { state: 'loading', value: change.value });
    refreshPendingUI(component);
    try {
        const response = await fetch(`${CONTROL_PANEL_AI_BASE_URL}/api/ai/parameter/evaluate`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                componentId: component.name,
                componentType: component.type,
                parameter: parameterId,
                value: change.value,
                currentValue: component.value,
                metadata: component.metadata || {},
                context: {
                    source: 'dashboard-pending',
                    pendingLabel: change.label
                }
            })
        });
        if (!response.ok) throw new Error(`AI parameter evaluate failed (${response.status})`);
        const data = await response.json();
        const warning = (data.warnings || []).find(w => w.componentId === component.name && w.parameter === parameterId);
        if (warning) {
            setPendingRisk(component.name, change.id, { state: 'ready', value: change.value, risk: warning });
        } else {
            setPendingRisk(component.name, change.id, { state: 'clear', value: change.value });
        }
    } catch (error) {
        console.warn('[AI] Parameter evaluation error', error);
        setPendingRisk(component.name, change.id, {
            state: 'error',
            value: change.value,
            message: error?.message || 'AI unavailable'
        });
    } finally {
        refreshPendingUI(component);
    }
}
// Control Panel - Renders control UI for selected components
// Enhanced with real-time parameter controls and instant feedback

// Pending change tracking (per component)
const pendingChanges = new Map(); // Map<componentName, Map<changeId, PendingChange>>
const pendingRiskState = new Map(); // Map<componentName, Map<changeId, RiskInfo>>
const pendingRiskTimers = new Map(); // Map<string, Timeout>
const PARAMETER_RISK_DEBOUNCE_MS = 350;
const CONTROL_PANEL_AI_BASE_URL = (typeof API_CONFIG !== 'undefined' && API_CONFIG.baseUrl)
    ? API_CONFIG.baseUrl
    : 'http://localhost:3000';

/**
 * Pending change structure:
 * {
 *   id: 'TargetSpeed',
 *   label: 'Target Speed',
 *   value: 200,
 *   displayValue: '200 mm/s',
 *   commands: [{ parameter: 'TargetSpeed', value: 200, delayAfter: 0 }]
 * }
 */

function getPendingMap(componentName) {
    if (!pendingChanges.has(componentName)) {
        pendingChanges.set(componentName, new Map());
    }
    return pendingChanges.get(componentName);
}

function getPendingChanges(componentName) {
    return Array.from((pendingChanges.get(componentName) || new Map()).values());
}

function hasPendingChanges(componentName) {
    const map = pendingChanges.get(componentName);
    return map ? map.size > 0 : false;
}

function getPendingValue(component, changeId, fallbackValue) {
    const componentName = component?.name;
    if (!componentName) return fallbackValue;
    const map = pendingChanges.get(componentName);
    if (!map) return fallbackValue;
    const change = map.get(changeId);
    return change ? change.value : fallbackValue;
}

function stagePendingChange(component, change) {
    if (!component || !component.name) return;
    const map = getPendingMap(component.name);
    map.set(change.id, change);
    refreshPendingUI(component);
    maybeRequestParameterPreview(component, change);
}

function clearPendingChange(componentName, changeId) {
    const map = pendingChanges.get(componentName);
    if (!map) return;
    map.delete(changeId);
    clearPendingRisk(componentName, changeId);
    if (map.size === 0) {
        pendingChanges.delete(componentName);
    }
}

function clearPendingChanges(component) {
    if (!component || !component.name) return;
    pendingChanges.delete(component.name);
    pendingRiskState.delete(component.name);
    refreshPendingUI(component);
}

function getRiskMap(componentName) {
    if (!pendingRiskState.has(componentName)) {
        pendingRiskState.set(componentName, new Map());
    }
    return pendingRiskState.get(componentName);
}

function setPendingRisk(componentName, changeId, info) {
    if (!componentName || !changeId) return;
    const map = getRiskMap(componentName);
    map.set(changeId, info);
}

function getPendingRisk(componentName, changeId) {
    const map = pendingRiskState.get(componentName);
    if (!map) return null;
    return map.get(changeId) || null;
}

function clearPendingRisk(componentName, changeId) {
    if (!componentName) return;
    if (changeId) {
        const timerKey = `${componentName}:${changeId}`;
        if (pendingRiskTimers.has(timerKey)) {
            clearTimeout(pendingRiskTimers.get(timerKey));
            pendingRiskTimers.delete(timerKey);
        }
    } else {
        [...pendingRiskTimers.keys()].forEach(key => {
            if (key.startsWith(`${componentName}:`)) {
                clearTimeout(pendingRiskTimers.get(key));
                pendingRiskTimers.delete(key);
            }
        });
    }
    const map = pendingRiskState.get(componentName);
    if (!map) return;
    if (changeId) {
        map.delete(changeId);
        if (map.size === 0) {
            pendingRiskState.delete(componentName);
        }
        return;
    }
    pendingRiskState.delete(componentName);
}

function refreshPendingUI(component) {
    if (!component) return;
    updatePendingSummary(component);
    updatePendingButtons(component);
    highlightPendingControls(component);
}

function updatePendingSummary(component) {
    const summaryEl = document.getElementById('pending-summary');
    if (!summaryEl) return;
    
    const pendingList = getPendingChanges(component.name);
    if (pendingList.length === 0) {
        summaryEl.innerHTML = '<div class="no-pending">No pending changes</div>';
        return;
    }
    
    const listItems = pendingList.map(change => {
        const riskBadge = renderPendingRiskBadge(component.name, change.id);
        return `
            <li class="pending-item" data-change-id="${change.id}">
                <span class="pending-label">${escapeHtml(change.label)}</span>
                <span class="pending-value">${escapeHtml(change.displayValue || String(change.value))}</span>
                ${riskBadge}
                <button class="pending-remove" data-remove-change="${change.id}">✕</button>
            </li>
        `;
    }).join('');
    
    summaryEl.innerHTML = `<ul class="pending-list">${listItems}</ul>`;
    
    // Remove handlers
    summaryEl.querySelectorAll('[data-remove-change]').forEach(btn => {
        btn.addEventListener('click', () => {
            clearPendingChange(component.name, btn.dataset.removeChange);
            renderControlPanel(component);
        });
    });
}

function updatePendingButtons(component) {
    const applyBtn = document.getElementById('pending-apply');
    const revertBtn = document.getElementById('pending-revert');
    const hasPending = hasPendingChanges(component.name);
    if (applyBtn) applyBtn.disabled = !hasPending;
    if (revertBtn) revertBtn.disabled = !hasPending;
}

function highlightPendingControls(component) {
    const pendingIds = new Set(getPendingChanges(component.name).map(change => change.id));
    const riskLevels = new Map();
    getPendingChanges(component.name).forEach(change => {
        const riskInfo = getPendingRisk(component.name, change.id);
        if (riskInfo?.risk?.risk) {
            riskLevels.set(change.id, (riskInfo.risk.risk || '').toLowerCase());
        }
    });
    document.querySelectorAll('#controlContent [data-control-id]').forEach(el => {
        el.classList.remove('pending-risk-critical', 'pending-risk-high', 'pending-risk-medium', 'pending-risk-low');
        if (pendingIds.has(el.dataset.controlId)) {
            el.classList.add('pending-change');
            const severity = riskLevels.get(el.dataset.controlId);
            if (severity) {
                el.classList.add(`pending-risk-${severity}`);
            }
        } else {
            el.classList.remove('pending-change');
        }
    });
}

function buildPendingDisplay(component) {
    const pendingList = getPendingChanges(component.name);
    if (pendingList.length === 0) {
        return '<div class="no-pending">No pending changes</div>';
    }
    const listItems = pendingList.map(change => `
        <li class="pending-item">
            <span class="pending-label">${escapeHtml(change.label)}</span>
            <span class="pending-value">${escapeHtml(change.displayValue || String(change.value))}</span>
            ${renderPendingRiskBadge(component.name, change.id)}
        </li>
    `).join('');
    return `<ul class="pending-list">${listItems}</ul>`;
}

function renderPendingRiskBadge(componentName, changeId) {
    const riskInfo = getPendingRisk(componentName, changeId);
    if (!riskInfo) return '';
    if (riskInfo.state === 'loading') {
        return '<span class="pending-risk pending-risk-loading">AI evaluating…</span>';
    }
    if (riskInfo.state === 'error') {
        return `<span class="pending-risk pending-risk-error">${escapeHtml(riskInfo.message || 'AI unavailable')}</span>`;
    }
    if (riskInfo.state === 'ready' && riskInfo.risk) {
        const severity = escapeHtml(riskInfo.risk.risk || 'medium');
        const wear = Number(riskInfo.risk.wearMultiplier || 1).toFixed(2);
        return `
            <span class="pending-risk pending-risk-${severity}">
                ${escapeHtml(severity.toUpperCase())} · Wear x${wear}
            </span>
        `;
    }
    return '';
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// Render control panel for selected component
function renderControlPanel(component) {
    const controlContent = document.getElementById('controlContent');
    if (!controlContent) return;
    
    if (!component) {
        controlContent.innerHTML = '<div class="no-selection">Select a component to control</div>';
        return;
    }
    
    const liveEnabled = component.active !== false;
    const stagedEnabled = getPendingValue(component, 'Enabled', liveEnabled);
    const isHalted = component.status === 'HALTED';
    
    let html = `
        <div class="control-header">
            <h3 class="component-title">${escapeHtml(component.name)}</h3>
            <span class="component-badge ${component.type.toLowerCase()}">${escapeHtml(component.type)}</span>
        </div>
        
        <!-- Master Enable/Disable Toggle -->
        <div class="master-control">
            <div class="master-toggle ${isHalted ? 'halted' : (stagedEnabled ? 'enabled' : 'disabled')}">
                <button class="toggle-btn enable ${stagedEnabled && !isHalted ? 'active' : ''}" id="component-enable" data-control-id="Enabled" ${isHalted ? 'disabled' : ''}>
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/></svg>
                    ENABLE
                </button>
                <button class="toggle-btn disable ${!stagedEnabled && !isHalted ? 'active' : ''}" id="component-disable" data-control-id="Enabled" ${isHalted ? 'disabled' : ''}>
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg>
                    DISABLE
                </button>
            </div>
            ${isHalted ? '<div class="halted-warning">⚠️ Component halted - Resume production first</div>' : ''}
        </div>
    `;
    
    // Render type-specific controls
    switch (component.type) {
        case 'Drive':
            html += renderDriveControls(component);
            break;
        case 'Sensor':
            html += renderSensorControls(component);
            break;
        case 'Lamp':
            html += renderLampControls(component);
            break;
        case 'Source':
            html += renderSourceControls(component);
            break;
        case 'Grip':
            html += renderGripControls(component);
            break;
        case 'Axis':
            html += renderAxisControls(component);
            break;
        case 'Sink':
            html += renderSinkControls(component);
            break;
        default:
            html += '<div class="no-controls">No specific controls available</div>';
    }
    
    html += renderPendingActions(component);
    
    controlContent.innerHTML = html;
    attachControlListeners(component);
    refreshPendingUI(component);
}

function renderPendingActions(component) {
    const pendingHtml = buildPendingDisplay(component);
    const hasPending = hasPendingChanges(component.name);
    
    return `
        <div class="pending-section">
            <div class="pending-summary" id="pending-summary">
                ${pendingHtml}
            </div>
            <div class="pending-buttons">
                <button class="btn btn-primary" id="pending-apply" ${hasPending ? '' : 'disabled'}>Apply Changes</button>
                <button class="btn btn-secondary" id="pending-revert" ${hasPending ? '' : 'disabled'}>Revert</button>
            </div>
        </div>
    `;
}

// Render Drive controls with real-time speed slider
function renderDriveControls(component) {
    const currentSpeed = component.value || 0;
    const stagedSpeed = getPendingValue(component, 'TargetSpeed', currentSpeed);
    const stagedDirection = getPendingValue(component, 'Direction', null);
    const maxSpeed = 500; // Default max, can be component-specific
    
    return `
        <div class="control-section">
            <div class="section-title">Speed Control</div>
            
            <div class="speed-control">
                <div class="speed-display">
                    <span class="speed-value" id="speed-display-value">${stagedSpeed.toFixed(1)}</span>
                    <span class="speed-unit">mm/s</span>
                </div>
                
                <div class="slider-container">
                    <input type="range" 
                           id="drive-speed" 
                           class="speed-slider" 
                           min="0" 
                           max="${maxSpeed}" 
                           value="${stagedSpeed}" 
                           step="1"
                           data-control-id="TargetSpeed">
                    <div class="slider-labels">
                        <span>0</span>
                        <span>${maxSpeed / 2}</span>
                        <span>${maxSpeed}</span>
                    </div>
                </div>
                
                <div class="speed-input-group">
                    <input type="number" 
                           id="drive-speed-input" 
                           class="speed-input" 
                           min="0" 
                           max="${maxSpeed}" 
                           value="${stagedSpeed}" 
                           step="1"
                           data-control-id="TargetSpeed">
                </div>
            </div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Direction</div>
            <div class="direction-controls">
                <button class="direction-btn forward ${stagedDirection === 'Forward' ? 'active' : ''}" id="drive-forward" data-control-id="Direction">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M4 11v2h12l-5.5 5.5 1.42 1.42L19.84 12l-7.92-7.92-1.42 1.42L16 11H4z"/></svg>
                    Forward
                </button>
                <button class="direction-btn stop ${stagedDirection === 'Stop' ? 'active' : ''}" id="drive-stop" data-control-id="Direction">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 6h12v12H6z"/></svg>
                    Stop
                </button>
                <button class="direction-btn backward ${stagedDirection === 'Backward' ? 'active' : ''}" id="drive-backward" data-control-id="Direction">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M20 11v2H8l5.5 5.5-1.42 1.42L4.16 12l7.92-7.92 1.42 1.42L8 11h12z"/></svg>
                    Backward
                </button>
            </div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Quick Speed Presets</div>
            <div class="preset-buttons">
                <button class="preset-btn" data-speed="0">Stop</button>
                <button class="preset-btn" data-speed="50">Slow</button>
                <button class="preset-btn" data-speed="100">Medium</button>
                <button class="preset-btn" data-speed="200">Fast</button>
                <button class="preset-btn" data-speed="${maxSpeed}">Max</button>
            </div>
        </div>
    `;
}

// Render Sensor controls
function renderSensorControls(component) {
    const isOccupied = component.status === 'Occupied';
    const currentTag = component.limitSensorTag || component.LimitSensorToTag || '';
    const stagedTag = getPendingValue(component, 'LimitSensorToTag', currentTag);
    
    return `
        <div class="control-section">
            <div class="section-title">Sensor Status</div>
            <div class="sensor-status ${isOccupied ? 'occupied' : 'free'}">
                <div class="sensor-indicator"></div>
                <span>${isOccupied ? 'OCCUPIED' : 'FREE'}</span>
            </div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Tag Filter</div>
            <div class="tag-filter">
                <input type="text" id="sensor-tag" placeholder="Enter tag to filter..." maxlength="50" value="${escapeHtml(stagedTag)}" data-control-id="LimitSensorToTag">
                <button class="apply-btn" id="sensor-tag-apply">Stage Filter</button>
            </div>
            <div class="info-text">Filter sensor to only detect MUs with specific tag</div>
        </div>
    `;
}

// Render Lamp controls
function renderLampControls(component) {
    const isOn = component.status && component.status.includes('ON');
    const isFlashing = component.status && component.status.includes('Flashing');
    const stagedOn = getPendingValue(component, 'LampOn', isOn);
    const stagedFlashing = getPendingValue(component, 'Flashing', isFlashing);
    
    return `
        <div class="control-section">
            <div class="section-title">Lamp State</div>
            <div class="lamp-controls">
                <button class="lamp-btn on ${stagedOn ? 'active' : ''}" id="lamp-on" data-control-id="LampOn">
                    <div class="lamp-icon ${stagedOn ? 'lit' : ''}"></div>
                    ON
                </button>
                <button class="lamp-btn off ${!stagedOn ? 'active' : ''}" id="lamp-off" data-control-id="LampOn">
                    <div class="lamp-icon off"></div>
                    OFF
                </button>
            </div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Flashing Mode</div>
            <button class="flash-toggle ${stagedFlashing ? 'active' : ''}" id="lamp-flashing" data-control-id="Flashing">
                <span class="flash-indicator ${stagedFlashing ? 'flashing' : ''}"></span>
                ${stagedFlashing ? 'Disable Flashing' : 'Enable Flashing'}
            </button>
        </div>
    `;
}

// Render Source controls
function renderSourceControls(component) {
    const metadata = component.metadata || {};
    const stagedAuto = getPendingValue(component, 'AutomaticGeneration', metadata.automaticGeneration === true ? true : false);
    const createdCount = metadata.createdCount ?? (component.value || 0);
    const maxMUs = metadata.maxMUs;
    const limitNumber = metadata.limitNumber === true;
    const plcGenerateBound = metadata.plcGenerateBound === true;
    const plcGenerateActive = metadata.plcGenerateActive === true;
    const plcDistanceBound = metadata.plcDistanceBound === true;
    const plcDistanceActive = metadata.plcDistanceActive === true;
    const stagedEnabled = getPendingValue(component, 'Enabled', metadata.enabled !== false);
    const modeLabel = stagedAuto ? 'Automatic Distance' : 'Manual';
    const plcStatus = plcGenerateBound ? (plcGenerateActive ? 'PLC Demand Active' : 'PLC Idle') : 'No PLC generate signal bound';
    const distanceStatus = plcDistanceBound ? (plcDistanceActive ? 'PLC Distance Signal ON' : 'PLC Distance Signal OFF') : 'Local distance trigger';
    const progressText = limitNumber && typeof maxMUs === 'number'
        ? `${createdCount}/${maxMUs} MUs`
        : `${createdCount} MUs created`;
    
    return `
        <div class="control-section">
            <div class="section-title">Source Overview</div>
            <div class="source-status ${stagedEnabled ? 'active' : 'inactive'}" data-control-id="Enabled">
                <div class="source-indicator ${stagedEnabled ? 'generating' : ''}"></div>
                <div>
                    <div class="source-mode">${modeLabel}</div>
                    <div class="source-progress">${progressText}</div>
                    <div class="source-plc">${plcStatus}</div>
                    <div class="source-plc">${distanceStatus}</div>
            </div>
            </div>
            <div class="source-controls compact">
                <button class="source-btn start ${stagedEnabled ? 'active' : ''}" id="source-enable" data-control-id="Enabled">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M8 5v14l11-7z"/></svg>
                    Enable
                </button>
                <button class="source-btn stop ${!stagedEnabled ? 'active' : ''}" id="source-disable" data-control-id="Enabled">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 6h12v12H6z"/></svg>
                    Disable
                </button>
            </div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Generation Mode</div>
            <div class="toggle-row">
                <label class="toggle-control" data-control-id="AutomaticGeneration">
                    <input type="checkbox" id="source-auto-toggle" ${stagedAuto ? 'checked' : ''}>
                    <span>Automatic Distance Generation</span>
                </label>
            </div>
            ${plcDistanceBound ? `<div class="info-text">Linked to PLC distance signal (${plcDistanceActive ? 'ON' : 'OFF'}). Dashboard toggle updates the PLC value.</div>` : '<div class="info-text">Distance mode is handled locally.</div>'}
        </div>
        
        <div class="control-section">
            <div class="section-title">Manual Commands</div>
            <div class="source-controls">
                <button class="source-btn pulse" id="source-generate-once" data-control-id="GenerateMU">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2l4 8h-8l4-8zm0 10c-4.42 0-8 1.79-8 4v4h16v-4c0-2.21-3.58-4-8-4z"/></svg>
                    Queue Generate 1 MU
                </button>
                <button class="source-btn danger" id="source-delete-all" data-control-id="DeleteAllMU">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zm3.46-9.12l1.41-1.41L12 10.59l1.12-1.12 1.41 1.41L13.41 12l1.12 1.12-1.41 1.41L12 13.41l-1.12 1.12-1.41-1.41L10.59 12l-1.13-1.12zM15.5 4l-1-1h-5l-1 1H5v2h14V4z"/></svg>
                    Queue Delete All
                </button>
            </div>
            ${plcGenerateBound ? `<div class="info-text">Pulse commands use the PLC generate signal (${plcGenerateActive ? 'currently HIGH' : 'low'}).</div>` : '<div class="info-text">Pulse commands toggle the local Generate MU flag.</div>'}
        </div>
    `;
}

// Render Grip controls
function renderGripControls(component) {
    const holdingCount = component.value || 0;
    const isHolding = holdingCount > 0;
    
    return `
        <div class="control-section">
            <div class="section-title">Gripper Status</div>
            <div class="grip-status">
                <div class="grip-visual ${isHolding ? 'holding' : 'open'}">
                    <div class="grip-finger left"></div>
                    <div class="grip-object ${isHolding ? 'visible' : ''}"></div>
                    <div class="grip-finger right"></div>
                </div>
                <div class="grip-info">
                    Holding: <strong>${holdingCount}</strong> object${holdingCount !== 1 ? 's' : ''}
                </div>
            </div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Grip Actions</div>
            <div class="grip-controls">
                <button class="grip-btn pick" id="grip-pick" data-control-id="PickObjects">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M5 9v6h14V9H5m0-2h14c1.1 0 2 .9 2 2v6c0 1.1-.9 2-2 2H5c-1.1 0-2-.9-2-2V9c0-1.1.9-2 2-2z"/></svg>
                    Pick Objects
                </button>
                <button class="grip-btn place" id="grip-place" data-control-id="PlaceObjects">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M19 11H5V9h14v2m0 4H5v-2h14v2z"/></svg>
                    Place Objects
                </button>
            </div>
        </div>
    `;
}

// Render Axis controls with position slider
function renderAxisControls(component) {
    const currentPosition = component.value || 0;
    const minPos = -1000;
    const maxPos = 1000;
    const isMoving = component.status === 'Moving';
    const stagedPosition = getPendingValue(component, 'TargetPosition', currentPosition);
    
    return `
        <div class="control-section">
            <div class="section-title">Position Control</div>
            
            <div class="position-display">
                <span class="position-value" id="position-display-value">${stagedPosition.toFixed(1)}</span>
                <span class="position-unit">mm</span>
                <span class="position-status ${isMoving ? 'moving' : 'idle'}">${isMoving ? 'MOVING' : 'IDLE'}</span>
            </div>
            
            <div class="slider-container">
                <input type="range" 
                       id="axis-position-slider" 
                       class="position-slider" 
                       min="${minPos}" 
                       max="${maxPos}" 
                       value="${stagedPosition}" 
                       step="1"
                       data-control-id="TargetPosition">
                <div class="slider-labels">
                    <span>${minPos}</span>
                    <span>0</span>
                    <span>${maxPos}</span>
                </div>
            </div>
            
            <div class="position-input-group">
                <input type="number" 
                       id="axis-position" 
                       class="position-input" 
                       min="${minPos}" 
                       max="${maxPos}" 
                       value="${stagedPosition}" 
                       step="0.1"
                       data-control-id="TargetPosition">
            </div>
            <div class="info-text">Axis will move to staged position when you apply.</div>
        </div>
        
        <div class="control-section">
            <div class="section-title">Quick Positions</div>
            <div class="preset-buttons">
                <button class="preset-btn" data-position="${minPos}">Min</button>
                <button class="preset-btn" data-position="-500">-500</button>
                <button class="preset-btn" data-position="0">Home</button>
                <button class="preset-btn" data-position="500">+500</button>
                <button class="preset-btn" data-position="${maxPos}">Max</button>
            </div>
        </div>
    `;
}

// Render Sink controls
function renderSinkControls(component) {
    return `
        <div class="control-section">
            <div class="section-title">Sink Information</div>
            <div class="sink-info">
                <div class="sink-icon">
                    <svg viewBox="0 0 24 24" fill="currentColor"><path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 14l-5-5h3V8h4v4h3l-5 5z"/></svg>
                </div>
                <p>Sink component - receives and removes material units from the simulation.</p>
                <p class="info-secondary">No additional controls available for sink components.</p>
            </div>
        </div>
    `;
}

// Attach event listeners to controls
function attachControlListeners(component) {
    if (!component) return;
    
    // Master Enable/Disable
    const enableBtn = document.getElementById('component-enable');
    const disableBtn = document.getElementById('component-disable');
    
    if (enableBtn) {
        enableBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'Enabled',
                label: 'Component State',
                value: true,
                displayValue: 'Enabled',
                commands: [{ parameter: 'Enabled', value: true }]
            });
                enableBtn.classList.add('active');
                if (disableBtn) disableBtn.classList.remove('active');
        });
    }
    
    if (disableBtn) {
        disableBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'Enabled',
                label: 'Component State',
                value: false,
                displayValue: 'Disabled',
                commands: [{ parameter: 'Enabled', value: false }]
            });
                disableBtn.classList.add('active');
                if (enableBtn) enableBtn.classList.remove('active');
        });
    }
    
    // Type-specific listeners
    switch (component.type) {
        case 'Drive':
            attachDriveListeners(component);
            break;
        case 'Sensor':
            attachSensorListeners(component);
            break;
        case 'Lamp':
            attachLampListeners(component);
            break;
        case 'Source':
            attachSourceListeners(component);
            break;
        case 'Grip':
            attachGripListeners(component);
            break;
        case 'Axis':
            attachAxisListeners(component);
            break;
    }
    
    const applyPendingBtn = document.getElementById('pending-apply');
    if (applyPendingBtn) {
        applyPendingBtn.addEventListener('click', () => applyPendingChanges(component));
    }
    
    const revertPendingBtn = document.getElementById('pending-revert');
    if (revertPendingBtn) {
        revertPendingBtn.addEventListener('click', () => revertPendingChanges(component));
    }
}

async function applyPendingChanges(component) {
    if (!component) return;
    const pendingList = getPendingChanges(component.name);
    if (pendingList.length === 0) return;
    
    const applyBtn = document.getElementById('pending-apply');
    const revertBtn = document.getElementById('pending-revert');
    if (applyBtn) applyBtn.disabled = true;
    if (revertBtn) revertBtn.disabled = true;
    
    try {
        for (const change of pendingList) {
            for (const command of change.commands) {
                const result = await sendCommand(component.name, command.parameter, command.value);
                if (!result.success) {
                    throw new Error(result.error || `Failed to apply ${command.parameter}`);
                }
                if (command.delayAfter) {
                    await sleep(command.delayAfter);
                }
            }
        }
        clearPendingChanges(component);
        renderControlPanel(component);
    } catch (error) {
        console.error('[Pending] Failed to apply changes:', error);
        alert(`Failed to apply changes: ${error?.message || error}`);
        refreshPendingUI(component);
    } finally {
        if (applyBtn) applyBtn.disabled = !hasPendingChanges(component.name);
        if (revertBtn) revertBtn.disabled = !hasPendingChanges(component.name);
    }
}

function revertPendingChanges(component) {
    if (!component) return;
    clearPendingChanges(component);
    renderControlPanel(component);
}

// Drive listeners with real-time feedback
function attachDriveListeners(component) {
    const speedSlider = document.getElementById('drive-speed');
    const speedInput = document.getElementById('drive-speed-input');
    const speedDisplay = document.getElementById('speed-display-value');
    const forwardBtn = document.getElementById('drive-forward');
    const backwardBtn = document.getElementById('drive-backward');
    const stopBtn = document.getElementById('drive-stop');
    
    const stageSpeed = (value) => {
        if (isNaN(value)) return;
        stagePendingChange(component, {
            id: 'TargetSpeed',
            label: 'Target Speed',
            value,
            displayValue: `${value.toFixed(1)} mm/s`,
            commands: [{ parameter: 'TargetSpeed', value }]
        });
        if (speedDisplay) speedDisplay.textContent = value.toFixed(1);
        if (speedSlider) speedSlider.value = value;
        if (speedInput) speedInput.value = value;
    };
    
    if (speedSlider) {
        speedSlider.addEventListener('input', (e) => {
            const value = parseFloat(e.target.value);
            stageSpeed(value);
        });
    }
    
    if (speedInput) {
        speedInput.addEventListener('input', (e) => {
            const value = parseFloat(e.target.value);
            if (!isNaN(value)) {
                stageSpeed(value);
            }
        });
    }
    
    const stageDirection = (direction) => {
        const commands = [];
        if (direction === 'Forward') {
            commands.push(
                { parameter: 'JogForward', value: true },
                { parameter: 'JogBackward', value: false }
            );
        } else if (direction === 'Backward') {
            commands.push(
                { parameter: 'JogForward', value: false },
                { parameter: 'JogBackward', value: true }
            );
        } else {
            commands.push(
                { parameter: 'JogForward', value: false },
                { parameter: 'JogBackward', value: false }
            );
        }
        
        stagePendingChange(component, {
            id: 'Direction',
            label: 'Direction',
            value: direction,
            displayValue: direction,
            commands
        });
        
        [forwardBtn, backwardBtn, stopBtn].forEach(btn => btn && btn.classList.remove('active'));
        if (direction === 'Forward' && forwardBtn) forwardBtn.classList.add('active');
        if (direction === 'Backward' && backwardBtn) backwardBtn.classList.add('active');
        if (direction === 'Stop' && stopBtn) stopBtn.classList.add('active');
    };
    
    if (forwardBtn) {
        forwardBtn.addEventListener('click', () => stageDirection('Forward'));
    }
    
    if (backwardBtn) {
        backwardBtn.addEventListener('click', () => stageDirection('Backward'));
    }
    
    if (stopBtn) {
        stopBtn.addEventListener('click', () => stageDirection('Stop'));
    }
    
    document.querySelectorAll('#controlContent .preset-btn[data-speed]').forEach(btn => {
        btn.addEventListener('click', () => {
            const speed = parseFloat(btn.dataset.speed);
            stageSpeed(speed);
        });
    });
}

// Sensor listeners
function attachSensorListeners(component) {
    const tagInput = document.getElementById('sensor-tag');
    const tagApplyBtn = document.getElementById('sensor-tag-apply');
    
    if (tagApplyBtn && tagInput) {
        const stageTag = () => {
            const tag = tagInput.value.trim();
            stagePendingChange(component, {
                id: 'LimitSensorToTag',
                label: 'Sensor Tag Filter',
                value: tag,
                displayValue: tag || 'Any',
                commands: [{ parameter: 'LimitSensorToTag', value: tag }]
            });
        };
        
        tagApplyBtn.addEventListener('click', stageTag);
        
        tagInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                stageTag();
            }
        });
    }
}

// Lamp listeners
function attachLampListeners(component) {
    const onBtn = document.getElementById('lamp-on');
    const offBtn = document.getElementById('lamp-off');
    const flashingBtn = document.getElementById('lamp-flashing');
    
    if (onBtn) {
        onBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'LampOn',
                label: 'Lamp State',
                value: true,
                displayValue: 'On',
                commands: [{ parameter: 'LampOn', value: true }]
            });
            onBtn.classList.add('active');
            if (offBtn) offBtn.classList.remove('active');
        });
    }
    
    if (offBtn) {
        offBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'LampOn',
                label: 'Lamp State',
                value: false,
                displayValue: 'Off',
                commands: [{ parameter: 'LampOn', value: false }]
            });
            offBtn.classList.add('active');
            if (onBtn) onBtn.classList.remove('active');
        });
    }
    
    if (flashingBtn) {
        flashingBtn.addEventListener('click', () => {
            const isFlashing = flashingBtn.classList.contains('active');
            const next = !isFlashing;
            stagePendingChange(component, {
                id: 'Flashing',
                label: 'Flashing Mode',
                value: next,
                displayValue: next ? 'Flashing' : 'Steady',
                commands: [{ parameter: 'Flashing', value: next }]
            });
            flashingBtn.classList.toggle('active', next);
            const indicator = flashingBtn.querySelector('.flash-indicator');
            if (indicator) indicator.classList.toggle('flashing', next);
            flashingBtn.innerHTML = `
                <span class="flash-indicator ${next ? 'flashing' : ''}"></span>
                ${next ? 'Disable Flashing' : 'Enable Flashing'}
            `;
        });
    }
}

// Source listeners
function attachSourceListeners(component) {
    const enableBtn = document.getElementById('source-enable');
    const disableBtn = document.getElementById('source-disable');
    const autoToggle = document.getElementById('source-auto-toggle');
    const generateBtn = document.getElementById('source-generate-once');
    const deleteBtn = document.getElementById('source-delete-all');
    
    if (enableBtn) {
        enableBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'Enabled',
                label: 'Component State',
                value: true,
                displayValue: 'Enabled',
                commands: [{ parameter: 'Enabled', value: true }]
            });
            enableBtn.classList.add('active');
            if (disableBtn) disableBtn.classList.remove('active');
        });
    }
    
    if (disableBtn) {
        disableBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'Enabled',
                label: 'Component State',
                value: false,
                displayValue: 'Disabled',
                commands: [{ parameter: 'Enabled', value: false }]
            });
            disableBtn.classList.add('active');
            if (enableBtn) enableBtn.classList.remove('active');
        });
    }
    
    if (autoToggle) {
        autoToggle.addEventListener('change', (e) => {
            const value = e.target.checked;
            stagePendingChange(component, {
                id: 'AutomaticGeneration',
                label: 'Automatic Generation',
                value,
                displayValue: value ? 'Automatic Distance' : 'Manual',
                commands: [{ parameter: 'AutomaticGeneration', value }]
            });
        });
    }
    
    if (generateBtn) {
        generateBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'GenerateMU',
                label: 'Generate MU Pulse',
                value: true,
                displayValue: 'Emit 1 MU',
                commands: [{ parameter: 'GenerateMU', value: true }]
            });
        });
    }
    
    if (deleteBtn) {
        deleteBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'DeleteAllMU',
                label: 'Delete All MUs',
                value: true,
                displayValue: 'Clear generated MUs',
                commands: [{ parameter: 'DeleteAllMU', value: true }]
            });
        });
    }
}

// Grip listeners
function attachGripListeners(component) {
    const pickBtn = document.getElementById('grip-pick');
    const placeBtn = document.getElementById('grip-place');
    
    if (pickBtn) {
        pickBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'PickObjects',
                label: 'Pick Objects',
                value: 'Queued',
                displayValue: 'Pick on apply',
                commands: [
                    { parameter: 'PickObjects', value: true, delayAfter: 500 },
                    { parameter: 'PickObjects', value: false }
                ]
            });
        });
    }
    
    if (placeBtn) {
        placeBtn.addEventListener('click', () => {
            stagePendingChange(component, {
                id: 'PlaceObjects',
                label: 'Place Objects',
                value: 'Queued',
                displayValue: 'Place on apply',
                commands: [
                    { parameter: 'PlaceObjects', value: true, delayAfter: 500 },
                    { parameter: 'PlaceObjects', value: false }
                ]
            });
        });
    }
}

// Axis listeners with real-time feedback
function attachAxisListeners(component) {
    const positionSlider = document.getElementById('axis-position-slider');
    const positionInput = document.getElementById('axis-position');
    const positionDisplay = document.getElementById('position-display-value');
    
    const stagePosition = (value) => {
        if (isNaN(value)) return;
        stagePendingChange(component, {
            id: 'TargetPosition',
            label: 'Target Position',
            value,
            displayValue: `${value.toFixed(1)} mm`,
            commands: [
                { parameter: 'TargetPosition', value },
                { parameter: 'TargetStartMove', value: true, delayAfter: 400 },
                { parameter: 'TargetStartMove', value: false }
            ]
        });
        if (positionSlider) positionSlider.value = value;
            if (positionInput) positionInput.value = value;
            if (positionDisplay) positionDisplay.textContent = value.toFixed(1);
    };
    
    if (positionSlider) {
        positionSlider.addEventListener('input', (e) => {
            stagePosition(parseFloat(e.target.value));
        });
    }
    
    if (positionInput) {
        positionInput.addEventListener('input', (e) => {
            stagePosition(parseFloat(e.target.value));
        });
            }
    
    document.querySelectorAll('#controlContent .preset-btn[data-position]').forEach(btn => {
        btn.addEventListener('click', () => {
            stagePosition(parseFloat(btn.dataset.position));
        });
    });
}

// Render status panel with enhanced details
function renderStatusPanel(component) {
    const statusContent = document.getElementById('statusContent');
    if (!statusContent) return;
    
    if (!component) {
        statusContent.innerHTML = '<div class="no-selection">Select a component for details</div>';
        return;
    }
    
    const statusClass = component.status === 'HALTED' ? 'halted' : 
                        (component.active ? 'active' : 'inactive');
    
    const html = `
        <div class="status-details">
            <div class="status-header">
                <span class="status-badge ${statusClass}">${escapeHtml(component.status || 'Unknown')}</span>
            </div>
            
            <div class="detail-grid">
                <div class="detail-item">
                    <span class="detail-icon">📛</span>
                    <div class="detail-content">
                        <span class="detail-label">Name</span>
                        <span class="detail-value">${escapeHtml(component.name)}</span>
                    </div>
                </div>
                
                <div class="detail-item">
                    <span class="detail-icon">🏷️</span>
                    <div class="detail-content">
                        <span class="detail-label">Type</span>
                        <span class="detail-value">${escapeHtml(component.type)}</span>
                    </div>
                </div>
                
                <div class="detail-item">
                    <span class="detail-icon">📁</span>
                    <div class="detail-content">
                        <span class="detail-label">Category</span>
                        <span class="detail-value">${escapeHtml(component.category || 'other')}</span>
                    </div>
                </div>
                
                <div class="detail-item">
                    <span class="detail-icon">📊</span>
                    <div class="detail-content">
                        <span class="detail-label">Value</span>
                        <span class="detail-value highlight">${component.value !== undefined ? component.value.toFixed(2) : 'N/A'} ${escapeHtml(component.unit || '')}</span>
                    </div>
                </div>
                
                <div class="detail-item">
                    <span class="detail-icon">${component.active ? '✅' : '❌'}</span>
                    <div class="detail-content">
                        <span class="detail-label">Active</span>
                        <span class="detail-value">${component.active ? 'Yes' : 'No'}</span>
                    </div>
                </div>
            </div>
        </div>
    `;
    
    statusContent.innerHTML = html;
}
