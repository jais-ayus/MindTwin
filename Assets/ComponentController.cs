using UnityEngine;
using System.Collections.Generic;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Controls individual IoT components with parameter validation
    /// </summary>
    public class ComponentController : MonoBehaviour
    {
        private IoTComponentData componentData;
        private Dictionary<string, ParameterDefinition> parameters;
        
        /// <summary>
        /// Initializes the controller with component data
        /// </summary>
        public void Initialize(IoTComponentData data)
        {
            componentData = data;
            parameters = ParameterDefinitionProvider.GetDefinitions(data.ComponentType);
        }
        
        /// <summary>
        /// Validates a parameter value before setting it
        /// </summary>
        public bool ValidateParameter(string parameterName, object value)
        {
            if (!parameters.ContainsKey(parameterName))
            {
                Debug.LogWarning($"[ComponentController] Parameter {parameterName} not found for {componentData.ComponentType}");
                return false;
            }
            
            ParameterDefinition param = parameters[parameterName];
            
            // Type validation
            if (!ValidateType(value, param.type))
            {
                Debug.LogWarning($"[ComponentController] Type mismatch for {parameterName}. Expected {param.type}");
                return false;
            }
            
            // Range validation for numeric types
            if (param.type == "float" || param.type == "int")
            {
                float numValue = System.Convert.ToSingle(value);
                if (numValue < param.min || numValue > param.max)
                {
                    Debug.LogWarning($"[ComponentController] Value {numValue} out of range [{param.min}, {param.max}] for {parameterName}");
                    return false;
                }
            }
            
            // Mutual exclusivity validation
            if (param.mutualExclusive != null && param.mutualExclusive.Length > 0)
            {
                foreach (string exclusiveParam in param.mutualExclusive)
                {
                    object currentValue = GetParameterValue(exclusiveParam);
                    if (currentValue != null && System.Convert.ToBoolean(currentValue) == true && System.Convert.ToBoolean(value) == true)
                    {
                        Debug.LogWarning($"[ComponentController] {parameterName} and {exclusiveParam} cannot both be true");
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Sets a parameter value on the component
        /// CRITICAL: Blocks all parameter changes when emergency stop is active
        /// </summary>
        public bool SetParameter(string parameterName, object value)
        {
            // CRITICAL: Check emergency stop state - block ALL parameter changes when halted
            if (EmergencyStopHandler.IsHalted)
            {
                // Allow only ResumeProduction command
                if (parameterName == "ResumeProduction" || parameterName == "Resume")
                {
                    // This is handled by WebGLCommandReceiver, but allow it here too
                    return true;
                }
                
                Debug.LogWarning($"[ComponentController] Parameter change blocked - Production is HALTED. Component: {componentData.ComponentName}, Parameter: {parameterName}");
                return false;
            }
            
            if (!ValidateParameter(parameterName, value))
                return false;
            
            try
            {
                switch (componentData.ComponentType)
                {
                    case "Drive":
                        return SetDriveParameter(parameterName, value);
                    case "Sensor":
                        return SetSensorParameter(parameterName, value);
                    case "Lamp":
                        return SetLampParameter(parameterName, value);
                    case "Source":
                        return SetSourceParameter(parameterName, value);
                    case "Grip":
                        return SetGripParameter(parameterName, value);
                    case "Axis":
                        return SetAxisParameter(parameterName, value);
                    default:
                        Debug.LogWarning($"[ComponentController] Unknown component type: {componentData.ComponentType}");
                        return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ComponentController] Error setting parameter {parameterName}: {ex.Message}");
                return false;
            }
        }
        
        private bool SetDriveParameter(string parameterName, object value)
        {
            if (componentData.Drive == null) return false;
            
            // Emergency stop check is already done in SetParameter, but double-check for safety
            if (EmergencyStopHandler.IsHalted)
            {
                Debug.LogWarning($"[ComponentController] Cannot set {parameterName} - Production is halted");
                return false;
            }
            
            try
            {
                switch (parameterName)
                {
                    case "TargetSpeed":
                        float speed = System.Convert.ToSingle(value);
                        componentData.Drive.TargetSpeed = speed;
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.TargetSpeed = {speed}");
                        return true;
                    case "JogForward":
                        bool jogForward = System.Convert.ToBoolean(value);
                        componentData.Drive.JogForward = jogForward;
                        if (jogForward) 
                        {
                            componentData.Drive.JogBackward = false;
                            Debug.Log($"[ComponentController] Started {componentData.ComponentName} forward");
                        }
                        else
                        {
                            Debug.Log($"[ComponentController] Stopped {componentData.ComponentName} forward");
                        }
                        return true;
                    case "JogBackward":
                        bool jogBackward = System.Convert.ToBoolean(value);
                        componentData.Drive.JogBackward = jogBackward;
                        if (jogBackward) 
                        {
                            componentData.Drive.JogForward = false;
                            Debug.Log($"[ComponentController] Started {componentData.ComponentName} backward");
                        }
                        else
                        {
                            Debug.Log($"[ComponentController] Stopped {componentData.ComponentName} backward");
                        }
                        return true;
                    case "TargetPosition":
                        componentData.Drive.TargetPosition = System.Convert.ToSingle(value);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.TargetPosition = {componentData.Drive.TargetPosition}");
                        return true;
                    case "TargetStartMove":
                        componentData.Drive.TargetStartMove = System.Convert.ToBoolean(value);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.TargetStartMove = {componentData.Drive.TargetStartMove}");
                        return true;
                    case "SpeedOverride":
                        componentData.Drive.SpeedOverride = System.Convert.ToSingle(value);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.SpeedOverride = {componentData.Drive.SpeedOverride}");
                        return true;
                    case "Enabled":
                    case "Enable":
                        // Enable/disable drive by activating/deactivating GameObject
                        if (componentData.ComponentObject != null)
                        {
                            bool enable = System.Convert.ToBoolean(value);
                            componentData.ComponentObject.SetActive(enable);
                            Debug.Log($"[ComponentController] Set {componentData.ComponentName}.Enabled = {enable}");
                            return true;
                        }
                        return false;
                    default:
                        Debug.LogWarning($"[ComponentController] Unknown parameter: {parameterName}");
                        return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ComponentController] Error setting {parameterName}: {ex.Message}");
                return false;
            }
        }
        
        private bool SetSensorParameter(string parameterName, object value)
        {
            if (componentData.Sensor == null) return false;
            
            switch (parameterName)
            {
                case "DisplayStatus":
                    componentData.Sensor.DisplayStatus = System.Convert.ToBoolean(value);
                    return true;
                case "LimitSensorToTag":
                    componentData.Sensor.LimitSensorToTag = value.ToString();
                    return true;
                case "Enabled":
                case "Enable":
                    // Enable/disable sensor by activating/deactivating GameObject
                    if (componentData.ComponentObject != null)
                    {
                        bool enable = System.Convert.ToBoolean(value);
                        componentData.ComponentObject.SetActive(enable);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.Enabled = {enable}");
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
        
        private bool SetLampParameter(string parameterName, object value)
        {
            if (componentData.Lamp == null) return false;
            
            switch (parameterName)
            {
                case "LampOn":
                    componentData.Lamp.LampOn = System.Convert.ToBoolean(value);
                    return true;
                case "Flashing":
                    componentData.Lamp.Flashing = System.Convert.ToBoolean(value);
                    return true;
                case "Enabled":
                case "Enable":
                    // Enable/disable lamp by activating/deactivating GameObject
                    if (componentData.ComponentObject != null)
                    {
                        bool enable = System.Convert.ToBoolean(value);
                        componentData.ComponentObject.SetActive(enable);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.Enabled = {enable}");
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
        
        private bool SetSourceParameter(string parameterName, object value)
        {
            if (componentData.Source == null) return false;
            
            // CRITICAL: Block source enabling when halted
            if (EmergencyStopHandler.IsHalted && parameterName == "Enabled" && System.Convert.ToBoolean(value) == true)
            {
                Debug.LogWarning($"[ComponentController] Cannot enable source - Production is halted");
                return false;
            }
            
            switch (parameterName)
            {
                case "Enabled":
                    componentData.Source.Enabled = System.Convert.ToBoolean(value);
                    return true;
                default:
                    return false;
            }
        }
        
        private bool SetGripParameter(string parameterName, object value)
        {
            if (componentData.Grip == null) return false;
            
            switch (parameterName)
            {
                case "PickObjects":
                    bool pick = System.Convert.ToBoolean(value);
                    componentData.Grip.PickObjects = pick;
                    if (pick) componentData.Grip.PlaceObjects = false;
                    return true;
                case "PlaceObjects":
                    bool place = System.Convert.ToBoolean(value);
                    componentData.Grip.PlaceObjects = place;
                    if (place) componentData.Grip.PickObjects = false;
                    return true;
                case "Enabled":
                case "Enable":
                    // Enable/disable grip by activating/deactivating GameObject
                    if (componentData.ComponentObject != null)
                    {
                        bool enable = System.Convert.ToBoolean(value);
                        componentData.ComponentObject.SetActive(enable);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.Enabled = {enable}");
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }
        
        private bool SetAxisParameter(string parameterName, object value)
        {
            if (componentData.Axis == null) return false;
            
            // CRITICAL: Block axis movement when halted (except Enable/Disable)
            if (EmergencyStopHandler.IsHalted && parameterName != "Enabled" && parameterName != "Enable")
            {
                Debug.LogWarning($"[ComponentController] Cannot set {parameterName} - Production is halted");
                return false;
            }
            
            switch (parameterName)
            {
                case "Enabled":
                case "Enable":
                    // Enable/disable axis by activating/deactivating GameObject
                    if (componentData.ComponentObject != null)
                    {
                        bool enable = System.Convert.ToBoolean(value);
                        componentData.ComponentObject.SetActive(enable);
                        Debug.Log($"[ComponentController] Set {componentData.ComponentName}.Enabled = {enable}");
                        return true;
                    }
                    return false;
                case "TargetPosition":
                    Drive associatedDrive = componentData.Axis.GetComponent<Drive>();
                    if (associatedDrive == null) return false;
                    associatedDrive.TargetPosition = System.Convert.ToSingle(value);
                    return true;
                case "TargetStartMove":
                    Drive associatedDrive2 = componentData.Axis.GetComponent<Drive>();
                    if (associatedDrive2 == null) return false;
                    associatedDrive2.TargetStartMove = System.Convert.ToBoolean(value);
                    return true;
                default:
                    return false;
            }
        }
        
        private bool ValidateType(object value, string expectedType)
        {
            switch (expectedType)
            {
                case "boolean":
                    return value is bool || value is int || value is float;
                case "float":
                    return value is float || value is double || value is int;
                case "int":
                    return value is int || value is float;
                case "string":
                    return value is string;
                default:
                    return false;
            }
        }
        
        private object GetParameterValue(string parameterName)
        {
            // Get current value of parameter
            switch (componentData.ComponentType)
            {
                case "Drive":
                    if (componentData.Drive == null) return null;
                    switch (parameterName)
                    {
                        case "JogForward": return componentData.Drive.JogForward;
                        case "JogBackward": return componentData.Drive.JogBackward;
                        default: return null;
                    }
                case "Grip":
                    if (componentData.Grip == null) return null;
                    switch (parameterName)
                    {
                        case "PickObjects": return componentData.Grip.PickObjects;
                        case "PlaceObjects": return componentData.Grip.PlaceObjects;
                        default: return null;
                    }
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Gets current component state for response
        /// </summary>
        public ComponentState GetCurrentState()
        {
            return new ComponentState
            {
                name = componentData.ComponentName,
                status = componentData.Status,
                value = componentData.Value,
                unit = componentData.Unit
            };
        }
        
        /// <summary>
        /// Gets the component data
        /// </summary>
        public IoTComponentData GetComponentData()
        {
            return componentData;
        }
    }
    
    /// <summary>
    /// Component state for API responses
    /// </summary>
    [System.Serializable]
    public class ComponentState
    {
        public string name;
        public string status;
        public float value;
        public string unit;
    }
}

