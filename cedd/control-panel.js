// Control Panel - Renders control UI for selected components

// Render control panel for selected component
function renderControlPanel(component) {
    const controlContent = document.getElementById('controlContent');
    if (!controlContent) return;
    
    if (!component) {
        controlContent.innerHTML = '<div class="no-selection">Select a component to control</div>';
        return;
    }
    
    let html = `<div class="control-group">
        <h3>${escapeHtml(component.name)}</h3>
        <div class="component-type">${escapeHtml(component.type)}</div>
    </div>`;
    
    // ALWAYS add Enable/Disable control for ALL component types
    const isEnabled = component.active !== false;
    html += `
        <div class="control-group" style="border-bottom: 2px solid #ecf0f1; padding-bottom: 15px; margin-bottom: 15px;">
            <label>Component State</label>
            <div class="control-buttons">
                <button class="btn-toggle ${isEnabled ? 'active' : ''}" id="component-enable">
                    Enable
                </button>
                <button class="btn-toggle ${!isEnabled ? 'active' : ''}" id="component-disable">
                    Disable
                </button>
            </div>
        </div>
    `;
    
    // Render controls based on component type
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
            html += '<div class="no-selection">No specific controls available for this component type</div>';
    }
    
    controlContent.innerHTML = html;
    
    // Attach event listeners
    attachControlListeners(component);
}

// Render Drive controls
function renderDriveControls(component) {
    return `
        <div class="control-group">
            <label>Target Speed (mm/s)</label>
            <input type="range" id="drive-speed" min="0" max="200" value="${component.value || 0}" step="1">
            <input type="number" id="drive-speed-input" min="0" max="200" value="${component.value || 0}" step="1">
            <div class="value-display">Current: ${component.value || 0} mm/s</div>
        </div>
        <div class="control-group">
            <label>Direction</label>
            <div class="control-buttons">
                <button class="btn-toggle ${component.status === 'Running' ? 'active' : ''}" id="drive-forward">
                    Forward
                </button>
                <button class="btn-toggle" id="drive-backward">
                    Backward
                </button>
                <button class="btn-action" id="drive-stop">
                    Stop
                </button>
            </div>
        </div>
    `;
}

// Render Sensor controls
function renderSensorControls(component) {
    return `
        <div class="control-group">
            <label>Display Status</label>
            <button class="btn-toggle active" id="sensor-display">
                ${component.status === 'Occupied' ? 'ON' : 'OFF'}
            </button>
        </div>
        <div class="control-group">
            <label>Filter by Tag</label>
            <input type="text" id="sensor-tag" placeholder="Enter tag name" maxlength="50">
            <button class="btn-action" id="sensor-tag-apply" style="margin-top: 10px; width: 100%;">
                Apply Tag Filter
            </button>
        </div>
    `;
}

// Render Lamp controls
function renderLampControls(component) {
    const isOn = component.status && component.status.includes('ON');
    const isFlashing = component.status && component.status.includes('Flashing');
    
    return `
        <div class="control-group">
            <label>Lamp State</label>
            <div class="control-buttons">
                <button class="btn-toggle ${isOn ? 'active' : ''}" id="lamp-on">
                    ON
                </button>
                <button class="btn-toggle ${!isOn ? 'active' : ''}" id="lamp-off">
                    OFF
                </button>
            </div>
        </div>
        <div class="control-group">
            <label>Flashing Mode</label>
            <button class="btn-toggle ${isFlashing ? 'active' : ''}" id="lamp-flashing">
                ${isFlashing ? 'Disable Flashing' : 'Enable Flashing'}
            </button>
        </div>
    `;
}

// Render Source controls
function renderSourceControls(component) {
    const isEnabled = component.status === 'Active';
    
    return `
        <div class="control-group">
            <label>Source State</label>
            <div class="control-buttons">
                <button class="btn-toggle ${isEnabled ? 'active' : ''}" id="source-enable">
                    Enable
                </button>
                <button class="btn-toggle ${!isEnabled ? 'active' : ''}" id="source-disable">
                    Disable
                </button>
            </div>
        </div>
    `;
}

// Render Grip controls
function renderGripControls(component) {
    return `
        <div class="control-group">
            <label>Grip Actions</label>
            <div class="control-buttons">
                <button class="btn-action" id="grip-pick">
                    Pick Objects
                </button>
                <button class="btn-action" id="grip-place">
                    Place Objects
                </button>
            </div>
            <div style="margin-top: 10px; color: #7f8c8d; font-size: 12px;">
                Currently holding: ${component.value || 0} objects
            </div>
        </div>
    `;
}

// Render Axis controls
function renderAxisControls(component) {
    return `
        <div class="control-group">
            <label>Target Position (mm)</label>
            <input type="number" id="axis-position" min="-1000" max="1000" value="${component.value || 0}" step="0.1">
            <div class="value-display">Current: ${component.value || 0} mm</div>
        </div>
        <div class="control-group">
            <button class="btn-action" id="axis-move" style="width: 100%;">
                Move to Position
            </button>
        </div>
    `;
}

// Render Sink controls
function renderSinkControls(component) {
    return `
        <div class="control-group">
            <p style="color: #7f8c8d; font-size: 12px;">Sink components receive material units. No additional controls available.</p>
        </div>
    `;
}

// Attach event listeners to controls
function attachControlListeners(component) {
    if (!component) return;
    
    // ALWAYS attach enable/disable listeners for ALL component types
    const enableBtn = document.getElementById('component-enable');
    const disableBtn = document.getElementById('component-disable');
    
    if (enableBtn) {
        enableBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'Enabled', true);
            enableBtn.classList.add('active');
            if (disableBtn) disableBtn.classList.remove('active');
        });
    }
    
    if (disableBtn) {
        disableBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'Enabled', false);
            disableBtn.classList.add('active');
            if (enableBtn) enableBtn.classList.remove('active');
        });
    }
    
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
        case 'Sink':
            // Sink has no specific listeners, only enable/disable
            break;
    }
}

// Drive listeners
function attachDriveListeners(component) {
    const speedSlider = document.getElementById('drive-speed');
    const speedInput = document.getElementById('drive-speed-input');
    const forwardBtn = document.getElementById('drive-forward');
    const backwardBtn = document.getElementById('drive-backward');
    const stopBtn = document.getElementById('drive-stop');
    
    if (speedSlider && speedInput) {
        speedSlider.addEventListener('input', (e) => {
            speedInput.value = e.target.value;
        });
        
        speedInput.addEventListener('input', (e) => {
            speedSlider.value = e.target.value;
        });
        
        speedInput.addEventListener('change', async (e) => {
            const value = parseFloat(e.target.value);
            if (!isNaN(value)) {
                await sendCommand(component.name, 'TargetSpeed', value);
            }
        });
    }
    
    if (forwardBtn) {
        forwardBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'JogForward', true);
            forwardBtn.classList.add('active');
            if (backwardBtn) backwardBtn.classList.remove('active');
        });
    }
    
    if (backwardBtn) {
        backwardBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'JogBackward', true);
            backwardBtn.classList.add('active');
            if (forwardBtn) forwardBtn.classList.remove('active');
        });
    }
    
    if (stopBtn) {
        stopBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'JogForward', false);
            await sendCommand(component.name, 'JogBackward', false);
            if (forwardBtn) forwardBtn.classList.remove('active');
            if (backwardBtn) backwardBtn.classList.remove('active');
        });
    }
}

// Sensor listeners
function attachSensorListeners(component) {
    const tagInput = document.getElementById('sensor-tag');
    const tagApplyBtn = document.getElementById('sensor-tag-apply');
    
    if (tagApplyBtn && tagInput) {
        tagApplyBtn.addEventListener('click', async () => {
            const tag = tagInput.value.trim();
            await sendCommand(component.name, 'LimitSensorToTag', tag);
        });
    }
}

// Lamp listeners
function attachLampListeners(component) {
    const onBtn = document.getElementById('lamp-on');
    const offBtn = document.getElementById('lamp-off');
    const flashingBtn = document.getElementById('lamp-flashing');
    
    if (onBtn) {
        onBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'LampOn', true);
            onBtn.classList.add('active');
            if (offBtn) offBtn.classList.remove('active');
        });
    }
    
    if (offBtn) {
        offBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'LampOn', false);
            offBtn.classList.add('active');
            if (onBtn) onBtn.classList.remove('active');
        });
    }
    
    if (flashingBtn) {
        flashingBtn.addEventListener('click', async () => {
            const isFlashing = flashingBtn.classList.contains('active');
            await sendCommand(component.name, 'Flashing', !isFlashing);
            flashingBtn.classList.toggle('active');
            flashingBtn.textContent = !isFlashing ? 'Disable Flashing' : 'Enable Flashing';
        });
    }
}

// Source listeners
function attachSourceListeners(component) {
    const enableBtn = document.getElementById('source-enable');
    const disableBtn = document.getElementById('source-disable');
    
    if (enableBtn) {
        enableBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'Enabled', true);
            enableBtn.classList.add('active');
            if (disableBtn) disableBtn.classList.remove('active');
        });
    }
    
    if (disableBtn) {
        disableBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'Enabled', false);
            disableBtn.classList.add('active');
            if (enableBtn) enableBtn.classList.remove('active');
        });
    }
}

// Grip listeners
function attachGripListeners(component) {
    const pickBtn = document.getElementById('grip-pick');
    const placeBtn = document.getElementById('grip-place');
    
    if (pickBtn) {
        pickBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'PickObjects', true);
            // Auto-reset after a delay
            setTimeout(async () => {
                await sendCommand(component.name, 'PickObjects', false);
            }, 1000);
        });
    }
    
    if (placeBtn) {
        placeBtn.addEventListener('click', async () => {
            await sendCommand(component.name, 'PlaceObjects', true);
            // Auto-reset after a delay
            setTimeout(async () => {
                await sendCommand(component.name, 'PlaceObjects', false);
            }, 1000);
        });
    }
}

// Axis listeners
function attachAxisListeners(component) {
    const positionInput = document.getElementById('axis-position');
    const moveBtn = document.getElementById('axis-move');
    
    if (moveBtn && positionInput) {
        moveBtn.addEventListener('click', async () => {
            const position = parseFloat(positionInput.value);
            if (!isNaN(position)) {
                await sendCommand(component.name, 'TargetPosition', position);
                await sendCommand(component.name, 'TargetStartMove', true);
                // Auto-reset after a delay
                setTimeout(async () => {
                    await sendCommand(component.name, 'TargetStartMove', false);
                }, 500);
            }
        });
    }
}

// Render status panel
function renderStatusPanel(component) {
    const statusContent = document.getElementById('statusContent');
    if (!statusContent) return;
    
    if (!component) {
        statusContent.innerHTML = '<div class="no-selection">Select a component for details</div>';
        return;
    }
    
    const html = `
        <div class="status-details">
            <div class="detail-row">
                <span class="detail-label">Name:</span>
                <span class="detail-value">${escapeHtml(component.name)}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">Type:</span>
                <span class="detail-value">${escapeHtml(component.type)}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">Category:</span>
                <span class="detail-value">${escapeHtml(component.category || 'other')}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">Status:</span>
                <span class="detail-value">${escapeHtml(component.status || 'Unknown')}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">Value:</span>
                <span class="detail-value">${component.value !== undefined ? component.value.toFixed(2) : 'N/A'} ${escapeHtml(component.unit || '')}</span>
            </div>
            <div class="detail-row">
                <span class="detail-label">Active:</span>
                <span class="detail-value">${component.active ? 'Yes' : 'No'}</span>
            </div>
        </div>
    `;
    
    statusContent.innerHTML = html;
}


