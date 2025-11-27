using UnityEngine;
using IoTDashboard;

namespace IoTDashboard
{
    /// <summary>
    /// Setup script to easily add IoT Web Server to DemoRealVirtual scene
    /// Just add this component to any GameObject in the scene
    /// </summary>
    public class IoTWebServerSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [Tooltip("Automatically create web server on Start")]
        public bool AutoSetupOnStart = true;
        
        [Header("Server Settings")]
        [Tooltip("Port for the web server (default: 8080)")]
        public int ServerPort = 8080;
        
        [Tooltip("Update interval for monitoring (seconds)")]
        public float MonitorUpdateInterval = 0.1f;
        
        private IoTMonitor monitor;
        private IoTWebServer webServer;
        
        void Start()
        {
            if (AutoSetupOnStart)
            {
                SetupWebServer();
            }
        }
        
        /// <summary>
        /// Sets up the complete IoT Web Server system
        /// </summary>
        [ContextMenu("Setup IoT Web Server")]
        public void SetupWebServer()
        {
            // Create Monitor
            if (monitor == null)
            {
                GameObject monitorObj = new GameObject("IoT Monitor");
                monitor = monitorObj.AddComponent<IoTMonitor>();
                monitor.UpdateInterval = MonitorUpdateInterval;
                monitor.AutoDiscoverOnStart = true;
            }
            
            // Create Web Server
            if (webServer == null)
            {
                GameObject serverObj = new GameObject("IoT Web Server");
                webServer = serverObj.AddComponent<IoTWebServer>();
                webServer.monitor = monitor;
                webServer.ServerPort = ServerPort;
                webServer.AutoStartOnStart = true;
            }
            
            Debug.Log($"[IoTWebServerSetup] IoT Web Server system initialized!");
            Debug.Log($"[IoTWebServerSetup] Open http://localhost:{ServerPort}/dashboard.html in your browser");
            Debug.Log($"[IoTWebServerSetup] The server will automatically discover and monitor all IoT components.");
        }
        
        /// <summary>
        /// Removes the web server system
        /// </summary>
        [ContextMenu("Remove IoT Web Server")]
        public void RemoveWebServer()
        {
            if (webServer != null)
            {
                webServer.StopServer();
                Destroy(webServer.gameObject);
                webServer = null;
            }
            
            if (monitor != null)
            {
                Destroy(monitor.gameObject);
                monitor = null;
            }
            
            Debug.Log("[IoTWebServerSetup] IoT Web Server system removed.");
        }
    }
}









