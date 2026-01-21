using UnityEngine;
using System.Collections.Generic;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Data structure to hold information about an IoT component
    /// </summary>
    [System.Serializable]
    public class IoTComponentData
    {
        public string ComponentName;
        public string ComponentType;
        public GameObject ComponentObject;
        public bool IsActive;
        public string Status;
        public float Value;
        public string Unit;
        public Color StatusColor;
        public Dictionary<string, object> Metadata;
        
        // Component-specific data
        public Sensor Sensor;
        public Drive Drive;
        public Axis Axis;
        public Grip Grip;
        public Lamp Lamp;
        public Source Source;
        public Sink Sink;
        
        // Historical data for trends
        public List<float> ValueHistory;
        public int MaxHistorySize = 100;
        
        // Category metadata (Phase 1)
        public string Category;
        public bool HasTransportSurface;
        public string ParentRobot;
        public bool IsRobotAxis;
        public bool IsRobotGrip;
        
        public IoTComponentData()
        {
            ValueHistory = new List<float>();
            StatusColor = Color.white;
            Category = "other";
            HasTransportSurface = false;
            ParentRobot = null;
            IsRobotAxis = false;
            IsRobotGrip = false;
            Metadata = new Dictionary<string, object>();
        }
        
        public void AddValueToHistory(float val)
        {
            ValueHistory.Add(val);
            if (ValueHistory.Count > MaxHistorySize)
            {
                ValueHistory.RemoveAt(0);
            }
        }
    }
}







