// HMI Control Panel - Industrial Control Interface
// Mimics physical control panel with E-Stop, Start, indicators, mode selector

// HMI State
let hmiState = {
    mode: 'auto', // 'auto' or 'manual'
    emergencyActive: false,
    running: false,
    fault: false,
    warning: false
};

// Initialize HMI Panel
function initHMIPanel() {
    renderHMIPanel();
    setupHMIListeners();
    updateHMIFromBackend();
    
    // Periodic update of HMI state
    setInterval(updateHMIFromBackend, 1000);
}

// Render the HMI Panel
function renderHMIPanel() {
    const container = document.getElementById('hmiPanelContainer');
    if (!container) return;
    
    container.innerHTML = `
        <div class="hmi-panel">
            <div class="hmi-panel-header">
                <div class="hmi-panel-title">
                    <svg class="hmi-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                    </svg>
                    CONTROL PANEL
                </div>
            </div>
            
            <div class="hmi-panel-body">
                <!-- Top Row: E-Stop and Fault Light -->
                <div class="hmi-row">
                    <div class="hmi-control-group">
                        <button class="hmi-estop ${hmiState.emergencyActive ? 'active' : ''}" id="hmiEstop" title="Emergency Stop">
                            <div class="estop-button">
                                <div class="estop-mushroom"></div>
                                <div class="estop-ring"></div>
                            </div>
                        </button>
                        <span class="hmi-label">E-STOP</span>
                    </div>
                    
                    <div class="hmi-control-group">
                        <div class="hmi-indicator ${hmiState.fault ? 'active' : ''}" id="hmiFaultLight">
                            <div class="indicator-lens red"></div>
                            <div class="indicator-glow"></div>
                        </div>
                        <span class="hmi-label">FAULT</span>
                    </div>
                </div>
                
                <!-- Middle Row: Start Button and Warning Light -->
                <div class="hmi-row">
                    <div class="hmi-control-group">
                        <button class="hmi-start ${hmiState.running && !hmiState.emergencyActive ? 'active' : ''}" id="hmiStart" title="Start/Resume">
                            <div class="start-button">
                                <div class="start-inner"></div>
                            </div>
                        </button>
                        <span class="hmi-label">START</span>
                    </div>
                    
                    <div class="hmi-control-group">
                        <div class="hmi-indicator ${hmiState.warning ? 'active' : ''}" id="hmiWarningLight">
                            <div class="indicator-lens yellow"></div>
                            <div class="indicator-glow"></div>
                        </div>
                        <span class="hmi-label">WARNING</span>
                    </div>
                </div>
                
                <!-- Bottom Row: Mode Selector and Running Light -->
                <div class="hmi-row">
                    <div class="hmi-control-group">
                        <div class="hmi-mode-selector" id="hmiModeSelector">
                            <div class="mode-switch ${hmiState.mode === 'manual' ? 'manual' : 'auto'}">
                                <div class="mode-knob"></div>
                            </div>
                            <div class="mode-labels">
                                <span class="${hmiState.mode === 'manual' ? 'active' : ''}">MAN</span>
                                <span class="${hmiState.mode === 'auto' ? 'active' : ''}">AUTO</span>
                            </div>
                        </div>
                        <span class="hmi-label">MODE</span>
                    </div>
                    
                    <div class="hmi-control-group">
                        <div class="hmi-indicator ${hmiState.running && !hmiState.emergencyActive ? 'active' : ''}" id="hmiRunningLight">
                            <div class="indicator-lens green"></div>
                            <div class="indicator-glow"></div>
                        </div>
                        <span class="hmi-label">RUNNING</span>
                    </div>
                </div>
            </div>
            
            <div class="hmi-status-bar">
                <span class="status-text" id="hmiStatusText">
                    ${getStatusText()}
                </span>
            </div>
        </div>
    `;
}

// Get status text based on current state
function getStatusText() {
    if (hmiState.emergencyActive) return '⛔ EMERGENCY STOP ACTIVE';
    if (hmiState.fault) return '🔴 FAULT DETECTED';
    if (hmiState.warning) return '⚠️ WARNING';
    if (hmiState.running) return '✅ PRODUCTION RUNNING';
    return '⏸️ SYSTEM READY';
}

// Setup event listeners
function setupHMIListeners() {
    // E-Stop Button
    document.addEventListener('click', async (e) => {
        const estopBtn = e.target.closest('#hmiEstop');
        if (estopBtn) {
            if (!hmiState.emergencyActive) {
                // Activate emergency stop
                if (confirm('Activate Emergency Stop? This will halt all production.')) {
                    await activateEmergencyStop();
                }
            } else {
                // Already active - show message
                alert('Emergency Stop is active. Use START button to resume after clearing faults.');
            }
        }
    });
    
    // Start Button
    document.addEventListener('click', async (e) => {
        const startBtn = e.target.closest('#hmiStart');
        if (startBtn) {
            if (hmiState.emergencyActive) {
                // Resume from emergency stop
                if (confirm('Resume production? Ensure all areas are clear.')) {
                    await resumeProduction();
                }
            } else if (!hmiState.running) {
                // Start production
                await startProduction();
            }
        }
    });
    
    // Mode Selector
    document.addEventListener('click', async (e) => {
        const modeSelector = e.target.closest('#hmiModeSelector');
        if (modeSelector) {
            await toggleMode();
        }
    });
}

// Activate emergency stop
async function activateEmergencyStop() {
    try {
        const response = await fetch(`${API_CONFIG.baseUrl}/api/emergency/stop`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: 'HMI Panel Emergency Stop', category: null })
        });
        
        if (response.ok) {
            hmiState.emergencyActive = true;
            hmiState.running = false;
            hmiState.fault = true;
            updateHMIDisplay();
            
            // Also update the main dashboard buttons
            if (typeof toggleEmergencyButtons === 'function') {
                toggleEmergencyButtons(true);
            }
            if (typeof updateProductionStatus === 'function') {
                updateProductionStatus('HALTED');
            }
        }
    } catch (error) {
        console.error('[HMI] Emergency stop error:', error);
        alert('Failed to activate emergency stop!');
    }
}

// Resume production
async function resumeProduction() {
    try {
        const response = await fetch(`${API_CONFIG.baseUrl}/api/emergency/resume`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ acknowledgedBy: 'hmi_panel' })
        });
        
        if (response.ok) {
            hmiState.emergencyActive = false;
            hmiState.running = true;
            hmiState.fault = false;
            updateHMIDisplay();
            
            // Also update the main dashboard buttons
            if (typeof toggleEmergencyButtons === 'function') {
                toggleEmergencyButtons(false);
            }
            if (typeof updateProductionStatus === 'function') {
                updateProductionStatus('RUNNING');
            }
        }
    } catch (error) {
        console.error('[HMI] Resume error:', error);
        alert('Failed to resume production!');
    }
}

// Start production (when not in emergency)
async function startProduction() {
    // For now, just set running state - in future could trigger PLC start
    hmiState.running = true;
    updateHMIDisplay();
    
    // Send mode update to backend
    await updateModeOnBackend();
}

// Toggle mode between auto and manual
async function toggleMode() {
    hmiState.mode = hmiState.mode === 'auto' ? 'manual' : 'auto';
    updateHMIDisplay();
    await updateModeOnBackend();
}

// Update mode on backend
async function updateModeOnBackend() {
    try {
        await fetch(`${API_CONFIG.baseUrl}/api/hmi/mode`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ mode: hmiState.mode })
        });
    } catch (error) {
        console.error('[HMI] Mode update error:', error);
    }
}

// Update HMI state from backend
async function updateHMIFromBackend() {
    try {
        // Check emergency status
        const emergencyResponse = await fetch(`${API_CONFIG.baseUrl}/api/emergency/status`);
        if (emergencyResponse.ok) {
            const data = await emergencyResponse.json();
            hmiState.emergencyActive = data.active;
            hmiState.running = !data.active;
            hmiState.fault = data.active;
        }
        
        // Check HMI status (mode, warnings)
        try {
            const hmiResponse = await fetch(`${API_CONFIG.baseUrl}/api/hmi/status`);
            if (hmiResponse.ok) {
                const hmiData = await hmiResponse.json();
                if (hmiData.mode) hmiState.mode = hmiData.mode;
                if (hmiData.warning !== undefined) hmiState.warning = hmiData.warning;
            }
        } catch (e) {
            // HMI endpoint might not exist yet, ignore
        }
        
        updateHMIDisplay();
    } catch (error) {
        console.error('[HMI] Status update error:', error);
    }
}

// Update HMI visual display
function updateHMIDisplay() {
    // Update E-Stop button
    const estopBtn = document.getElementById('hmiEstop');
    if (estopBtn) {
        estopBtn.classList.toggle('active', hmiState.emergencyActive);
    }
    
    // Update Start button
    const startBtn = document.getElementById('hmiStart');
    if (startBtn) {
        startBtn.classList.toggle('active', hmiState.running && !hmiState.emergencyActive);
    }
    
    // Update Fault light
    const faultLight = document.getElementById('hmiFaultLight');
    if (faultLight) {
        faultLight.classList.toggle('active', hmiState.fault || hmiState.emergencyActive);
    }
    
    // Update Warning light
    const warningLight = document.getElementById('hmiWarningLight');
    if (warningLight) {
        warningLight.classList.toggle('active', hmiState.warning);
    }
    
    // Update Running light
    const runningLight = document.getElementById('hmiRunningLight');
    if (runningLight) {
        runningLight.classList.toggle('active', hmiState.running && !hmiState.emergencyActive);
    }
    
    // Update Mode selector
    const modeSwitch = document.querySelector('.mode-switch');
    if (modeSwitch) {
        modeSwitch.classList.toggle('manual', hmiState.mode === 'manual');
        modeSwitch.classList.toggle('auto', hmiState.mode === 'auto');
    }
    
    const modeLabels = document.querySelectorAll('.mode-labels span');
    if (modeLabels.length === 2) {
        modeLabels[0].classList.toggle('active', hmiState.mode === 'manual');
        modeLabels[1].classList.toggle('active', hmiState.mode === 'auto');
    }
    
    // Update status text
    const statusText = document.getElementById('hmiStatusText');
    if (statusText) {
        statusText.innerHTML = getStatusText();
    }
}

// Make functions available globally
window.initHMIPanel = initHMIPanel;
window.updateHMIFromBackend = updateHMIFromBackend;


