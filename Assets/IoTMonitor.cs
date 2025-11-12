using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Monitors all IoT components in the scene without modifying them
    /// Discovers and tracks Sensors, Drives, Axes, Grips, Lamps, Sources, and Sinks
    /// </summary>
    public class IoTMonitor : MonoBehaviour
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
        
        // Discovered components
        private List<IoTComponentData> allComponents = new List<IoTComponentData>();
        private Dictionary<GameObject, IoTComponentData> componentMap = new Dictionary<GameObject, IoTComponentData>();
        
        private float lastUpdateTime;
        
        // Events
        public System.Action<IoTComponentData> OnComponentDiscovered;
        public System.Action<List<IoTComponentData>> OnComponentsUpdated;
        
        void Start()
        {
            if (AutoDiscoverOnStart)
            {
                DiscoverAllComponents();
            }
        }
        
        void Update()
        {
            if (Time.time - lastUpdateTime >= UpdateInterval)
            {
                UpdateAllComponents();
                lastUpdateTime = Time.time;
            }
        }
        
        /// <summary>
        /// Discovers all IoT components in the scene
        /// </summary>
        public void DiscoverAllComponents()
        {
            allComponents.Clear();
            componentMap.Clear();
            
            if (MonitorSensors)
            {
                var sensors = FindObjectsByType<Sensor>(FindObjectsSortMode.None);
                foreach (var sensor in sensors)
                {
                    AddComponent(sensor.gameObject, "Sensor", sensor);
                }
            }
            
            if (MonitorDrives)
            {
                var drives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                foreach (var drive in drives)
                {
                    AddComponent(drive.gameObject, "Drive", drive);
                }
            }
            
            if (MonitorAxes)
            {
                var axes = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                foreach (var axis in axes)
                {
                    AddComponent(axis.gameObject, "Axis", axis);
                }
            }
            
            if (MonitorGrips)
            {
                var grips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                foreach (var grip in grips)
                {
                    AddComponent(grip.gameObject, "Grip", grip);
                }
            }
            
            if (MonitorLamps)
            {
                var lamps = FindObjectsByType<Lamp>(FindObjectsSortMode.None);
                foreach (var lamp in lamps)
                {
                    AddComponent(lamp.gameObject, "Lamp", lamp);
                }
            }
            
            if (MonitorSources)
            {
                var sources = FindObjectsByType<Source>(FindObjectsSortMode.None);
                foreach (var source in sources)
                {
                    AddComponent(source.gameObject, "Source", source);
                }
            }
            
            if (MonitorSinks)
            {
                var sinks = FindObjectsByType<Sink>(FindObjectsSortMode.None);
                foreach (var sink in sinks)
                {
                    AddComponent(sink.gameObject, "Sink", sink);
                }
            }
            
            Debug.Log($"[IoTMonitor] Discovered {allComponents.Count} IoT components");
        }
        
        private void AddComponent(GameObject obj, string type, Component component)
        {
            if (componentMap.ContainsKey(obj))
                return;
            
            var data = new IoTComponentData
            {
                ComponentName = obj.name,
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
            
            allComponents.Add(data);
            componentMap[obj] = data;
            
            OnComponentDiscovered?.Invoke(data);
        }
        
        /// <summary>
        /// Updates status of all discovered components
        /// </summary>
        private void UpdateAllComponents()
        {
            foreach (var component in allComponents)
            {
                if (component.ComponentObject == null)
                    continue;
                
                component.IsActive = component.ComponentObject.activeInHierarchy;
                
                // Update based on component type
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
            
            OnComponentsUpdated?.Invoke(allComponents);
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
            
            // Check if Axis has an associated Drive component for target status
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
        /// Get all discovered components
        /// </summary>
        public List<IoTComponentData> GetAllComponents()
        {
            return allComponents;
        }
        
        /// <summary>
        /// Get components by type
        /// </summary>
        public List<IoTComponentData> GetComponentsByType(string type)
        {
            return allComponents.Where(c => c.ComponentType == type).ToList();
        }
        
        /// <summary>
        /// Get component count by type
        /// </summary>
        public Dictionary<string, int> GetComponentCounts()
        {
            return allComponents
                .GroupBy(c => c.ComponentType)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}

