const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');

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

// Emergency stop state
let emergencyStopState = {
    active: false,
    reason: null,
    category: null,
    timestamp: null,
    resumedAt: null
};

// Helper function to convert Color to hex
function colorToHex(color) {
    if (!color) return '#FFFFFF';
    const r = Math.round(color.r * 255);
    const g = Math.round(color.g * 255);
    const b = Math.round(color.b * 255);
    return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
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
        timestamp: comp.timestamp || new Date().toISOString()
    };
}

// Helper function to filter components by category
function filterByCategory(components, category) {
    if (category === 'all') return components;
    
    switch(category) {
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
        
        const categories = {
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
        
        // Verify: all + conveyors (if counted separately) should match, but conveyors are subset
        // Total = sensors + (drives including conveyors) + axes + grips + lamps + sources + sinks
        // But we show: all = total unique components
        
        res.json({
            success: true,
            categories: categories,
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
                'TargetSpeed': { min: 0, max: 200, unit: 'mm/s', type: 'float', editable: true },
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
                'Enabled': { type: 'boolean', editable: true }
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
        
        // Check emergency stop (block all commands when halted)
        if (emergencyStopState.active) {
            console.log(`[Command] Blocked command for ${componentId}.${parameter} - Production is halted`);
            return res.status(409).json({ 
                success: false,
                error: { 
                    code: 'STATE_ERROR',
                    message: 'Production is halted. Cannot execute commands. Resume production first.',
                    details: { 
                        reason: emergencyStopState.reason,
                        category: emergencyStopState.category,
                        haltedAt: emergencyStopState.timestamp
                    }
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
        
        emergencyStopState = {
            active: true,
            reason: reason || 'Manual emergency stop',
            category: category || null,
            timestamp: new Date().toISOString(),
            resumedAt: null
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
            timestamp: emergencyStopState.timestamp,
            userId: req.body.userId || 'system',
            status: 'pending',
            isEmergencyCommand: true,
            priority: 'high' // Mark as high priority
        };
        
        commandQueue.set(commandId, command);
        
        console.log(`[Emergency Stop] Command created: ${commandId} for category: ${category || 'all'}`);
        
        res.json({
            success: true,
            message: 'Emergency stop activated',
            stoppedComponents: 'all',
            timestamp: emergencyStopState.timestamp
        });
    } catch (error) {
        console.error('Error activating emergency stop:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// POST /api/emergency/resume - Resume production
app.post('/api/emergency/resume', (req, res) => {
    try {
        if (!emergencyStopState.active) {
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
        
        emergencyStopState = {
            active: false,
            reason: null,
            category: null,
            timestamp: null,
            resumedAt: new Date().toISOString()
        };
        
        // Create resume command for Unity
        const commandId = `resume_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        const command = {
            commandId: commandId,
            componentId: 'all',
            category: 'all',
            parameter: 'ResumeProduction',
            value: JSON.stringify({ acknowledgedBy: req.body.acknowledgedBy || 'dashboard_user' }),
            timestamp: emergencyStopState.resumedAt,
            userId: req.body.acknowledgedBy || 'dashboard_user',
            status: 'pending',
            isEmergencyCommand: false
        };
        
        commandQueue.set(commandId, command);
        console.log(`[Resume] Command created: ${commandId}`);
        
        res.json({
            success: true,
            message: 'Production resumed',
            timestamp: emergencyStopState.resumedAt
        });
    } catch (error) {
        console.error('Error resuming production:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// GET /api/emergency/status - Get emergency stop status
app.get('/api/emergency/status', (req, res) => {
    try {
        res.json({
            success: true,
            active: emergencyStopState.active,
            reason: emergencyStopState.reason,
            category: emergencyStopState.category,
            timestamp: emergencyStopState.timestamp,
            resumedAt: emergencyStopState.resumedAt
        });
    } catch (error) {
        console.error('Error getting emergency status:', error);
        res.status(500).json({ error: 'Internal server error', message: error.message });
    }
});

// Start server
const server = app.listen(PORT, () => {
    console.log(`[Backend API] Server started on port ${PORT}`);
    console.log(`[Backend API] Health check: http://localhost:${PORT}/api/health`);
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






