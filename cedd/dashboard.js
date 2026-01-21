// Configuration
const API_CONFIG = {
    baseUrl: 'http://localhost:3000',
    endpoints: {
        components: '/api/components',
        summary: '/api/summary',
        health: '/api/health'
    },
    pollInterval: 1000, // 1 second
    connectionTimeout: 5000
};

// State
let autoRefresh = true;
let refreshInterval = null;
let lastSuccessfulUpdate = null;
let connectionStatus = 'connecting'; // 'connecting', 'connected', 'disconnected'

// Initialize on page load
function init() {
    document.getElementById('refreshBtn').addEventListener('click', refreshData);
    document.getElementById('autoRefresh').addEventListener('change', (e) => {
        autoRefresh = e.target.checked;
        if (autoRefresh) {
            startAutoRefresh();
        } else {
            stopAutoRefresh();
        }
    });
    
    // Initialize category manager
    if (typeof initCategoryManager === 'function') {
        initCategoryManager();
    }
    
    // Initialize command handler
    if (typeof initCommandHandler === 'function') {
        initCommandHandler();
    }
    
    // Initial data fetch
    refreshData();
    if (autoRefresh) {
        startAutoRefresh();
    }
}

function startAutoRefresh() {
    if (refreshInterval) clearInterval(refreshInterval);
    refreshInterval = setInterval(refreshData, API_CONFIG.pollInterval);
}

function stopAutoRefresh() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = null;
    }
}

function updateConnectionStatus(status) {
    connectionStatus = status;
    const statusDot = document.getElementById('statusDot');
    const statusText = document.getElementById('statusText');
    
    statusDot.className = 'status-dot';
    
    switch (status) {
        case 'connected':
            statusDot.classList.add('connected');
            statusText.textContent = 'Connected';
            break;
        case 'disconnected':
            statusDot.classList.add('disconnected');
            statusText.textContent = 'Disconnected';
            break;
        case 'connecting':
        default:
            statusText.textContent = 'Connecting...';
            break;
    }
}

function updateLastUpdateTime(timestamp) {
    const lastUpdateEl = document.getElementById('lastUpdate');
    if (timestamp) {
        const date = new Date(timestamp);
        const now = new Date();
        const age = now - date;
        
        let ageText = '';
        if (age < 1000) {
            ageText = 'Just now';
        } else if (age < 60000) {
            ageText = `${Math.floor(age / 1000)}s ago`;
        } else if (age < 3600000) {
            ageText = `${Math.floor(age / 60000)}m ago`;
        } else {
            ageText = date.toLocaleTimeString();
        }
        
        lastUpdateEl.textContent = `Last update: ${ageText}`;
        lastSuccessfulUpdate = timestamp;
    } else {
        lastUpdateEl.textContent = '';
    }
}

async function refreshData() {
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), API_CONFIG.connectionTimeout);
        
        const [componentsResponse, summaryResponse] = await Promise.all([
            fetch(API_CONFIG.baseUrl + API_CONFIG.endpoints.components, {
                signal: controller.signal
            }),
            fetch(API_CONFIG.baseUrl + API_CONFIG.endpoints.summary, {
                signal: controller.signal
            })
        ]);
        
        clearTimeout(timeoutId);
        
        if (!componentsResponse.ok || !summaryResponse.ok) {
            throw new Error('API request failed');
        }
        
        const componentsData = await componentsResponse.json();
        const summaryData = await summaryResponse.json();
        
        // Update connection status
        updateConnectionStatus('connected');
        
        // Update UI
        updateSummary(summaryData);
        
        // Store all components for category filtering
        allComponents = componentsData.components || [];
        if (typeof allComponents !== 'undefined') {
            // Update category counts
            if (typeof loadCategoryCounts === 'function') {
                loadCategoryCounts();
            }
        }
        
        updateComponents(componentsData.components || []);
        updateLastUpdateTime(componentsData.timestamp || summaryData.lastUpdate);
        
    } catch (error) {
        console.error('Error fetching data:', error);
        updateConnectionStatus('disconnected');
        
        // Show error message if we have no data
        const container = document.getElementById('componentsContainer');
        if (container.children.length === 0 || 
            container.querySelector('.empty-state') || 
            container.querySelector('.loading')) {
            container.innerHTML = `
                <div class="empty-state">
                    <h2>Connection Error</h2>
                    <p>Unable to connect to WebGL build. Make sure:</p>
                    <ul style="text-align: left; display: inline-block; margin-top: 10px;">
                        <li>The backend API is running on port 3000</li>
                        <li>The WebGL build is running and sending data</li>
                        <li>Check browser console for details</li>
                    </ul>
                </div>
            `;
        }
        
        // Show stale data warning if we have old data
        if (lastSuccessfulUpdate) {
            const age = Date.now() - new Date(lastSuccessfulUpdate).getTime();
            if (age > 10000) { // 10 seconds
                updateLastUpdateTime(lastSuccessfulUpdate);
            }
        }
    }
}

function updateSummary(summary) {
    let text = `Total Components: ${summary.total || 0} | Active: ${summary.active || 0}`;
    
    if (summary.counts && Object.keys(summary.counts).length > 0) {
        text += ' | ';
        const counts = Object.entries(summary.counts)
            .map(([type, count]) => `${type}: ${count}`)
            .join(' | ');
        text += counts;
    }
    
    document.getElementById('summaryText').textContent = text;
}

// Component cache for selection
let componentCache = new Map();

function updateComponents(components) {
    const container = document.getElementById('componentsContainer');
    
    // Filter by current category if category manager is available
    let filteredComponents = components;
    if (typeof getFilteredComponents === 'function') {
        // Use category manager's filtered list
        filteredComponents = getFilteredComponents();
    } else if (components) {
        // Fallback: filter manually if category manager not loaded
        const currentCat = typeof getCurrentCategory === 'function' ? getCurrentCategory() : 'all';
        if (currentCat !== 'all' && typeof getCategoryFilter === 'function') {
            const filter = getCategoryFilter(currentCat);
            if (filter) {
                filteredComponents = components.filter(filter);
            }
        }
    }
    
    if (!filteredComponents || filteredComponents.length === 0) {
        container.innerHTML = `
            <div class="empty-state">
                <h2>No Components Found</h2>
                <p>No IoT components detected in this category. Make sure the WebGL build is running and sending data to the API.</p>
            </div>
        `;
        return;
    }
    
    // Cache components for selection
    componentCache.clear();
    filteredComponents.forEach(comp => {
        componentCache.set(comp.name, comp);
    });
    
    container.innerHTML = filteredComponents.map((comp) => {
        const value = typeof comp.value === 'number' ? comp.value.toFixed(2) : comp.value;
        const isSelected = selectedComponent && selectedComponent.name === comp.name;
        const metadata = comp.metadata || {};
        const offline = metadata.isOnline === false || metadata.manualOffline === true;
        const cardClasses = [
            'component-card',
            comp.active ? '' : 'inactive',
            isSelected ? 'selected' : '',
            offline ? 'offline' : ''
        ].filter(Boolean).join(' ');
        const statusFlag = offline ? '<span class="status-flag offline">Offline</span>' : '';

        return `
            <div class="${cardClasses}" onclick="selectComponentByName('${escapeHtml(comp.name)}')">
                <div class="component-name">
                    <span class="status-indicator" style="background-color: ${comp.color || '#3498db'}"></span>
                    ${escapeHtml(comp.name || 'Unknown')}
                </div>
                <div class="component-type">${escapeHtml(comp.type || 'Unknown')}</div>
                <div class="component-status">
                    Status: <strong>${escapeHtml(comp.status || 'Unknown')}</strong> ${statusFlag}
                </div>
                <div class="component-value">${value} ${escapeHtml(comp.unit || '')}</div>
            </div>
        `;
    }).join('');
}

// Select component by name (safer for onclick)
function selectComponentByName(componentName) {
    const component = componentCache.get(componentName);
    if (component) {
        selectComponent(component);
    }
}

// Select component for control
function selectComponent(component) {
    selectedComponent = component;
    
    // Update UI
    updateComponents(allComponents || []);
    
    // Render control panel
    if (typeof renderControlPanel === 'function') {
        renderControlPanel(component);
    }
    
    // Render status panel
    if (typeof renderStatusPanel === 'function') {
        renderStatusPanel(component);
    }
}

function escapeHtml(text) {
    if (text === null || text === undefined) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Make functions globally accessible
window.selectComponent = selectComponent;
window.selectComponentByName = selectComponentByName;

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
} else {
    init();
}






