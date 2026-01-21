using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Receives and processes commands from the backend API
    /// Uses polling mechanism for WebGL compatibility
    /// </summary>
    public class WebGLCommandReceiver : MonoBehaviour
    {
        [Header("API Settings")]
        [Tooltip("Backend API base URL")]
        public string ApiBaseUrl = "http://localhost:3000";
        
        [Header("Command Settings")]
        [Tooltip("Poll interval for checking commands (seconds)")]
        public float PollInterval = 0.05f; // Real-time polling (50ms = 20 times per second)
        
        [Tooltip("Max queue size")]
        public int MaxQueueSize = 100;
        
        [Tooltip("Command timeout in seconds")]
        public float CommandTimeout = 30f;
        
        [Tooltip("Max retry attempts")]
        public int MaxRetries = 3;
        
        private Queue<Command> commandQueue = new Queue<Command>();
        private Queue<Command> emergencyQueue = new Queue<Command>();
        private bool emergencyProcessing = false;
        private bool isProcessing = false;
        private Dictionary<string, ComponentController> controllers = new Dictionary<string, ComponentController>();
        private WebGLIoTMonitor iotMonitor;
        private float lastPollTime = 0f;
        private bool isPollingCommands = false;
        
        void Start()
        {
            iotMonitor = GetComponent<WebGLIoTMonitor>();
            if (iotMonitor == null)
            {
                iotMonitor = gameObject.AddComponent<WebGLIoTMonitor>();
            }
            
            // Register all component controllers
            StartCoroutine(RegisterControllersDelayed());
        }
        
        void OnEnable()
        {
            // Start polling immediately when enabled
            lastPollTime = 0f; // Force immediate poll on next Update
        }
        
        void Update()
        {
            // Poll for commands periodically (real-time - every 50ms)
            if (!isPollingCommands && Time.time - lastPollTime >= PollInterval)
            {
                lastPollTime = Time.time;
                StartCoroutine(PollForCommands());
            }
            
            // Process queued commands immediately (don't wait)
            // Process multiple commands if queue is building up
            int maxProcessPerFrame = 3; // Process up to 3 commands per frame for real-time
            int processed = 0;
            while (!emergencyProcessing && !isProcessing && commandQueue.Count > 0 && processed < maxProcessPerFrame)
            {
                ProcessNextCommand();
                processed++;
            }
        }
        
        private IEnumerator RegisterControllersDelayed()
        {
            // Wait a bit for components to be discovered
            yield return new WaitForSeconds(1f);
            RegisterControllers();
        }
        
        private void RegisterControllers()
        {
            // Get all components from IoT Monitor
            var components = iotMonitor.GetAllComponents();
            foreach (var comp in components)
            {
                if (comp.ComponentObject != null)
                {
                    var controller = comp.ComponentObject.GetComponent<ComponentController>();
                    if (controller == null)
                    {
                        controller = comp.ComponentObject.AddComponent<ComponentController>();
                    }
                    controller.Initialize(comp);
                    controllers[comp.ComponentName] = controller;
                }
            }
            
            Debug.Log($"[WebGLCommandReceiver] Registered {controllers.Count} component controllers");
        }
        
        private IEnumerator PollForCommands()
        {
            if (isPollingCommands)
                yield break;
            
            isPollingCommands = true;
            string url = ApiBaseUrl + "/api/commands/pending";
            
            try
            {
            using (UnityEngine.Networking.UnityWebRequest request = 
                UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string jsonResponse = request.downloadHandler.text;
                        
                        if (string.IsNullOrEmpty(jsonResponse) || jsonResponse.Trim() == "{}")
                        {
                            yield break;
                        }
                        
                        CommandListResponse response = JsonUtility.FromJson<CommandListResponse>(jsonResponse);
                        
                        if (response != null && response.commands != null && response.commands.Length > 0)
                        {
                            Debug.Log($"[WebGLCommandReceiver] Received {response.commands.Length} command(s) from backend");
                            
                            System.Collections.Generic.List<Command> normalCommands = new System.Collections.Generic.List<Command>();
                            
                            for (int i = 0; i < response.commands.Length; i++)
                            {
                                Command cmd = response.commands[i];
                                
                                if (cmd == null)
                                {
                                    Debug.LogWarning($"[WebGLCommandReceiver] Command at index {i} is null, skipping");
                                    continue;
                                }
                                
                                try
                                {
                                    if (cmd.IsEmergency() || 
                                        (!string.IsNullOrEmpty(cmd.parameter) && cmd.parameter.Contains("Emergency")) ||
                                        (cmd.componentId == "all" && !string.IsNullOrEmpty(cmd.parameter)))
                                    {
                                            emergencyQueue.Enqueue(cmd);
                                    }
                                    else
                                    {
                                        normalCommands.Add(cmd);
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[WebGLCommandReceiver] Error checking command type: {ex.Message}");
                                    normalCommands.Add(cmd);
                                }
                            }
                            
                                if (emergencyQueue.Count > 0 && !emergencyProcessing)
                                {
                                    StartCoroutine(ProcessEmergencyCommands());
                                }
                                
                            for (int i = 0; i < normalCommands.Count; i++)
                            {
                                Command cmd = normalCommands[i];
                                
                                if (cmd == null)
                                {
                                    Debug.LogWarning($"[WebGLCommandReceiver] Normal command at index {i} is null, skipping");
                                    continue;
                                }
                                
                                try
                                {
                                    ProcessCommand(cmd);
                                }
                                catch (System.Exception ex)
                                {
                                    Debug.LogError($"[WebGLCommandReceiver] Error processing normal command: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[WebGLCommandReceiver] Error parsing commands: {ex.Message}\nResponse: {request.downloadHandler.text}");
                    }
                }
                else if (request.responseCode == 404)
                {
                        // normal - nothing queued
                }
                else if (request.result != UnityEngine.Networking.UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogWarning($"[WebGLCommandReceiver] Error polling commands: {request.error} (Code: {request.responseCode})");
                }
                }
            }
            finally
            {
                isPollingCommands = false;
            }
        }
        
        public void ProcessCommand(Command command)
        {
            // Null check
            if (command == null)
            {
                Debug.LogError("[WebGLCommandReceiver] Cannot process null command");
                return;
            }
            
            if (commandQueue.Count >= MaxQueueSize)
            {
                Debug.LogWarning($"[WebGLCommandReceiver] Command queue full, rejecting command {command.commandId ?? "null"}");
                SendErrorResponse(command, "Command queue full");
                return;
            }
            
            try
            {
                commandQueue.Enqueue(command);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLCommandReceiver] Error enqueueing command: {ex.Message}");
            }
        }
        
        private void ProcessNextCommand()
        {
            if (emergencyProcessing || isProcessing || commandQueue.Count == 0)
                return;
            
            try
            {
                isProcessing = true;
                Command command = commandQueue.Dequeue();
                
                if (command == null)
                {
                    Debug.LogError("[WebGLCommandReceiver] Dequeued null command, skipping");
                    isProcessing = false;
                    ProcessNextCommand(); // Try next command
                    return;
                }
                
                StartCoroutine(ExecuteCommand(command));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLCommandReceiver] Error processing next command: {ex.Message}");
                isProcessing = false;
            }
        }
        
        private IEnumerator ProcessEmergencyCommands()
        {
            if (emergencyProcessing)
                yield break;
            
            emergencyProcessing = true;
            
            while (emergencyQueue.Count > 0)
            {
                Command cmd = emergencyQueue.Dequeue();
                if (cmd == null)
                    continue;
                
                isProcessing = true;
                yield return ExecuteCommand(cmd, true);
            }
            
            emergencyProcessing = false;
            ProcessNextCommand();
        }
        
        private IEnumerator ExecuteCommand(Command command, bool skipQueueAdvance = false)
        {
            // Null check
            if (command == null)
            {
                Debug.LogError("[WebGLCommandReceiver] Cannot execute null command");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Handle resume production command
            if (!string.IsNullOrEmpty(command.parameter) && 
                (command.parameter == "ResumeProduction" || command.parameter == "Resume"))
            {
                try
                {
                    EmergencyStopHandler.ResumeProduction();
                    Debug.Log("[WebGLCommandReceiver] Production resumed from command");
                    
                    ComponentState resumeState = new ComponentState
                    {
                        name = "ResumeProduction",
                        status = "RUNNING",
                        value = 1,
                        unit = "State"
                    };
                    SendSuccessResponse(command, resumeState);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[WebGLCommandReceiver] Error resuming production: {ex.Message}");
                    SendErrorResponse(command, $"Resume error: {ex.Message}");
                }
                
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Handle emergency stop command (check multiple ways for reliability)
            if (command.IsEmergency())
            {
                Debug.Log($"[WebGLCommandReceiver] ========== PROCESSING EMERGENCY STOP COMMAND ==========");
                Debug.Log($"[WebGLCommandReceiver] Command ID: {command.commandId}");
                Debug.Log($"[WebGLCommandReceiver] Parameter: {command.parameter}");
                Debug.Log($"[WebGLCommandReceiver] isEmergencyCommand: {command.isEmergencyCommand}");
                Debug.Log($"[WebGLCommandReceiver] Value: {command.value}");
                
                bool emergencySuccess = false;
                string emergencyError = null;
                string stopReason = "Emergency stop from dashboard";
                string stopCategory = null;
                
                try
                {
                    // Parse emergency stop data (robust parsing)
                    if (!string.IsNullOrEmpty(command.value))
                    {
                        try
                        {
                            // Try to parse as JSON
                            EmergencyStopData emergencyData = JsonUtility.FromJson<EmergencyStopData>(command.value);
                            if (emergencyData != null)
                            {
                                stopReason = emergencyData.reason ?? stopReason;
                                stopCategory = emergencyData.category;
                                Debug.Log($"[WebGLCommandReceiver] Parsed emergency data: reason={stopReason}, category={stopCategory}");
                            }
                        }
                        catch (System.Exception parseEx)
                        {
                            // If JSON parsing fails, try to extract from string (with null safety)
                            Debug.LogWarning($"[WebGLCommandReceiver] JSON parse failed, using fallback: {parseEx.Message}");
                            
                            if (!string.IsNullOrEmpty(command.value) && command.value.Contains("reason"))
                            {
                                try
                                {
                                    // Try to extract reason manually
                                    int reasonStart = command.value.IndexOf("\"reason\"");
                                    if (reasonStart >= 0 && reasonStart < command.value.Length)
                                    {
                                        int valueStart = command.value.IndexOf("\"", reasonStart + 8);
                                        if (valueStart >= 0 && valueStart < command.value.Length - 1)
                                        {
                                            valueStart += 1; // Move past the quote
                                            int valueEnd = command.value.IndexOf("\"", valueStart);
                                            if (valueEnd > valueStart && valueEnd <= command.value.Length)
                                            {
                                                stopReason = command.value.Substring(valueStart, valueEnd - valueStart);
                                            }
                                        }
                                    }
                                }
                                catch (System.Exception extractEx)
                                {
                                    Debug.LogWarning($"[WebGLCommandReceiver] Failed to extract reason from string: {extractEx.Message}");
                                    // Use default reason
                                }
                            }
                        }
                    }
                    
                    Debug.Log($"[WebGLCommandReceiver] Triggering emergency stop: {stopReason} (Category: {stopCategory ?? "All"})");
                    
                    // CRITICAL: IMMEDIATE synchronous stop BEFORE triggering handler
                    // This ensures components stop in the same frame the command is received
                    StopAllComponentsImmediate(stopCategory);
                    
                    // CRITICAL: Ensure handler exists before triggering
                    // This will auto-create the handler if it doesn't exist
                    EmergencyStopHandler.TriggerEmergencyStop(stopReason, stopCategory);
                    
                    // IMMEDIATE enforcement coroutine - continues enforcement for short period
                    StartCoroutine(ImmediateEmergencyEnforcement(stopCategory));
                    
                    // Verify stop was successful
                    if (EmergencyStopHandler.IsHalted)
                    {
                        emergencySuccess = true;
                        Debug.Log("[WebGLCommandReceiver] Emergency stop confirmed successful - IsHalted = true");
                    }
                    else
                    {
                        Debug.LogWarning("[WebGLCommandReceiver] Emergency stop triggered but IsHalted is false - may have failed");
                        emergencySuccess = true; // Still consider it success if we got here
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[WebGLCommandReceiver] CRITICAL ERROR in emergency stop: {ex.Message}");
                    Debug.LogError($"[WebGLCommandReceiver] Stack trace: {ex.StackTrace}");
                    emergencyError = $"Emergency stop error: {ex.Message}";
                }
                
                if (emergencySuccess)
                {
                    // Create a simple state response for emergency stop
                    ComponentState emergencyState = new ComponentState
                    {
                        name = "EmergencyStop",
                        status = "HALTED",
                        value = 0,
                        unit = "State"
                    };
                    SendSuccessResponse(command, emergencyState);
                    Debug.Log("[WebGLCommandReceiver] Emergency stop response sent to backend");
                }
                else
                {
                    SendErrorResponse(command, emergencyError ?? "Emergency stop failed");
                    Debug.LogError($"[WebGLCommandReceiver] Emergency stop FAILED: {emergencyError}");
                }
                
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Check control mode - block non-emergency commands when not in Manual mode
            if (!EmergencyStopHandler.AreCommandsAllowed && !command.IsEmergency())
            {
                string modeStatus = EmergencyStopHandler.GetModeStatus();
                Debug.LogWarning($"[WebGLCommandReceiver] Command blocked - {modeStatus}: {command.commandId}");
                SendErrorResponse(command, $"Commands blocked: {modeStatus}");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Get component controller (with null safety)
            if (string.IsNullOrEmpty(command.componentId))
            {
                Debug.LogError("[WebGLCommandReceiver] Command has null or empty componentId");
                SendErrorResponse(command, "Invalid component ID");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            if (!controllers.ContainsKey(command.componentId))
            {
                // Try to register controllers again (in case new components were added)
                RegisterControllers();
                
                if (!controllers.ContainsKey(command.componentId))
                {
                    Debug.LogWarning($"[WebGLCommandReceiver] Component not found: {command.componentId}");
                    SendErrorResponse(command, "Component not found");
                    CompleteCommand(skipQueueAdvance);
                    yield break;
                }
            }
            
            ComponentController controller = controllers[command.componentId];
            
            if (controller == null)
            {
                Debug.LogError($"[WebGLCommandReceiver] Controller is null for component: {command.componentId}");
                SendErrorResponse(command, "Controller not initialized");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Parse value from JSON string
            object parsedValue = null;
            try
            {
                parsedValue = ParseCommandValue(command.value, command.parameter);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLCommandReceiver] Error parsing command value: {ex.Message}");
                SendErrorResponse(command, $"Error parsing value: {ex.Message}");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Validate parameter
            bool isValid = false;
            try
            {
                isValid = controller.ValidateParameter(command.parameter, parsedValue);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLCommandReceiver] Error validating parameter: {ex.Message}");
                SendErrorResponse(command, $"Validation error: {ex.Message}");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            if (!isValid)
            {
                SendErrorResponse(command, "Parameter validation failed");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            // Apply command
            bool success = false;
            try
            {
                success = controller.SetParameter(command.parameter, parsedValue);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLCommandReceiver] Error setting parameter: {ex.Message}");
                SendErrorResponse(command, $"Error: {ex.Message}");
                CompleteCommand(skipQueueAdvance);
                yield break;
            }
            
            if (success)
            {
                yield return new WaitForSeconds(0.1f); // Small delay to ensure state update
                SendSuccessResponse(command, controller.GetCurrentState());
            }
            else
            {
                SendErrorResponse(command, "Failed to apply command");
            }
            
            CompleteCommand(skipQueueAdvance);
        }
        
        private void CompleteCommand(bool skipQueueAdvance)
        {
            isProcessing = false;
            if (!skipQueueAdvance)
            {
            ProcessNextCommand();
            }
        }
        
        private void SendSuccessResponse(Command command, ComponentState state)
        {
            string url = ApiBaseUrl + "/api/commands/" + command.commandId + "/complete";
            
            if (command.IsEmergency())
            {
                Debug.Log($"[WebGLCommandReceiver] Sending emergency stop success response: {command.commandId}");
            }
            
            StartCoroutine(SendResponse(url, new CommandResponse 
            { 
                success = true, 
                state = state,
                message = command.IsEmergency() ? "Emergency stop executed successfully" : "Command executed successfully"
            }));
        }
        
        private void SendErrorResponse(Command command, string errorMessage)
        {
            string url = ApiBaseUrl + "/api/commands/" + command.commandId + "/error";
            StartCoroutine(SendResponse(url, new CommandResponse 
            { 
                success = false, 
                error = errorMessage 
            }));
        }
        
        private object ParseCommandValue(string jsonValue, string parameterName)
        {
            try
            {
                // Try to parse as JSON first
                if (jsonValue.StartsWith("{") || jsonValue.StartsWith("["))
                {
                    // Complex object - would need proper JSON parsing
                    // For now, treat as string
                    return jsonValue;
                }
                
                // Try boolean
                if (jsonValue.ToLower() == "true") return true;
                if (jsonValue.ToLower() == "false") return false;
                
                // Try number
                if (float.TryParse(jsonValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatValue))
                {
                    return floatValue;
                }
                
                // Default to string
                return jsonValue;
            }
            catch
            {
                return jsonValue;
            }
        }
        
        private IEnumerator SendResponse(string url, CommandResponse response)
        {
            // Create JSON manually for better compatibility
            string jsonData = "{" +
                $"\"success\":{response.success.ToString().ToLower()}," +
                (response.state != null ? $"\"state\":{{\"name\":\"{response.state.name}\",\"status\":\"{response.state.status}\",\"value\":{response.state.value},\"unit\":\"{response.state.unit}\"}}," : "") +
                (!string.IsNullOrEmpty(response.message) ? $"\"message\":\"{EscapeJson(response.message)}\"," : "") +
                (!string.IsNullOrEmpty(response.error) ? $"\"error\":\"{EscapeJson(response.error)}\"," : "") +
                "\"timestamp\":\"" + System.DateTime.UtcNow.ToString("o") + "\"" +
                "}";
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityEngine.Networking.UnityWebRequest request = 
                new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[WebGLCommandReceiver] Failed to send response: {request.error}");
                }
            }
        }
        
        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str))
                return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        /// <summary>
        /// IMMEDIATE synchronous stop - stops all components in the same frame (no coroutine delay)
        /// </summary>
        private void StopAllComponentsImmediate(string category = null)
        {
            Debug.Log("[WebGLCommandReceiver] Executing IMMEDIATE synchronous stop (no coroutine delay)");
            
            try
            {
                // Force stop all drives immediately (synchronous)
                Drive[] allDrives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                if (allDrives != null)
                {
                    int stopped = 0;
                    for (int i = 0; i < allDrives.Length; i++)
                    {
                        Drive drive = allDrives[i];
                        if (drive == null || drive.gameObject == null)
                            continue;
                        
                        try
                        {
                            // CRITICAL: Use ForceStop to completely freeze the drive
                            drive.ForceStop = true;
                            drive.Stop();
                            drive.JogForward = false;
                            drive.JogBackward = false;
                            drive.TargetStartMove = false;
                            drive.TargetSpeed = 0;
                            stopped++;
                        }
                        catch { }
                    }
                    Debug.Log($"[WebGLCommandReceiver] IMMEDIATELY stopped {stopped} drives (synchronous)");
                }
                
                // Force stop all sources immediately (synchronous)
                Source[] allSources = FindObjectsByType<Source>(FindObjectsSortMode.None);
                if (allSources != null)
                {
                    int stopped = 0;
                    for (int i = 0; i < allSources.Length; i++)
                    {
                        Source source = allSources[i];
                        if (source == null || source.gameObject == null)
                            continue;
                        
                        try
                        {
                            source.Enabled = false;
                            source.GenerateMU = false;
                            stopped++;
                        }
                        catch { }
                    }
                    Debug.Log($"[WebGLCommandReceiver] IMMEDIATELY stopped {stopped} sources (synchronous)");
                }
                
                // Force stop all Axis components immediately (synchronous)
                Axis[] allAxes = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                if (allAxes != null)
                {
                    int stopped = 0;
                    for (int i = 0; i < allAxes.Length; i++)
                    {
                        Axis axis = allAxes[i];
                        if (axis == null || axis.gameObject == null)
                            continue;
                        
                        try
                        {
                            Drive axisDrive = axis.GetComponent<Drive>();
                            if (axisDrive != null)
                            {
                                // CRITICAL: Use ForceStop to completely freeze the axis drive
                                axisDrive.ForceStop = true;
                                axisDrive.Stop();
                                axisDrive.JogForward = false;
                                axisDrive.JogBackward = false;
                                axisDrive.TargetStartMove = false;
                                axisDrive.TargetSpeed = 0;
                                stopped++;
                            }
                        }
                        catch { }
                    }
                    Debug.Log($"[WebGLCommandReceiver] IMMEDIATELY stopped {stopped} axes (synchronous)");
                }
                
                // Force stop all Grip components immediately (synchronous)
                // CRITICAL FIX: Do NOT call DeActivate(true) - it may release MUs!
                Grip[] allGrips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                if (allGrips != null)
                {
                    int stopped = 0;
                    for (int i = 0; i < allGrips.Length; i++)
                    {
                        Grip grip = allGrips[i];
                        if (grip == null || grip.gameObject == null)
                            continue;
                        
                        try
                        {
                            // CRITICAL FIX: Just stop operations, don't deactivate the grip!
                            grip.PickObjects = false;
                            grip.PlaceObjects = false;
                            
                            // Ensure all gripped MUs stay kinematic and attached
                            if (grip.PickedMUs != null && grip.PickedMUs.Count > 0)
                            {
                                foreach (GameObject muObj in grip.PickedMUs)
                                {
                                    if (muObj != null)
                                    {
                                        MU mu = muObj.GetComponent<MU>();
                                        if (mu != null)
                                        {
                                            if (mu.Rigidbody != null)
                                            {
                                                mu.Rigidbody.isKinematic = true;
                                                mu.Rigidbody.velocity = Vector3.zero;
                                                mu.Rigidbody.angularVelocity = Vector3.zero;
                                            }
                                            // Ensure MU stays parented to grip
                                            if (muObj.transform.parent != grip.transform)
                                            {
                                                muObj.transform.SetParent(grip.transform, true);
                                            }
                                        }
                                    }
                                }
                            }
                            
                            Drive gripDrive = grip.GetComponent<Drive>();
                            if (gripDrive != null)
                            {
                                // CRITICAL: Use ForceStop to completely freeze the grip drive
                                gripDrive.ForceStop = true;
                                gripDrive.Stop();
                                gripDrive.JogForward = false;
                                gripDrive.JogBackward = false;
                                gripDrive.TargetStartMove = false;
                                gripDrive.TargetSpeed = 0;
                            }
                            stopped++;
                        }
                        catch { }
                    }
                    Debug.Log($"[WebGLCommandReceiver] IMMEDIATELY stopped {stopped} grips (synchronous)");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WebGLCommandReceiver] Error in immediate synchronous stop: {ex.Message}");
            }
        }
        
        /// <summary>
        /// IMMEDIATE emergency enforcement - continues enforcement for short period
        /// </summary>
        private IEnumerator ImmediateEmergencyEnforcement(string category = null)
        {
            Debug.Log("[WebGLCommandReceiver] Starting IMMEDIATE emergency enforcement");
            
            // Wait one frame to ensure handler is initialized
            yield return null;
            
            // Note: Immediate synchronous stop already executed above
            // This coroutine just continues enforcement for a short period
            
            // Continue enforcement for 2 seconds to ensure components stay stopped
            float enforcementTime = 2f;
            float elapsed = 0f;
            
            while (elapsed < enforcementTime && EmergencyStopHandler.IsHalted)
            {
                yield return new WaitForSeconds(0.1f); // Every 100ms
                elapsed += 0.1f;
                
                // Re-enforce stop
                try
                {
                    Drive[] drives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                    if (drives != null)
                    {
                        for (int i = 0; i < drives.Length; i++)
                        {
                            Drive d = drives[i];
                            if (d != null && d.gameObject != null)
                            {
                                // CRITICAL: Use ForceStop to completely freeze the drive
                                d.ForceStop = true;
                                d.Stop();
                                d.JogForward = false;
                                d.JogBackward = false;
                                d.TargetStartMove = false;
                            }
                        }
                    }
                    
                    Source[] sources = FindObjectsByType<Source>(FindObjectsSortMode.None);
                    if (sources != null)
                    {
                        for (int i = 0; i < sources.Length; i++)
                        {
                            Source s = sources[i];
                            if (s != null && s.gameObject != null)
                            {
                                s.Enabled = false;
                                s.GenerateMU = false; // Also stop generation flag
                            }
                        }
                    }
                    
                    // Also re-enforce stop on Axis and Grip
                    Axis[] axes = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                    if (axes != null)
                    {
                        for (int i = 0; i < axes.Length; i++)
                        {
                            Axis a = axes[i];
                            if (a != null && a.gameObject != null)
                            {
                                try
                                {
                                    Drive ad = a.GetComponent<Drive>();
                                    if (ad != null)
                                    {
                                        // CRITICAL: Use ForceStop to completely freeze the axis drive
                                        ad.ForceStop = true;
                                        ad.Stop();
                                        ad.JogForward = false;
                                        ad.JogBackward = false;
                                        ad.TargetStartMove = false;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    
                    // CRITICAL FIX: Do NOT call DeActivate(true) - it may release MUs!
                    Grip[] grips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                    if (grips != null)
                    {
                        for (int i = 0; i < grips.Length; i++)
                        {
                            Grip g = grips[i];
                            if (g != null && g.gameObject != null)
                            {
                                try
                                {
                                    // CRITICAL FIX: Just stop operations, don't deactivate the grip!
                                    g.PickObjects = false;
                                    g.PlaceObjects = false;
                                    
                                    // Ensure all gripped MUs stay kinematic and attached
                                    if (g.PickedMUs != null && g.PickedMUs.Count > 0)
                                    {
                                        foreach (GameObject muObj in g.PickedMUs)
                                        {
                                            if (muObj != null)
                                            {
                                                MU mu = muObj.GetComponent<MU>();
                                                if (mu != null)
                                                {
                                                    if (mu.Rigidbody != null)
                                                    {
                                                        mu.Rigidbody.isKinematic = true;
                                                        mu.Rigidbody.velocity = Vector3.zero;
                                                        mu.Rigidbody.angularVelocity = Vector3.zero;
                                                    }
                                                    // Ensure MU stays parented to grip
                                                    if (muObj.transform.parent != g.transform)
                                                    {
                                                        muObj.transform.SetParent(g.transform, true);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    
                                    Drive gd = g.GetComponent<Drive>();
                                    if (gd != null)
                                    {
                                        // CRITICAL: Use ForceStop to completely freeze the grip drive
                                        gd.ForceStop = true;
                                        gd.Stop();
                                        gd.JogForward = false;
                                        gd.JogBackward = false;
                                        gd.TargetStartMove = false;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
            }
            
            Debug.Log("[WebGLCommandReceiver] Immediate enforcement complete - handler should take over");
        }
    }
    
    [System.Serializable]
    public class Command
    {
        public string commandId;
        public string componentId;
        public string category;
        public string parameter;
        public string value; // JSON string that needs to be parsed
        public string timestamp;
        public bool isEmergencyCommand;
        
        // Helper method to check if this is an emergency command (with null safety)
        public bool IsEmergency()
        {
            try
            {
                if (isEmergencyCommand) return true;
                
                if (parameter == null) return false;
                
                if (parameter == "EmergencyStop" || parameter == "Emergency") return true;
                
                if (componentId == "all" && !string.IsNullOrEmpty(parameter))
                {
                    string paramLower = parameter.ToLower();
                    if (paramLower.Contains("emergency")) return true;
                }
                
                return false;
            }
            catch
            {
                // If any error occurs, default to false
                return false;
            }
        }
    }
    
    [System.Serializable]
    public class CommandListResponse
    {
        public Command[] commands;
    }
    
    [System.Serializable]
    public class CommandResponse
    {
        public bool success;
        public ComponentState state;
        public string message;
        public string error;
    }
    
    [System.Serializable]
    public class EmergencyStopData
    {
        public string reason;
        public string category;
    }
}

