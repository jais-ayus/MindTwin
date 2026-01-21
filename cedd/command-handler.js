// Command Handler - Handles sending commands to backend and Unity

let selectedComponent = null;

// Send control command to component (real-time)
async function sendCommand(componentId, parameter, value) {
    try {
        const url = `${API_CONFIG.baseUrl}/api/components/${encodeURIComponent(componentId)}/control`;
        
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                parameter: parameter,
                value: value,
                userId: 'dashboard_user',
                timestamp: new Date().toISOString()
            })
        });
        
        const result = await response.json();
        
        if (!response.ok || !result.success) {
            const errorMsg = result?.error?.message || result?.error || `HTTP ${response.status}`;
            throw new Error(errorMsg);
        }
        
        console.log(`[Command] Sent: ${componentId}.${parameter} = ${value} (ID: ${result.commandId})`);
        
            setTimeout(() => {
                if (typeof refreshData === 'function') {
                    refreshData();
                }
            }, 500);
            
            return { success: true, commandId: result.commandId };
    } catch (error) {
        console.error('[Command] Error:', error);
        // Show user-friendly error message
        const errorMsg = error.message || 'Failed to send command';
        alert(`Command Error: ${errorMsg}`);
        return { success: false, error: errorMsg };
    }
}

// Emergency stop
async function emergencyStop(reason = 'Manual emergency stop', category = null) {
    try {
        const url = `${API_CONFIG.baseUrl}/api/emergency/stop`;
        
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                reason: reason,
                category: category
            })
        });
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        
        const result = await response.json();
        
        if (result.success) {
            updateProductionStatus('HALTED');
            toggleEmergencyButtons(true); // Show resume, hide emergency stop
            alert('Emergency stop activated!');
            
            // Verify stop by checking status after a delay
            setTimeout(async () => {
                await checkEmergencyStatus();
            }, 1000);
            
            return true;
        } else {
            throw new Error('Emergency stop failed');
        }
    } catch (error) {
        console.error('Error activating emergency stop:', error);
        alert(`Error: ${error.message}`);
        return false;
    }
}

// Resume production
async function resumeProduction() {
    try {
        const url = `${API_CONFIG.baseUrl}/api/emergency/resume`;
        
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                acknowledgedBy: 'dashboard_user'
            })
        });
        
        if (!response.ok) {
            const errorData = await response.json();
            if (response.status === 409) {
                throw new Error(errorData.error || 'Cannot resume: Validation failed');
            }
            throw new Error(`HTTP ${response.status}`);
        }
        
        const result = await response.json();
        
        if (result.success) {
            updateProductionStatus('RUNNING');
            toggleEmergencyButtons(false); // Show emergency stop, hide resume
            alert('Production resumed!');
            return true;
        } else {
            throw new Error('Resume failed');
        }
    } catch (error) {
        console.error('Error resuming production:', error);
        alert(`Error: ${error.message}`);
        return false;
    }
}

// Toggle emergency stop and resume buttons
function toggleEmergencyButtons(isHalted) {
    const emergencyBtn = document.getElementById('emergencyStopBtn');
    const resumeBtn = document.getElementById('resumeBtn');
    
    if (emergencyBtn && resumeBtn) {
        if (isHalted) {
            emergencyBtn.style.display = 'none';
            resumeBtn.style.display = 'inline-block';
        } else {
            emergencyBtn.style.display = 'inline-block';
            resumeBtn.style.display = 'none';
        }
    }
}

// Update production status display
function updateProductionStatus(status) {
    const statusEl = document.getElementById('productionStatus');
    if (statusEl) {
        statusEl.textContent = `Production: ${status}`;
        if (status === 'HALTED') {
            statusEl.classList.add('halted');
        } else {
            statusEl.classList.remove('halted');
        }
    }
}

// Check emergency stop status
async function checkEmergencyStatus() {
    try {
        const response = await fetch(`${API_CONFIG.baseUrl}/api/emergency/status`);
        if (response.ok) {
            const data = await response.json();
            if (data.success) {
                if (data.active) {
                    updateProductionStatus('HALTED');
                    toggleEmergencyButtons(true);
                } else {
                    updateProductionStatus('RUNNING');
                    toggleEmergencyButtons(false);
                }
            }
        }
    } catch (error) {
        console.error('Error checking emergency status:', error);
    }
}

// Initialize command handler
function initCommandHandler() {
    // Setup emergency stop button
    const emergencyBtn = document.getElementById('emergencyStopBtn');
    if (emergencyBtn) {
        emergencyBtn.addEventListener('click', () => {
            if (confirm('Are you sure you want to activate emergency stop? This will halt all production immediately.')) {
                emergencyStop();
            }
        });
    }
    
    // Setup resume button
    const resumeBtn = document.getElementById('resumeBtn');
    if (resumeBtn) {
        resumeBtn.addEventListener('click', () => {
            if (confirm('Resume production? Make sure all parameters are in safe range.')) {
                resumeProduction();
            }
        });
    }
    
    // Check emergency status on load
    checkEmergencyStatus();
    
    // Check emergency status periodically (every 2 seconds for real-time updates)
    setInterval(checkEmergencyStatus, 2000);
}

