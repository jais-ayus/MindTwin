const AI_PANEL_CONFIG = {
    baseUrl: (typeof API_CONFIG !== 'undefined' && API_CONFIG.baseUrl) ? API_CONFIG.baseUrl : 'http://localhost:3000',
    endpoints: {
        status: '/api/ai/status',
        anomalies: '/api/ai/anomalies',
        maintenance: '/api/ai/maintenance',
        optimization: '/api/ai/optimize',
        alerts: '/api/ai/alerts',
        dismissOptimization: '/api/ai/optimize/dismiss',
        applyOptimization: '/api/ai/optimize/apply'
    },
    pollInterval: 5000
};

let aiPanelInterval = null;
let latestOptimizationSuggestions = [];

function initAIPanel() {
    const panel = document.getElementById('aiPanel');
    if (!panel) return;

    refreshAIPanel();
    if (aiPanelInterval) clearInterval(aiPanelInterval);
    aiPanelInterval = setInterval(refreshAIPanel, AI_PANEL_CONFIG.pollInterval);
}

async function refreshAIPanel() {
    try {
        const [status, anomalies, maintenance, optimization, alerts] = await Promise.all([
            fetchAI('status'),
            fetchAI('anomalies'),
            fetchAI('maintenance'),
            fetchAI('optimization'),
            fetchAI('alerts')
        ]);

        updateAIStatus(status);
        renderAIAnomalies(anomalies?.data || []);
        renderAIMaintenance(maintenance?.data || []);
        renderAIOptimization(optimization || {});
        renderAIAlerts(alerts || {});
    } catch (error) {
        console.error('[AI Panel] Failed to refresh:', error);
        showAIPanelError(error?.message || 'Unable to reach AI service');
    }
}

function showAIPanelError(message) {
    const statusText = document.getElementById('aiStatusText');
    const statusSub = document.getElementById('aiStatusSubText');
    const dot = document.getElementById('aiStatusDot');
    if (statusText) statusText.textContent = 'AI service unavailable';
    if (statusSub) statusSub.textContent = message;
    if (dot) {
        dot.className = 'ai-status-dot offline';
    }
    renderAIAnomalies([]);
    renderAIMaintenance([]);
    renderAIOptimization({});
    renderAIAlerts({});
}

function updateAIStatus(statusResponse) {
    const dot = document.getElementById('aiStatusDot');
    const text = document.getElementById('aiStatusText');
    const subText = document.getElementById('aiStatusSubText');
    const meta = document.getElementById('aiStatusMeta');

    if (!statusResponse || !statusResponse.success) {
        showAIPanelError(statusResponse?.error || 'AI disabled');
        return;
    }

    const enabled = statusResponse.enabled !== false;
    const status = statusResponse.status || {};
    const pythonHealth = statusResponse.pythonService || {};

    if (!enabled) {
        if (dot) dot.className = 'ai-status-dot offline';
        if (text) text.textContent = 'AI disabled';
        if (subText) subText.textContent = 'Enable AI backend to view insights';
        if (meta) meta.textContent = '';
        return;
    }

    const healthy = pythonHealth.status === 'healthy' || status.healthy;
    if (dot) {
        dot.className = 'ai-status-dot ' + (healthy ? 'healthy' : 'degraded');
    }
    if (text) {
        text.textContent = healthy ? 'AI service online' : 'AI service degraded';
    }
    if (subText) {
        const lastCheck = status?.lastCheck ? formatRelativeTime(status.lastCheck) : 'unknown';
        subText.textContent = `Last check ${lastCheck}`;
    }
    if (meta) {
        meta.innerHTML = `
            <div>Python: ${pythonHealth.status || 'unknown'}</div>
            <div>Telemetry: ${statusResponse.telemetry?.components || 0} components</div>
            <div>Range alerts: ${statusResponse.rangeAlerts?.count || 0}</div>
        `;
    }
}

function renderAIAnomalies(anomalies) {
    const container = document.getElementById('aiAnomalyList');
    const countChip = document.getElementById('aiAnomalyCount');
    if (!container) return;

    if (countChip) {
        countChip.textContent = anomalies.length;
    }

    if (!anomalies.length) {
        container.innerHTML = '<div class="ai-empty">No anomalies detected</div>';
        return;
    }

    container.innerHTML = anomalies.slice(0, 5).map(anomaly => `
        <div class="ai-item">
            <div class="ai-item-header">
                <span>${escapeHtml(anomaly.componentId || 'Unknown')}</span>
                <span class="ai-pill ${escapeHtml(anomaly.severity || 'low')}">
                    ${escapeHtml(anomaly.severity || 'low')}
                </span>
            </div>
            <div class="ai-item-body">
                <div>Score: ${Number(anomaly.score || 0).toFixed(2)}</div>
                <div>${escapeHtml(anomaly.explanation || '')}</div>
                ${renderRecommendations(anomaly.recommendations)}
            </div>
        </div>
    `).join('');
}

function renderRecommendations(recommendations = []) {
    if (!recommendations.length) return '';
    return `
        <ul class="ai-recommendations">
            ${recommendations.map(r => `<li>${escapeHtml(r)}</li>`).join('')}
        </ul>
    `;
}

function renderAIMaintenance(predictions) {
    const container = document.getElementById('aiMaintenanceList');
    const countChip = document.getElementById('aiMaintenanceCount');
    if (!container) return;

    if (countChip) {
        countChip.textContent = predictions.length;
    }

    if (!predictions.length) {
        container.innerHTML = '<div class="ai-empty">No pending maintenance actions</div>';
        return;
    }

    container.innerHTML = predictions.slice(0, 4).map(pred => `
        <div class="ai-card">
            <div class="ai-card-title">${escapeHtml(pred.componentId || 'Component')}</div>
            <div class="ai-card-detail">TTF: ${Number(pred.timeToFailureHours || 0).toFixed(1)}h</div>
            <div class="ai-card-detail">Confidence: ${(Number(pred.confidence || 0) * 100).toFixed(0)}%</div>
            <div class="ai-card-detail">${escapeHtml(pred.recommendedAction || '')}</div>
        </div>
    `).join('');
}

function renderAIOptimization(optimization) {
    const container = document.getElementById('aiOptimizationList');
    const countChip = document.getElementById('aiOptimizationCount');
    if (!container) return;

    const suggestions = optimization.data || [];
    latestOptimizationSuggestions = suggestions;
    if (countChip) {
        countChip.textContent = suggestions.length;
    }

    if (!suggestions.length) {
        container.innerHTML = '<div class="ai-empty">No active suggestions</div>';
        return;
    }

    container.innerHTML = suggestions.slice(0, 4).map((suggestion, idx) => `
        <div class="ai-item">
            <div class="ai-item-header">
                <span>${escapeHtml(suggestion.componentId)}</span>
                <span class="ai-pill">${escapeHtml(suggestion.parameter)}</span>
            </div>
            <div class="ai-item-body">
                <div>Current: ${Number(suggestion.current).toFixed(2)}</div>
                <div>Recommended: ${Number(suggestion.recommended).toFixed(2)}</div>
                <div>${escapeHtml(suggestion.explanation || '')}</div>
                ${renderExpectedImpact(suggestion.expectedImpact)}
            </div>
            <div class="ai-actions">
                <button class="ai-btn primary" onclick="stageOptimizationSuggestion(${idx})">Stage</button>
                <button class="ai-btn secondary" onclick="dismissOptimizationSuggestion('${suggestion.componentId}','${suggestion.parameter}')">Dismiss</button>
            </div>
        </div>
    `).join('');
}

function renderExpectedImpact(impact = {}) {
    if (!impact || !Object.keys(impact).length) return '';
    return `
        <div class="ai-impact">
            ${Object.entries(impact).map(([key, value]) => {
                if (key === 'horizonMinutes') return '';
                const val = typeof value === 'number' ? value.toFixed(2) : value;
                return `<span>${escapeHtml(key)}: ${escapeHtml(val)}</span>`;
            }).join('')}
        </div>
    `;
}

function renderAIAlerts(payload = {}) {
    renderOfflineAlerts(payload.offline || {});
    renderParameterWarnings(payload.parameter || {});
}

function renderOfflineAlerts(offline) {
    const container = document.getElementById('aiOfflineList');
    const chip = document.getElementById('aiOfflineCount');
    if (!container) return;
    const alerts = Array.isArray(offline.alerts) ? offline.alerts : [];
    if (chip) chip.textContent = alerts.length;

    if (!alerts.length) {
        container.innerHTML = '<div class="ai-empty">No emergency alerts</div>';
        return;
    }

    container.innerHTML = alerts.slice(0, 4).map(alert => `
        <div class="ai-item">
            <div class="ai-item-header">
                <span>${escapeHtml(alert.componentId || 'Component')}</span>
                <span class="ai-pill ${escapeHtml(alert.severity || 'warning')}">
                    ${escapeHtml(alert.severity || 'warning')}
                </span>
            </div>
            <div class="ai-item-body">
                <div>${escapeHtml(alert.reason || '')}</div>
                <div>Gap: ${Number(alert.gapSeconds || 0).toFixed(1)}s · Likelihood ${(Number(alert.likelihood || 0) * 100).toFixed(0)}%</div>
                <div>${escapeHtml(alert.recommendation || '')}</div>
            </div>
            ${alert.autoStopRecommended ? `
                <div class="ai-actions">
                    <button class="ai-btn danger" data-component="${escapeHtml(alert.componentId || '')}" onclick="acknowledgeAiEmergency(this.dataset.component)">
                        Emergency Stop
                    </button>
                </div>
            ` : ''}
        </div>
    `).join('');
}

function renderParameterWarnings(parameter) {
    const container = document.getElementById('aiRiskList');
    const chip = document.getElementById('aiRiskCount');
    if (!container) return;
    const warnings = Array.isArray(parameter.warnings) ? parameter.warnings : [];
    if (chip) chip.textContent = warnings.length;

    if (!warnings.length) {
        container.innerHTML = '<div class="ai-empty">No risky parameters</div>';
        return;
    }

    container.innerHTML = warnings.slice(0, 4).map(warn => `
        <div class="ai-item">
            <div class="ai-item-header">
                <span>${escapeHtml(warn.componentId || 'Component')}</span>
                <span class="ai-pill ${escapeHtml(warn.risk || 'medium')}">
                    ${escapeHtml(warn.parameter || 'Parameter')}
                </span>
            </div>
            <div class="ai-item-body">
                <div>Value: ${escapeHtml(String(warn.value))} (default ${warn.defaultValue ?? 'n/a'})</div>
                <div>Wear x${Number(warn.wearMultiplier || 1).toFixed(2)} · RUL ~ ${Number(warn.estimatedRULHours || 0).toFixed(1)}h</div>
                ${renderRecommendations(warn.notes)}
                ${renderRecommendations(warn.suggestions)}
            </div>
        </div>
    `).join('');
}

function acknowledgeAiEmergency(componentId) {
    if (typeof emergencyStop !== 'function') {
        console.warn('Emergency stop function unavailable');
        return;
    }
    const name = componentId || 'component';
    if (!confirm(`AI recommends emergency stop for ${name}. Proceed?`)) {
        return;
    }
    emergencyStop(`AI detected offline state for ${name}`, 'AI');
}

async function dismissOptimizationSuggestion(componentId, parameter) {
    try {
        await fetch(AI_PANEL_CONFIG.baseUrl + AI_PANEL_CONFIG.endpoints.dismissOptimization, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ componentId, parameter })
        });
        refreshAIPanel();
    } catch (error) {
        console.error('[AI Panel] Failed to dismiss suggestion:', error);
    }
}

function stageOptimizationSuggestion(index) {
    try {
        const suggestion = latestOptimizationSuggestions[index];
        if (!suggestion || !suggestion.componentId) return;

        if (typeof selectComponentByName === 'function') {
            selectComponentByName(suggestion.componentId);
        }

        const component = typeof selectedComponent !== 'undefined' ? selectedComponent : null;
        if (!component || component.name !== suggestion.componentId) {
            console.warn('[AI Panel] Unable to stage change: component not selected');
            return;
        }

        if (typeof stagePendingChange === 'function') {
            stagePendingChange(component, {
                id: `AI_${suggestion.componentId}_${suggestion.parameter}`,
                label: `AI: ${suggestion.parameter}`,
                value: suggestion.recommended,
                displayValue: `${suggestion.recommended} (AI)`,
                commands: [
                    {
                        parameter: suggestion.parameter,
                        value: suggestion.recommended
                    }
                ]
            });
        }
    } catch (error) {
        console.error('[AI Panel] Failed to stage suggestion:', error);
    }
}

async function fetchAI(key) {
    const endpoint = AI_PANEL_CONFIG.endpoints[key];
    if (!endpoint) throw new Error(`Unknown AI endpoint: ${key}`);
    const response = await fetch(AI_PANEL_CONFIG.baseUrl + endpoint, { cache: 'no-store' });
    if (!response.ok) {
        throw new Error(`AI endpoint ${endpoint} failed: ${response.status}`);
    }
    return response.json();
}

function formatRelativeTime(timestamp) {
    const time = typeof timestamp === 'number' ? timestamp : new Date(timestamp).getTime();
    if (!time) return 'unknown';
    const diff = Date.now() - time;
    if (diff < 5000) return 'just now';
    if (diff < 60000) return `${Math.floor(diff / 1000)}s ago`;
    if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`;
    return `${Math.floor(diff / 3600000)}h ago`;
}

window.initAIPanel = initAIPanel;
window.dismissOptimizationSuggestion = dismissOptimizationSuggestion;
window.stageOptimizationSuggestion = stageOptimizationSuggestion;
window.acknowledgeAiEmergency = acknowledgeAiEmergency;

