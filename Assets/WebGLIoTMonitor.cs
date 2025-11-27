using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Monitors IoT components in WebGL build and sends data to backend API
    /// Similar to IoTMonitor but designed for WebGL builds
    /// </summary>
    public class WebGLIoTMonitor : MonoBehaviour
    {
        [Header("Monitoring Settings")]
        [Tooltip("Update interval in seconds")]
        public float UpdateInterval = 0.1f;
        
        [Header("Component Discovery")]
        public bool AutoDiscoverOnStart = true;
        public bool MonitorSensors = true;
        public bool MonitorDrives = true;
        public bool MonitorAxes = true;
        public bool MonitorGrips = true;
        public bool MonitorLamps = true;
        public bool MonitorSources = true;
        public bool MonitorSinks = true;
        
        [Header("API Settings")]
        [Tooltip("Backend API base URL")]
        public string ApiBaseUrl = "http://localhost:3000";
        
        [Tooltip("Send data interval in seconds")]
        public float SendInterval = 0.5f;
        
        // Discovered components
        private List<IoTComponentData> allComponents = new List<IoTComponentData>();
        private Dictionary<GameObject, IoTComponentData> componentMap = new Dictionary<GameObject, IoTComponentData>();
        
        private float lastUpdateTime;
        private float lastSendTime;
        private WebGLDataSender dataSender;
        
        void Start()
        {
            // Get or create data sender
            dataSender = GetComponent<WebGLDataSender>();
            if (dataSender == null)
            {
                dataSender = gameObject.AddComponent<WebGLDataSender>();
            }
            
            dataSender.ApiBaseUrl = ApiBaseUrl;
            
            if (AutoDiscoverOnStart)
            {
                DiscoverAllComponents();
            }
        }
        
        void Update()
        {
            // Update component data
            if (Time.time - lastUpdateTime >= UpdateInterval)
            {
                UpdateAllComponents();
                lastUpdateTime = Time.time;
            }
            
            // Send data to API
            if (Time.time - lastSendTime >= SendInterval)
            {
                SendDataToAPI();
                lastSendTime = Time.time;
            }
        }
        
        /// <summary>
        /// Discovers all IoT components in the scene
        /// </summary>
        public void DiscoverAllComponents()
        {
            try
            {
                allComponents.Clear();
                componentMap.Clear();
                
                if (MonitorSensors)
                {
                    try
                    {
                        var sensors = FindObjectsByType<Sensor>(FindObjectsSortMode.None);
                        if (sensors != null)
                        {
                            for (int i = 0; i < sensors.Length; i++)
                            {
                                var sensor = sensors[i];
                                if (sensor != null && sensor.gameObject != null)
                                {
                                    AddComponent(sensor.gameObject, "Sensor", sensor);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering sensors: {ex.Message}");
                    }
                }
                
                if (MonitorDrives)
                {
                    try
                    {
                        var drives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                        if (drives != null)
                        {
                            for (int i = 0; i < drives.Length; i++)
                            {
                                var drive = drives[i];
                                if (drive != null && drive.gameObject != null)
                                {
                                    AddComponent(drive.gameObject, "Drive", drive);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering drives: {ex.Message}");
                    }
                }
                
                if (MonitorAxes)
                {
                    try
                    {
                        var axes = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                        if (axes != null)
                        {
                            for (int i = 0; i < axes.Length; i++)
                            {
                                var axis = axes[i];
                                if (axis != null && axis.gameObject != null)
                                {
                                    AddComponent(axis.gameObject, "Axis", axis);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering axes: {ex.Message}");
                    }
                }
                
                if (MonitorGrips)
                {
                    try
                    {
                        var grips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                        if (grips != null)
                        {
                            for (int i = 0; i < grips.Length; i++)
                            {
                                var grip = grips[i];
                                if (grip != null && grip.gameObject != null)
                                {
                                    AddComponent(grip.gameObject, "Grip", grip);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering grips: {ex.Message}");
                    }
                }
                
                if (MonitorLamps)
                {
                    try
                    {
                        var lamps = FindObjectsByType<Lamp>(FindObjectsSortMode.None);
                        if (lamps != null)
                        {
                            for (int i = 0; i < lamps.Length; i++)
                            {
                                var lamp = lamps[i];
                                if (lamp != null && lamp.gameObject != null)
                                {
                                    AddComponent(lamp.gameObject, "Lamp", lamp);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering lamps: {ex.Message}");
                    }
                }
                
                if (MonitorSources)
                {
                    try
                    {
                        var sources = FindObjectsByType<Source>(FindObjectsSortMode.None);
                        if (sources != null)
                        {
                            for (int i = 0; i < sources.Length; i++)
                            {
                                var source = sources[i];
                                if (source != null && source.gameObject != null)
                                {
                                    AddComponent(source.gameObject, "Source", source);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering sources: {ex.Message}");
                    }
                }
                
                if (MonitorSinks)
                {
                    try
                    {
                        var sinks = FindObjectsByType<Sink>(FindObjectsSortMode.None);
                        if (sinks != null)
                        {
                            for (int i = 0; i < sinks.Length; i++)
                            {
                                var sink = sinks[i];
                                if (sink != null && sink.gameObject != null)
                                {
                                    AddComponent(sink.gameObject, "Sink", sink);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLIoTMonitor] Error discovering sinks: {ex.Message}");
                    }
                }
                
                Debug.Log($"[WebGLIoTMonitor] Discovered {allComponents.Count} IoT components");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLIoTMonitor] CRITICAL ERROR in DiscoverAllComponents: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void AddComponent(GameObject obj, string type, Component component)
        {
            // Null checks
            if (obj == null)
            {
                Debug.LogWarning($"[WebGLIoTMonitor] Cannot add component - GameObject is null (type: {type})");
                return;
            }
            
            if (component == null)
            {
                Debug.LogWarning($"[WebGLIoTMonitor] Cannot add component - Component is null (GameObject: {obj.name})");
                return;
            }
            
            // Check if already added
            if (componentMap.ContainsKey(obj))
                return;
            
            try
            {
                var data = new IoTComponentData
                {
                    ComponentName = obj.name != null ? obj.name : "Unknown",
                    ComponentType = type,
                    ComponentObject = obj,
                    IsActive = obj.activeInHierarchy
                };
                
                // Store component reference
                switch (type)
                {
                    case "Sensor":
                        data.Sensor = component as Sensor;
                        break;
                    case "Drive":
                        data.Drive = component as Drive;
                        break;
                    case "Axis":
                        data.Axis = component as Axis;
                        break;
                    case "Grip":
                        data.Grip = component as Grip;
                        break;
                    case "Lamp":
                        data.Lamp = component as Lamp;
                        break;
                    case "Source":
                        data.Source = component as Source;
                        break;
                    case "Sink":
                        data.Sink = component as Sink;
                        break;
                }
                
                // Determine category (Phase 1)
                try
                {
                    data.Category = DetermineCategory(type, obj.name, component);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[WebGLIoTMonitor] Error determining category: {ex.Message}");
                    data.Category = "other";
                }
                
                // Check for TransportSurface association (for conveyors)
                if (type == "Drive")
                {
                    try
                    {
                        data.HasTransportSurface = obj.GetComponent<TransportSurface>() != null ||
                                                   obj.GetComponentInChildren<TransportSurface>() != null ||
                                                   obj.GetComponentInParent<TransportSurface>() != null;
                    }
                    catch
                    {
                        data.HasTransportSurface = false;
                    }
                }
                
                // Check for robot association
                if (type == "Axis" || type == "Grip")
                {
                    try
                    {
                        GameObject robotParent = FindParentRobot(obj);
                        if (robotParent != null && robotParent.name != null)
                        {
                            data.ParentRobot = robotParent.name;
                            data.IsRobotAxis = (type == "Axis");
                            data.IsRobotGrip = (type == "Grip");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[WebGLIoTMonitor] Error finding parent robot: {ex.Message}");
                    }
                }
                
                allComponents.Add(data);
                componentMap[obj] = data;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLIoTMonitor] Error in AddComponent: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Determines the category for a component
        /// </summary>
        private string DetermineCategory(string type, string name, Component comp)
        {
            try
            {
                if (string.IsNullOrEmpty(type))
                    return "other";
                
                switch(type)
                {
                    case "Sensor":
                        return "sensors";
                    case "Lamp":
                        return "lights";
                    case "Source":
                        return "sources";
                    case "Sink":
                        return "sinks";
                    case "Drive":
                        if (comp == null) return "drives";
                        
                        string lowerName = !string.IsNullOrEmpty(name) ? name.ToLower() : "";
                        bool hasTransport = false;
                        
                        try
                        {
                            hasTransport = comp.GetComponent<TransportSurface>() != null ||
                                           comp.GetComponentInChildren<TransportSurface>() != null ||
                                           comp.GetComponentInParent<TransportSurface>() != null;
                        }
                        catch
                        {
                            // Component access failed, continue without transport check
                        }
                        
                        if (!string.IsNullOrEmpty(lowerName))
                        {
                            if (lowerName.Contains("conveyor") || 
                                lowerName.Contains("belt") || 
                                lowerName.Contains("transport") ||
                                hasTransport)
                            {
                                return "conveyors";
                            }
                        }
                        return "drives";
                    case "Axis":
                        if (comp == null || comp.gameObject == null) return "axes";
                        try
                        {
                            GameObject robotParent = FindParentRobot(comp.gameObject);
                            return robotParent != null ? "robots" : "axes";
                        }
                        catch
                        {
                            return "axes";
                        }
                    case "Grip":
                        if (comp == null || comp.gameObject == null) return "grippers";
                        try
                        {
                            GameObject robotParent2 = FindParentRobot(comp.gameObject);
                            return robotParent2 != null ? "robots" : "grippers";
                        }
                        catch
                        {
                            return "grippers";
                        }
                    default:
                        return "other";
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[WebGLIoTMonitor] Error in DetermineCategory: {ex.Message}");
                return "other";
            }
        }
        
        /// <summary>
        /// Finds parent robot GameObject by checking hierarchy for robot indicators (with null safety)
        /// </summary>
        private GameObject FindParentRobot(GameObject obj)
        {
            if (obj == null || obj.transform == null)
                return null;
            
            try
            {
                Transform current = obj.transform.parent;
                int maxDepth = 10; // Prevent infinite loops
                int depth = 0;
                
                while (current != null && depth < maxDepth)
                {
                    if (current.gameObject == null)
                        break;
                    
                    string parentName = current.name != null ? current.name.ToLower() : "";
                    
                    // Check if parent has robot indicators
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        if (parentName.Contains("robot") || parentName.Contains("arm"))
                        {
                            return current.gameObject;
                        }
                    }
                    
                    try
                    {
                        if (current.GetComponent<Axis>() != null && current.GetComponent<Grip>() != null)
                        {
                            return current.gameObject;
                        }
                    }
                    catch
                    {
                        // Component access failed, continue
                    }
                    
                    // Check if parent has multiple axes/grips (likely a robot)
                    try
                    {
                        Axis[] axes = current.GetComponentsInChildren<Axis>();
                        Grip[] grips = current.GetComponentsInChildren<Grip>();
                        if (axes != null && grips != null && (axes.Length > 1 || (axes.Length > 0 && grips.Length > 0)))
                        {
                            return current.gameObject;
                        }
                    }
                    catch
                    {
                        // Component access failed, continue
                    }
                    
                    current = current.parent;
                    depth++;
                }
                
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[WebGLIoTMonitor] Error in FindParentRobot: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Updates status of all discovered components
        /// CRITICAL: Respects emergency stop state - forces stopped status when halted
        /// </summary>
        private void UpdateAllComponents()
        {
            // Check if production is halted
            bool isHalted = EmergencyStopHandler.IsHalted;
            
            foreach (var component in allComponents)
            {
                if (component.ComponentObject == null)
                    continue;
                
                component.IsActive = component.ComponentObject.activeInHierarchy;
                
                // If production is halted, force stopped status for controllable components
                if (isHalted)
                {
                    // Force stopped status for drives, sources, and axes
                    if (component.ComponentType == "Drive" || 
                        component.ComponentType == "Source" ||
                        component.ComponentType == "Axis")
                    {
                        component.Status = "HALTED";
                        component.Value = 0f;
                        component.Unit = component.ComponentType == "Drive" ? "mm/s" : 
                                        component.ComponentType == "Axis" ? "mm" : "State";
                        component.StatusColor = Color.red;
                        // Don't update history when halted - keep showing halted state
                        continue; // Skip normal update
                    }
                }
                
                // Normal update (only if not halted or component type doesn't need to be stopped)
                switch (component.ComponentType)
                {
                    case "Sensor":
                        UpdateSensor(component);
                        break;
                    case "Drive":
                        UpdateDrive(component);
                        break;
                    case "Axis":
                        UpdateAxis(component);
                        break;
                    case "Grip":
                        UpdateGrip(component);
                        break;
                    case "Lamp":
                        UpdateLamp(component);
                        break;
                    case "Source":
                        UpdateSource(component);
                        break;
                    case "Sink":
                        UpdateSink(component);
                        break;
                }
            }
        }
        
        private void UpdateSensor(IoTComponentData data)
        {
            if (data.Sensor == null) return;
            
            data.Status = data.Sensor.Occupied ? "Occupied" : "Free";
            data.Value = data.Sensor.Occupied ? 1f : 0f;
            data.Unit = "State";
            data.StatusColor = data.Sensor.Occupied ? Color.green : Color.gray;
            data.AddValueToHistory(data.Value);
        }
        
        private void UpdateDrive(IoTComponentData data)
        {
            if (data.Drive == null) return;
            
            // CRITICAL: Check emergency stop - if halted, force stopped status
            if (EmergencyStopHandler.IsHalted)
            {
                data.Status = "HALTED";
                data.Value = 0f;
                data.Unit = "mm/s";
                data.StatusColor = Color.red;
                // Don't add to history - keep showing halted
                return;
            }
            
            bool isRunning = data.Drive.JogForward || data.Drive.JogBackward;
            data.Status = isRunning ? "Running" : "Stopped";
            data.Value = isRunning ? data.Drive.CurrentSpeed : 0f;
            data.Unit = "mm/s";
            data.StatusColor = isRunning ? Color.green : Color.red;
            data.AddValueToHistory(data.Value);
        }
        
        private void UpdateAxis(IoTComponentData data)
        {
            if (data.Axis == null) return;
            
            // CRITICAL: Check emergency stop - if halted, force stopped status
            if (EmergencyStopHandler.IsHalted)
            {
                data.Status = "HALTED";
                data.Value = data.Axis.Position; // Keep current position
                data.Unit = "mm";
                data.StatusColor = Color.red;
                // Don't add to history - keep showing halted
                return;
            }
            
            Drive associatedDrive = data.Axis.GetComponent<Drive>();
            bool isAtTarget = false;
            if (associatedDrive != null)
            {
                isAtTarget = associatedDrive.IsAtTarget;
            }
            
            data.Status = isAtTarget ? "At Target" : "Moving";
            data.Value = data.Axis.Position;
            data.Unit = "mm";
            data.StatusColor = isAtTarget ? Color.green : Color.yellow;
            data.AddValueToHistory(data.Value);
        }
        
        private void UpdateGrip(IoTComponentData data)
        {
            if (data.Grip == null) return;
            
            int pickedCount = data.Grip.PickedMUs != null ? data.Grip.PickedMUs.Count : 0;
            data.Status = pickedCount > 0 ? $"Holding {pickedCount}" : "Open";
            data.Value = pickedCount;
            data.Unit = "Objects";
            data.StatusColor = pickedCount > 0 ? Color.green : Color.blue;
            data.AddValueToHistory(data.Value);
        }
        
        private void UpdateLamp(IoTComponentData data)
        {
            if (data.Lamp == null) return;
            
            data.Status = data.Lamp.LampOn ? "ON" : "OFF";
            data.Value = data.Lamp.LampOn ? 1f : 0f;
            data.Unit = "State";
            data.StatusColor = data.Lamp.LampOn ? Color.yellow : Color.gray;
            if (data.Lamp.Flashing)
            {
                data.Status += " (Flashing)";
                data.StatusColor = Color.cyan;
            }
            data.AddValueToHistory(data.Value);
        }
        
        private void UpdateSource(IoTComponentData data)
        {
            if (data.Source == null) return;
            
            // CRITICAL: Check emergency stop - if halted, force stopped status
            if (EmergencyStopHandler.IsHalted)
            {
                data.Status = "HALTED";
                data.Value = 0f;
                data.Unit = "State";
                data.StatusColor = Color.red;
                // Don't add to history - keep showing halted
                return;
            }
            
            data.Status = data.Source.Enabled ? "Active" : "Inactive";
            data.Value = data.Source.Enabled ? 1f : 0f;
            data.Unit = "State";
            data.StatusColor = data.Source.Enabled ? Color.green : Color.gray;
            data.AddValueToHistory(data.Value);
        }
        
        private void UpdateSink(IoTComponentData data)
        {
            if (data.Sink == null) return;
            
            data.Status = "Active";
            data.Value = 1f;
            data.Unit = "State";
            data.StatusColor = Color.green;
            data.AddValueToHistory(data.Value);
        }
        
        /// <summary>
        /// Sends component data to backend API
        /// </summary>
        private void SendDataToAPI()
        {
            if (dataSender != null && allComponents.Count > 0)
            {
                dataSender.SendComponentData(allComponents);
            }
        }
        
        /// <summary>
        /// Get all discovered components
        /// </summary>
        public List<IoTComponentData> GetAllComponents()
        {
            return allComponents;
        }
    }
}






