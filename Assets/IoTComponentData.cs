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
        
        public IoTComponentData()
        {
            ValueHistory = new List<float>();
            StatusColor = Color.white;
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


