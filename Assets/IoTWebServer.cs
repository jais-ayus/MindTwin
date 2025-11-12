using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Web server that serves IoT component data via HTTP REST API
    /// Allows web dashboard to fetch real-time data
    /// </summary>
    public class IoTWebServer : MonoBehaviour
    {
        [Header("Server Settings")]
        [Tooltip("Port for the web server (default: 8080)")]
        public int ServerPort = 8080;
        
        [Tooltip("Enable CORS for web access")]
        public bool EnableCORS = true;
        
        [Tooltip("Auto-start server on scene start")]
        public bool AutoStartOnStart = true;
        
        [Header("References")]
        public IoTMonitor monitor;
        
        private HttpListener listener;
        private Thread serverThread;
        private bool isRunning = false;
        
        void Start()
        {
            if (monitor == null)
            {
                monitor = FindFirstObjectByType<IoTMonitor>();
            }
            
            if (AutoStartOnStart)
            {
                StartServer();
            }
        }
        
        void OnDestroy()
        {
            StopServer();
        }
        
        void OnApplicationQuit()
        {
            StopServer();
        }
        
        /// <summary>
        /// Starts the web server
        /// </summary>
        [ContextMenu("Start Web Server")]
        public void StartServer()
        {
            if (isRunning)
            {
                Debug.LogWarning("[IoTWebServer] Server is already running!");
                return;
            }
            
            try
            {
                listener = new HttpListener();
                string prefix = $"http://localhost:{ServerPort}/";
                listener.Prefixes.Add(prefix);
                listener.Start();
                
                isRunning = true;
                serverThread = new Thread(ListenForRequests);
                serverThread.IsBackground = true;
                serverThread.Start();
                
                Debug.Log($"[IoTWebServer] Server started on http://localhost:{ServerPort}/");
                Debug.Log($"[IoTWebServer] Open http://localhost:{ServerPort}/dashboard.html in your browser");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[IoTWebServer] Failed to start server: {e.Message}");
                isRunning = false;
            }
        }
        
        /// <summary>
        /// Stops the web server
        /// </summary>
        [ContextMenu("Stop Web Server")]
        public void StopServer()
        {
            if (!isRunning)
                return;
            
            isRunning = false;
            
            if (listener != null)
            {
                listener.Stop();
                listener.Close();
                listener = null;
            }
            
            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join(1000);
            }
            
            Debug.Log("[IoTWebServer] Server stopped");
        }
        
        private void ListenForRequests()
        {
            while (isRunning && listener != null)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
                catch (System.Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"[IoTWebServer] Error handling request: {e.Message}");
                    }
                }
            }
        }
        
        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            
            string path = request.Url.AbsolutePath;
            
            // Add CORS headers
            if (EnableCORS)
            {
                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            }
            
            // Handle OPTIONS request (CORS preflight)
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }
            
            try
            {
                if (path == "/" || path == "/dashboard.html")
                {
                    ServeDashboardHTML(response);
                }
                else if (path == "/api/components")
                {
                    ServeComponentsAPI(response);
                }
                else if (path == "/api/summary")
                {
                    ServeSummaryAPI(response);
                }
                else if (path.StartsWith("/dashboard.css"))
                {
                    ServeCSS(response);
                }
                else if (path.StartsWith("/dashboard.js"))
                {
                    ServeJavaScript(response);
                }
                else
                {
                    response.StatusCode = 404;
                    WriteResponse(response, "Not Found");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[IoTWebServer] Error serving request: {e.Message}");
                response.StatusCode = 500;
                WriteResponse(response, $"Error: {e.Message}");
            }
        }
        
        private void ServeDashboardHTML(HttpListenerResponse response)
        {
            response.ContentType = "text/html; charset=utf-8";
            string html = GetDashboardHTML();
            WriteResponse(response, html);
        }
        
        private void ServeCSS(HttpListenerResponse response)
        {
            response.ContentType = "text/css; charset=utf-8";
            string css = GetDashboardCSS();
            WriteResponse(response, css);
        }
        
        private void ServeJavaScript(HttpListenerResponse response)
        {
            response.ContentType = "application/javascript; charset=utf-8";
            string js = GetDashboardJavaScript();
            WriteResponse(response, js);
        }
        
        private void ServeComponentsAPI(HttpListenerResponse response)
        {
            response.ContentType = "application/json; charset=utf-8";
            
            if (monitor == null)
            {
                WriteResponse(response, "[]");
                return;
            }
            
            var components = monitor.GetAllComponents();
            var json = SerializeComponents(components);
            WriteResponse(response, json);
        }
        
        private void ServeSummaryAPI(HttpListenerResponse response)
        {
            response.ContentType = "application/json; charset=utf-8";
            
            if (monitor == null)
            {
                WriteResponse(response, "{}");
                return;
            }
            
            var components = monitor.GetAllComponents();
            var counts = monitor.GetComponentCounts();
            
            // Manually build JSON since JsonUtility doesn't handle dictionaries well
            var json = $@"{{
                ""total"": {components.Count},
                ""active"": {components.Count(c => c.IsActive)},
                ""counts"": {{
                    {string.Join(",", counts.Select(kvp => $"\"{EscapeJson(kvp.Key)}\": {kvp.Value}"))}
                }}
            }}";
            
            WriteResponse(response, json);
        }
        
        private string SerializeComponents(List<IoTComponentData> components)
        {
            var jsonList = new List<string>();
            
            foreach (var comp in components)
            {
                var json = $@"{{
                    ""name"": ""{EscapeJson(comp.ComponentName)}"",
                    ""type"": ""{EscapeJson(comp.ComponentType)}"",
                    ""active"": {comp.IsActive.ToString().ToLower()},
                    ""status"": ""{EscapeJson(comp.Status)}"",
                    ""value"": {comp.Value},
                    ""unit"": ""{EscapeJson(comp.Unit)}"",
                    ""color"": ""{ColorToHex(comp.StatusColor)}""
                }}";
                jsonList.Add(json);
            }
            
            return "[" + string.Join(",", jsonList) + "]";
        }
        
        private string ColorToHex(Color color)
        {
            return $"#{((int)(color.r * 255)).ToString("X2")}{((int)(color.g * 255)).ToString("X2")}{((int)(color.b * 255)).ToString("X2")}";
        }
        
        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        private void WriteResponse(HttpListenerResponse response, string content)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }
        
        private string GetDashboardHTML()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>IoT Insight Dashboard</title>
    <style>" + GetDashboardCSS() + @"</style>
</head>
<body>
    <div class=""dashboard-container"">
        <header class=""dashboard-header"">
            <h1>🔌 IoT Insight Dashboard</h1>
            <div class=""header-controls"">
                <button id=""refreshBtn"" class=""btn btn-primary"">Refresh</button>
                <label class=""toggle-label"">
                    <input type=""checkbox"" id=""autoRefresh"" checked>
                    <span>Auto Refresh</span>
                </label>
            </div>
        </header>
        
        <div class=""summary-bar"">
            <div id=""summaryText"">Loading...</div>
        </div>
        
        <div class=""components-container"" id=""componentsContainer"">
            <div class=""loading"">Loading components...</div>
        </div>
    </div>
    
    <script>" + GetDashboardJavaScript() + @"</script>
</body>
</html>";
        }
        
        private string GetDashboardCSS()
        {
            return @"
* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    min-height: 100vh;
    padding: 20px;
}

.dashboard-container {
    max-width: 1400px;
    margin: 0 auto;
    background: rgba(255, 255, 255, 0.95);
    border-radius: 12px;
    box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
    overflow: hidden;
}

.dashboard-header {
    background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
    color: white;
    padding: 20px 30px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: 15px;
}

.dashboard-header h1 {
    font-size: 28px;
    font-weight: 600;
}

.header-controls {
    display: flex;
    gap: 15px;
    align-items: center;
}

.btn {
    padding: 10px 20px;
    border: none;
    border-radius: 6px;
    cursor: pointer;
    font-size: 14px;
    font-weight: 500;
    transition: all 0.3s ease;
}

.btn-primary {
    background: #3498db;
    color: white;
}

.btn-primary:hover {
    background: #2980b9;
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
}

.toggle-label {
    display: flex;
    align-items: center;
    gap: 8px;
    color: white;
    font-size: 14px;
    cursor: pointer;
}

.toggle-label input[type=""checkbox""] {
    width: 18px;
    height: 18px;
    cursor: pointer;
}

.summary-bar {
    background: #ecf0f1;
    padding: 15px 30px;
    border-bottom: 2px solid #bdc3c7;
}

#summaryText {
    font-size: 14px;
    color: #2c3e50;
    font-weight: 500;
}

.components-container {
    padding: 20px;
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
    gap: 20px;
    max-height: calc(100vh - 200px);
    overflow-y: auto;
}

.component-card {
    background: white;
    border-radius: 8px;
    padding: 20px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    transition: all 0.3s ease;
    border-left: 4px solid #3498db;
}

.component-card:hover {
    transform: translateY(-4px);
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
}

.component-card.inactive {
    opacity: 0.6;
    border-left-color: #95a5a6;
}

.status-indicator {
    width: 12px;
    height: 12px;
    border-radius: 50%;
    display: inline-block;
    margin-right: 8px;
    vertical-align: middle;
}

.component-name {
    font-size: 18px;
    font-weight: 600;
    color: #2c3e50;
    margin-bottom: 8px;
}

.component-type {
    font-size: 12px;
    color: #7f8c8d;
    text-transform: uppercase;
    letter-spacing: 1px;
    margin-bottom: 12px;
}

.component-status {
    font-size: 14px;
    color: #34495e;
    margin-bottom: 8px;
}

.component-value {
    font-size: 16px;
    color: #3498db;
    font-weight: 600;
}

.loading {
    text-align: center;
    padding: 40px;
    color: #7f8c8d;
    font-size: 16px;
}

.empty-state {
    text-align: center;
    padding: 60px 20px;
    color: #7f8c8d;
}

.empty-state h2 {
    font-size: 24px;
    margin-bottom: 10px;
    color: #2c3e50;
}

.empty-state p {
    font-size: 16px;
}
";
        }
        
        private string GetDashboardJavaScript()
        {
            return @"
let autoRefresh = true;
let refreshInterval = null;

const API_BASE = window.location.origin;

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
    
    refreshData();
    if (autoRefresh) {
        startAutoRefresh();
    }
}

function startAutoRefresh() {
    if (refreshInterval) clearInterval(refreshInterval);
    refreshInterval = setInterval(refreshData, 1000); // Refresh every second
}

function stopAutoRefresh() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = null;
    }
}

async function refreshData() {
    try {
        const [components, summary] = await Promise.all([
            fetch(API_BASE + '/api/components').then(r => r.json()),
            fetch(API_BASE + '/api/summary').then(r => r.json())
        ]);
        
        updateSummary(summary);
        updateComponents(components);
    } catch (error) {
        console.error('Error fetching data:', error);
        document.getElementById('componentsContainer').innerHTML = 
            '<div class=""empty-state""><h2>Connection Error</h2><p>Unable to connect to Unity server. Make sure the server is running.</p></div>';
    }
}

function updateSummary(summary) {
    let text = `Total Components: ${summary.total} | Active: ${summary.active} | `;
    for (const [type, count] of Object.entries(summary.counts)) {
        text += `${type}: ${count} | `;
    }
    document.getElementById('summaryText').textContent = text.slice(0, -3);
}

function updateComponents(components) {
    const container = document.getElementById('componentsContainer');
    
    if (components.length === 0) {
        container.innerHTML = '<div class=""empty-state""><h2>No Components Found</h2><p>No IoT components detected in the scene.</p></div>';
        return;
    }
    
    container.innerHTML = components.map(comp => `
        <div class=""component-card ${comp.active ? '' : 'inactive'}"">
            <div class=""component-name"">
                <span class=""status-indicator"" style=""background-color: ${comp.color}""></span>
                ${escapeHtml(comp.name)}
            </div>
            <div class=""component-type"">${escapeHtml(comp.type)}</div>
            <div class=""component-status"">Status: <strong>${escapeHtml(comp.status)}</strong></div>
            <div class=""component-value"">${comp.value.toFixed(2)} ${escapeHtml(comp.unit)}</div>
        </div>
    `).join('');
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Initialize on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
} else {
    init();
}
";
        }
    }
}
