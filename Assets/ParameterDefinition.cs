using UnityEngine;
using System.Collections.Generic;

namespace IoTDashboard
{
    /// <summary>
    /// Defines parameter metadata for component types
    /// </summary>
    [System.Serializable]
    public class ParameterDefinition
    {
        public string name;
        public string type; // "boolean", "float", "int", "string"
        public float min;
        public float max;
        public string unit;
        public float defaultValue;
        public bool editable;
        public string[] mutualExclusive; // Parameters that cannot be true at the same time
        
        public ParameterDefinition(string name, string type, float min = 0, float max = 100, string unit = "", bool editable = true, string[] mutualExclusive = null)
        {
            this.name = name;
            this.type = type;
            this.min = min;
            this.max = max;
            this.unit = unit;
            this.defaultValue = type == "boolean" ? 0 : min;
            this.editable = editable;
            this.mutualExclusive = mutualExclusive ?? new string[0];
        }
    }
    
    /// <summary>
    /// Static class to provide parameter definitions for each component type
    /// </summary>
    public static class ParameterDefinitionProvider
    {
        private static Dictionary<string, Dictionary<string, ParameterDefinition>> definitions = new Dictionary<string, Dictionary<string, ParameterDefinition>>();
        
        static ParameterDefinitionProvider()
        {
            InitializeDefinitions();
        }
        
        private static void InitializeDefinitions()
        {
            // Drive parameters
            var driveParams = new Dictionary<string, ParameterDefinition>();
            driveParams["TargetSpeed"] = new ParameterDefinition("TargetSpeed", "float", 0, 200, "mm/s", true);
            driveParams["JogForward"] = new ParameterDefinition("JogForward", "boolean", 0, 1, "", true, new string[] { "JogBackward" });
            driveParams["JogBackward"] = new ParameterDefinition("JogBackward", "boolean", 0, 1, "", true, new string[] { "JogForward" });
            driveParams["TargetPosition"] = new ParameterDefinition("TargetPosition", "float", -1000, 1000, "mm", true);
            driveParams["TargetStartMove"] = new ParameterDefinition("TargetStartMove", "boolean", 0, 1, "", true);
            driveParams["SpeedOverride"] = new ParameterDefinition("SpeedOverride", "float", 0.1f, 5.0f, "multiplier", true);
            definitions["Drive"] = driveParams;
            
            // Sensor parameters
            var sensorParams = new Dictionary<string, ParameterDefinition>();
            sensorParams["DisplayStatus"] = new ParameterDefinition("DisplayStatus", "boolean", 0, 1, "", true);
            sensorParams["LimitSensorToTag"] = new ParameterDefinition("LimitSensorToTag", "string", 0, 50, "", true);
            sensorParams["Enabled"] = new ParameterDefinition("Enabled", "boolean", 0, 1, "", true);
            definitions["Sensor"] = sensorParams;
            
            // Lamp parameters
            var lampParams = new Dictionary<string, ParameterDefinition>();
            lampParams["LampOn"] = new ParameterDefinition("LampOn", "boolean", 0, 1, "", true);
            lampParams["Flashing"] = new ParameterDefinition("Flashing", "boolean", 0, 1, "", true);
            lampParams["Enabled"] = new ParameterDefinition("Enabled", "boolean", 0, 1, "", true);
            definitions["Lamp"] = lampParams;
            
            // Source parameters
            var sourceParams = new Dictionary<string, ParameterDefinition>();
            sourceParams["Enabled"] = new ParameterDefinition("Enabled", "boolean", 0, 1, "", true);
            definitions["Source"] = sourceParams;
            
            // Grip parameters
            var gripParams = new Dictionary<string, ParameterDefinition>();
            gripParams["PickObjects"] = new ParameterDefinition("PickObjects", "boolean", 0, 1, "", true, new string[] { "PlaceObjects" });
            gripParams["PlaceObjects"] = new ParameterDefinition("PlaceObjects", "boolean", 0, 1, "", true, new string[] { "PickObjects" });
            gripParams["Enabled"] = new ParameterDefinition("Enabled", "boolean", 0, 1, "", true);
            definitions["Grip"] = gripParams;
            
            // Axis parameters (controlled via Drive)
            var axisParams = new Dictionary<string, ParameterDefinition>();
            axisParams["TargetPosition"] = new ParameterDefinition("TargetPosition", "float", -1000, 1000, "mm", true);
            axisParams["TargetStartMove"] = new ParameterDefinition("TargetStartMove", "boolean", 0, 1, "", true);
            axisParams["Enabled"] = new ParameterDefinition("Enabled", "boolean", 0, 1, "", true);
            definitions["Axis"] = axisParams;
        }
        
        public static Dictionary<string, ParameterDefinition> GetDefinitions(string componentType)
        {
            if (definitions.ContainsKey(componentType))
            {
                return new Dictionary<string, ParameterDefinition>(definitions[componentType]);
            }
            return new Dictionary<string, ParameterDefinition>();
        }
        
        public static ParameterDefinition GetDefinition(string componentType, string parameterName)
        {
            if (definitions.ContainsKey(componentType) && definitions[componentType].ContainsKey(parameterName))
            {
                return definitions[componentType][parameterName];
            }
            return null;
        }
    }
}


