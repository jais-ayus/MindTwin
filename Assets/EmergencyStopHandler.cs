using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Handles emergency stop functionality for the digital twin
    /// </summary>
    public class EmergencyStopHandler : MonoBehaviour
    {
        public static bool IsHalted { get; private set; } = false;
        public static string HaltReason { get; private set; } = null;
        public static string HaltCategory { get; private set; } = null;
        
        private static EmergencyStopHandler instance;
        private static bool isStopping = false; // Prevent multiple simultaneous stops
        private WebGLIoTMonitor iotMonitor;
        private List<Drive> allDrives = new List<Drive>();
        private List<Source> allSources = new List<Source>();
        private List<Axis> allAxes = new List<Axis>(); // For robots/CNC
        private List<Grip> allGrips = new List<Grip>(); // For robots
        private List<PLCDemoCNCLoadUnload> cncControllers = new List<PLCDemoCNCLoadUnload>(); // PLC driven CNC/robot controllers
        private List<IKPath> allIKPaths = new List<IKPath>(); // CRITICAL: Robot path programs - must be frozen to prevent restart
        private ComponentStateStorage stateStorage = new ComponentStateStorage(); // For resume functionality
        
        void Awake()
        {
            // Set static instance
            instance = this;
            Debug.Log("[EmergencyStopHandler] Instance registered");
        }
        
        void Start()
        {
            iotMonitor = GetComponent<WebGLIoTMonitor>();
            if (iotMonitor == null)
            {
                iotMonitor = gameObject.AddComponent<WebGLIoTMonitor>();
            }
            
            // Find all components in scene
            RefreshComponentLists();
            Debug.Log($"[EmergencyStopHandler] Initialized with {allDrives.Count} drives, {allSources.Count} sources, {allAxes.Count} axes, {allGrips.Count} grips");
            
            // Start enforcement coroutine if already halted
            if (IsHalted)
            {
                StartCoroutine(ContinuousEnforcementCoroutine());
                Debug.Log("[EmergencyStopHandler] Started continuous enforcement coroutine (already halted)");
            }
        }
        
        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
        
        /// <summary>
        /// FixedUpdate enforcement - runs at physics timestep to match component updates
        /// CRITICAL: This runs in FixedUpdate() to match when Drive.FixedUpdate() runs
        /// This ensures components are stopped in the same update cycle they check their state
        /// </summary>
        void FixedUpdate()
        {
            // Only enforce if production is halted
            if (!IsHalted)
                return;
            
            // Continuous enforcement - prevent any component from restarting
            // This runs every FixedUpdate to ensure components stay stopped
            EnforceEmergencyStop();
        }
        
        /// <summary>
        /// Coroutine for aggressive continuous enforcement (runs every 0.05 seconds = 20 times per second)
        /// This is MORE aggressive than Update() and ensures components stay stopped
        /// </summary>
        private System.Collections.IEnumerator ContinuousEnforcementCoroutine()
        {
            while (IsHalted)
            {
                // Aggressive enforcement - check and stop components very frequently
                EnforceEmergencyStop();
                
                // Also use direct method as backup
                EnforceEmergencyStopDirect();
                
                yield return new WaitForSeconds(0.05f); // Every 50ms = 20 times per second
            }
        }
        
        void OnEnable()
        {
            // Start enforcement coroutine when enabled
            if (IsHalted)
            {
                StartCoroutine(ContinuousEnforcementCoroutine());
            }
        }
        
        /// <summary>
        /// Continuously enforces emergency stop state - prevents components from restarting
        /// MORE AGGRESSIVE: Always refreshes lists and forces stop on ALL components
        /// </summary>
        private void EnforceEmergencyStop()
        {
            // ALWAYS refresh component lists to catch any new components
            RefreshComponentLists();
            
            int enforcedDrives = 0;
            int enforcedSources = 0;
            
            // Continuously enforce stop on all drives - FORCE STOP REGARDLESS OF STATE
            for (int i = 0; i < allDrives.Count; i++)
            {
                Drive drive = allDrives[i];
                
                if (drive == null || drive.gameObject == null)
                    continue;
                
                // Category filtering (if specific category was halted)
                if (HaltCategory != null && HaltCategory != "all")
                {
                    try
                    {
                        string driveCategory = DetermineDriveCategory(drive);
                        if (driveCategory != HaltCategory)
                            continue;
                    }
                    catch
                    {
                        // Continue anyway if category check fails
                    }
                }
                
                try
                {
                    // AGGRESSIVE ENFORCEMENT - ALWAYS force stop, don't check if running
                    // This ensures components stay stopped even if other scripts try to restart them
                    bool wasRunning = drive.JogForward || drive.JogBackward || drive.TargetStartMove;
                    float currentSpeed = 0f;
                    
                    try
                    {
                        currentSpeed = drive.CurrentSpeed;
                    }
                    catch { }
                    
                    // CRITICAL: Use ForceStop to completely freeze the drive
                    // This bypasses CalcFixedUpdate() and prevents any movement
                    drive.ForceStop = true;
                    
                    // Also call Stop() to set flags
                    drive.Stop();
                    
                    // FORCE STOP - Always set to false, even if already false
                    drive.JogForward = false;
                    drive.JogBackward = false;
                    drive.TargetStartMove = false;
                    
                    // Force speed to 0 (extra safety)
                    try
                    {
                        drive.TargetSpeed = 0;
                    }
                    catch { }
                    
                    if (wasRunning || currentSpeed > 0.01f)
                    {
                        enforcedDrives++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error enforcing stop on drive: {ex.Message}");
                }
            }
            
            // Continuously enforce stop on all sources - FORCE DISABLED REGARDLESS OF STATE
            for (int i = 0; i < allSources.Count; i++)
            {
                Source source = allSources[i];
                
                if (source == null || source.gameObject == null)
                    continue;
                
                // Category filtering
                if (HaltCategory != null && HaltCategory != "all" && HaltCategory != "sources")
                    continue;
                
                try
                {
                    // AGGRESSIVE ENFORCEMENT - ALWAYS force disabled
                    bool wasEnabled = source.Enabled;
                    source.Enabled = false;
                    
                    if (wasEnabled)
                    {
                        enforcedSources++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error enforcing stop on source: {ex.Message}");
                }
            }
            
            // CRITICAL: Stop all Axis components (robots/CNC) - Stop their associated Drive components
            int enforcedAxes = 0;
            for (int i = 0; i < allAxes.Count; i++)
            {
                Axis axis = allAxes[i];
                
                if (axis == null || axis.gameObject == null)
                    continue;
                
                // Category filtering (if specific category was halted)
                if (HaltCategory != null && HaltCategory != "all")
                {
                    try
                    {
                        // Check if axis belongs to robots category
                        if (HaltCategory == "robots")
                        {
                            // Only stop if it's part of a robot (has parentRobot)
                            if (string.IsNullOrEmpty(axis.gameObject.name))
                                continue;
                            
                            // Check if it's part of a robot by checking hierarchy
                            bool isRobotAxis = false;
                            try
                            {
                                Transform parent = axis.transform.parent;
                                int depth = 0;
                                while (parent != null && depth < 5)
                                {
                                    if (parent.name != null && (parent.name.ToLower().Contains("robot") || parent.name.ToLower().Contains("arm")))
                                    {
                                        isRobotAxis = true;
                                        break;
                                    }
                                    parent = parent.parent;
                                    depth++;
                                }
                            }
                            catch { }
                            
                            if (!isRobotAxis)
                                continue;
                        }
                        else if (HaltCategory != "axes" && HaltCategory != "robots")
                        {
                            continue; // Skip if category doesn't match
                        }
                    }
                    catch
                    {
                        // Continue anyway if category check fails
                    }
                }
                
                try
                {
                    // Stop the Axis by stopping its associated Drive component
                    Drive axisDrive = null;
                    try
                    {
                        axisDrive = axis.GetComponent<Drive>();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error getting Drive from Axis: {ex.Message}");
                    }
                    
                    if (axisDrive != null)
                    {
                        // FORCE STOP the drive associated with this axis
                        bool wasRunning = axisDrive.JogForward || axisDrive.JogBackward || axisDrive.TargetStartMove;
                        
                        // Call Stop() first for immediate effect
                        axisDrive.Stop();
                        axisDrive.JogForward = false;
                        axisDrive.JogBackward = false;
                        axisDrive.TargetStartMove = false;
                        
                        try
                        {
                            axisDrive.TargetSpeed = 0;
                        }
                        catch { }
                        
                        if (wasRunning)
                        {
                            enforcedAxes++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error enforcing stop on axis: {ex.Message}");
                }
            }
            
            // CRITICAL: Stop all Grip components (robots) - Stop pick/place operations
            int enforcedGrips = 0;
            for (int i = 0; i < allGrips.Count; i++)
            {
                Grip grip = allGrips[i];
                
                if (grip == null || grip.gameObject == null)
                    continue;
                
                // Category filtering
                if (HaltCategory != null && HaltCategory != "all" && HaltCategory != "robots" && HaltCategory != "grippers")
                    continue;
                
                try
                {
                    // Stop grip operations by setting PickObjects and PlaceObjects to false
                    bool wasPicking = false;
                    bool wasPlacing = false;
                    
                    try
                    {
                        wasPicking = grip.PickObjects;
                        wasPlacing = grip.PlaceObjects;
                        
                        // CRITICAL FIX: DO NOT call DeActivate(true) on grips!
                        // DeActivate may release MUs causing them to drop.
                        // Instead, just set the operation flags to false and freeze MUs.
                        
                        // Force stop pick/place operations WITHOUT deactivating the grip
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
                                        // Keep MU frozen
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
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error accessing Grip properties: {ex.Message}");
                    }
                    
                    // Also stop any associated Drive components (grips might have drives)
                    try
                    {
                        Drive gripDrive = grip.GetComponent<Drive>();
                        if (gripDrive != null)
                        {
                            // CRITICAL: Use ForceStop to completely freeze the grip drive
                            gripDrive.ForceStop = true;
                            gripDrive.Stop();
                            gripDrive.JogForward = false;
                            gripDrive.JogBackward = false;
                            gripDrive.TargetStartMove = false;
                            
                            try
                            {
                                gripDrive.TargetSpeed = 0;
                            }
                            catch { }
                        }
                    }
                    catch { }
                    
                    if (wasPicking || wasPlacing)
                    {
                        enforcedGrips++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error enforcing stop on grip: {ex.Message}");
                }
            }
            
            // CRITICAL: Continuously enforce ForceStop on all drives to prevent any movement
            for (int i = 0; i < allDrives.Count; i++)
            {
                Drive drive = allDrives[i];
                if (drive != null && drive.gameObject != null)
                {
                    try
                    {
                        drive.ForceStop = true; // Keep frozen
                    }
                    catch { }
                }
            }
            
            // CRITICAL FIX: Continuously ensure all gripped MUs stay kinematic and attached
            // DO NOT call DeActivate(true) here - it may cause MUs to be released!
            for (int i = 0; i < allGrips.Count; i++)
            {
                Grip grip = allGrips[i];
                if (grip != null && grip.gameObject != null && grip.PickedMUs != null)
                {
                    try
                    {
                        // Just keep operations stopped, don't deactivate the grip itself
                        grip.PickObjects = false;
                        grip.PlaceObjects = false;
                        
                        // Ensure all gripped MUs stay kinematic and attached to grip
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
                    catch { }
                }
            }
            
            // Log enforcement activity (only if something was enforced, to avoid spam)
            if (enforcedDrives > 0 || enforcedSources > 0 || enforcedAxes > 0 || enforcedGrips > 0)
            {
                Debug.Log($"[EmergencyStopHandler] Enforced stop: {enforcedDrives} drives, {enforcedSources} sources, {enforcedAxes} axes, {enforcedGrips} grips (Frame {Time.frameCount})");
            }

            // Also enforce PLC-driven CNC/robot controllers so their logic halts immediately
            EnforceCncControllerEmergencyState();
            
            // CRITICAL: Enforce ForceStop on all IKPath components (robot path programs)
            // This prevents the robot from continuing its path program while stopped
            EnforceIKPathEmergencyStop();
        }
        
        /// <summary>
        /// CRITICAL: Freezes all IKPath components (robot path programs) during emergency stop
        /// Without this, robot would continue executing its path program and might drop MUs
        /// </summary>
        private void EnforceIKPathEmergencyStop()
        {
            if (allIKPaths == null || allIKPaths.Count == 0)
                return;
            
            for (int i = 0; i < allIKPaths.Count; i++)
            {
                IKPath ikPath = allIKPaths[i];
                if (ikPath == null || ikPath.gameObject == null)
                    continue;
                
                try
                {
                    // CRITICAL: Set ForceStop to freeze the path program
                    // IKPath.FixedUpdate() checks ForceStop and returns immediately if true
                    ikPath.ForceStop = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error enforcing IKPath emergency stop: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// CRITICAL FIX: Reset IKPath to safe state before clearing ForceStop
        /// 
        /// THE PROBLEM: When ForceStop is cleared and PathIsActive is still true,
        /// the robot continues from where it was frozen. If it was mid-movement to a
        /// "Place" target, it will arrive and call OnAtTarget() -> fixer.Place() -> MU drops!
        /// 
        /// THE FIX: Reset PathIsActive to FALSE so the path won't auto-continue.
        /// The PLC state machine will naturally restart the robot program from a safe state.
        /// </summary>
        private void RestoreIKPathStates()
        {
            // First, refresh component lists to ensure we have all IKPaths
            RefreshComponentLists();
            
            if (allIKPaths == null || allIKPaths.Count == 0)
            {
                Debug.Log("[EmergencyStopHandler] No IKPath components found to restore");
                return;
            }
            
            int restoredCount = 0;
            
            for (int i = 0; i < allIKPaths.Count; i++)
            {
                IKPath ikPath = allIKPaths[i];
                if (ikPath == null || ikPath.gameObject == null)
                    continue;
                
                try
                {
                    string pathName = ikPath.gameObject.name;
                    var ikState = stateStorage.GetState<ComponentStateStorage.IKPathState>(pathName + "_IKPath");
                    
                    // CRITICAL FIX: RESET the path to prevent auto-continue to Place target
                    // This is the KEY change that prevents MU from dropping
                    
                    // Step 1: Reset PathIsActive to FALSE
                    // This prevents the path from continuing when ForceStop is cleared
                    ikPath.PathIsActive = false;
                    
                    // Step 2: Reset NumTarget to 0 (start of path)
                    // When PLC restarts the robot program, it will start from a known safe state
                    ikPath.NumTarget = 0;
                    
                    // Step 3: Clear CurrentTarget
                    ikPath.CurrentTarget = null;
                    
                    // Step 4: Reset LinearPathActive in case it was doing linear interpolation
                    ikPath.LinearPathActive = false;
                    ikPath.LinearPathPos = 0;
                    
                    // Step 5: Clear WaitForSignal
                    ikPath.WaitForSignal = false;
                    
                    // Step 6: NOW clear ForceStop - the path is in a safe reset state
                    ikPath.ForceStop = false;
                    
                    if (ikState != null && ikState.wasPathIsActive)
                    {
                        Debug.Log($"[EmergencyStopHandler] RESET IKPath: {pathName} - was at Target#{ikState.numTarget}, now reset to Target#0. Path will restart safely via PLC.");
                    }
                    else
                    {
                        Debug.Log($"[EmergencyStopHandler] RESET IKPath: {pathName} - cleared ForceStop and reset to safe state");
                    }
                    
                    restoredCount++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error restoring IKPath state: {ex.Message}");
                    
                    // Even on error, try to reset and clear ForceStop
                    try
                    {
                        ikPath.PathIsActive = false;
                        ikPath.NumTarget = 0;
                        ikPath.CurrentTarget = null;
                        ikPath.ForceStop = false;
                    }
                    catch { }
                }
            }
            
            Debug.Log($"[EmergencyStopHandler] Reset {restoredCount} IKPath components to safe state");
        }
        
        /// <summary>
        /// Clears ForceStop on all IKPath components (used in final cleanup)
        /// </summary>
        private void ClearAllIKPathForceStops()
        {
            IKPath[] allPaths = null;
            try
            {
                allPaths = FindObjectsByType<IKPath>(FindObjectsSortMode.None);
            }
            catch { return; }
            
            if (allPaths == null) return;
            
            for (int i = 0; i < allPaths.Length; i++)
            {
                if (allPaths[i] != null)
                {
                    try
                    {
                        allPaths[i].ForceStop = false;
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Forces PLC-driven CNC/robot controller logic into emergency state so their internal PLC state machines halt
        /// </summary>
        private void EnforceCncControllerEmergencyState()
        {
            if (cncControllers == null || cncControllers.Count == 0)
                return;

            for (int i = 0; i < cncControllers.Count; i++)
            {
                PLCDemoCNCLoadUnload controller = cncControllers[i];
                if (controller == null)
                    continue;

                try
                {
                    ApplyCncControllerEmergencyState(controller, true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error forcing CNC controller emergency state: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Releases CNC/robot controller override when production resumes
        /// </summary>
        private void ReleaseCncControllers()
        {
            if (cncControllers == null || cncControllers.Count == 0)
                return;

            for (int i = 0; i < cncControllers.Count; i++)
            {
                PLCDemoCNCLoadUnload controller = cncControllers[i];
                if (controller == null)
                    continue;

                try
                {
                    ApplyCncControllerEmergencyState(controller, false);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error releasing CNC controller emergency state: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Applies or releases the emergency override on a specific PLC demo controller
        /// </summary>
        private void ApplyCncControllerEmergencyState(PLCDemoCNCLoadUnload controller, bool isEmergency)
        {
            if (controller == null)
                return;

            if (isEmergency)
            {
                // EMERGENCY MODE: Force everything to stop
                
                // Toggle PLC "buttons" so the scripted PLC logic also sees the emergency
                try
                {
                    if (controller.EmergencyButton != null)
                        controller.EmergencyButton.SetValue(true);
                    if (controller.AutomaticButton != null)
                        controller.AutomaticButton.SetValue(false);
                    if (controller.RobotButton != null)
                        controller.RobotButton.SetValue(false);
                    if (controller.ConveyorInButton != null)
                        controller.ConveyorInButton.SetValue(false);
                    if (controller.ConyeyorOutButton != null)
                        controller.ConyeyorOutButton.SetValue(false);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error toggling PLC input signals: {ex.Message}");
                }

                controller.AutomaticMode = false;
                controller.RobotState = "EmergencyStop";
                controller.MachineState = "EmergencyStop";
                controller.EntryState = "EmergencyStop";
                controller.ExitState = "EmergencyStop";

                // Force all PLC outputs to a safe state
                try { controller.EntryConveyorStart?.SetValue(false); } catch { }
                try { controller.ExitConveyorStart?.SetValue(false); } catch { }
                try { controller.StartLoadingProgramm?.SetValue(false); } catch { }
                try { controller.StartUnloadingProgramm?.SetValue(false); } catch { }
                try { controller.StartMachine?.SetValue(false); } catch { }
                try { controller.MoveToolingWheel?.SetValue(false); } catch { }
                try { controller.OpenDoor?.SetValue(false); } catch { }
                try { controller.StartMachining?.SetValue(false); } catch { }
                try { controller.AutomaticButtonLight?.SetValue(false); } catch { }
                try { controller.RobotLight?.SetValue(false); } catch { }
                try { controller.ConveyorInLight?.SetValue(false); } catch { }
                try { controller.ConveyorOutLight?.SetValue(false); } catch { }
                try { controller.LoadingProgrammIsRunning?.SetValue(false); } catch { }
                try { controller.UnloadingProgrammIsRunning?.SetValue(false); } catch { }
            }
            else
            {
                // RESUME MODE: Restore PLC state from stored values
                string controllerName = controller.gameObject != null ? controller.gameObject.name : "PLCController";
                var plcState = stateStorage.GetState<ComponentStateStorage.PLCControllerState>(controllerName + "_PLC");
                
                // Clear emergency button first
                try
                {
                    if (controller.EmergencyButton != null)
                        controller.EmergencyButton.SetValue(false);
                }
                catch { }
                
                if (plcState != null)
                {
                    // CRITICAL: Restore the state machine states
                    controller.AutomaticMode = plcState.wasAutomaticMode;
                    controller.RobotState = plcState.robotState;
                    controller.MachineState = plcState.machineState;
                    controller.EntryState = plcState.entryState;
                    controller.ExitState = plcState.exitState;
                    
                    // Restore button states
                    try
                    {
                        if (controller.RobotButton != null)
                            controller.RobotButton.SetValue(plcState.wasRobotButton);
                        if (controller.ConveyorInButton != null)
                            controller.ConveyorInButton.SetValue(plcState.wasConveyorInButton);
                        if (controller.ConyeyorOutButton != null)
                            controller.ConyeyorOutButton.SetValue(plcState.wasConveyorOutButton);
                        if (controller.AutomaticButton != null)
                            controller.AutomaticButton.SetValue(plcState.wasAutomaticButton);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring PLC button states: {ex.Message}");
                    }
                    
                    // CRITICAL: Restore OnSwitch FIRST (required for conveyor to work)
                    try
                    {
                        if (controller.OnSwitch != null)
                            controller.OnSwitch.SetValue(plcState.wasOnSwitch);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring OnSwitch: {ex.Message}");
                    }
                    
                    // Restore PLC outputs (EXCEPT robot program signals)
                    try
                    {
                        if (controller.EntryConveyorStart != null)
                            controller.EntryConveyorStart.SetValue(plcState.wasEntryConveyorStart);
                        if (controller.ExitConveyorStart != null)
                            controller.ExitConveyorStart.SetValue(plcState.wasExitConveyorStart);
                        
                        // CRITICAL FIX: Do NOT restore robot program signals to true!
                        // Setting these to true immediately triggers the robot's IKPath program,
                        // which continues to a Place target and drops the MU.
                        // Instead, keep them FALSE and let PLC state machine restart them naturally.
                        if (controller.StartLoadingProgramm != null)
                            controller.StartLoadingProgramm.SetValue(false);
                        if (controller.StartUnloadingProgramm != null)
                            controller.StartUnloadingProgramm.SetValue(false);
                        
                        if (controller.StartMachine != null)
                            controller.StartMachine.SetValue(plcState.wasStartMachine);
                        if (controller.OpenDoor != null)
                            controller.OpenDoor.SetValue(plcState.wasOpenDoor);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring PLC output states: {ex.Message}");
                    }
                    
                    // Restore lights based on button states
                    try
                    {
                        if (controller.AutomaticButtonLight != null)
                            controller.AutomaticButtonLight.SetValue(plcState.wasAutomaticMode);
                        if (controller.RobotLight != null)
                            controller.RobotLight.SetValue(plcState.wasRobotButton);
                        if (controller.ConveyorInLight != null)
                            controller.ConveyorInLight.SetValue(plcState.wasConveyorInButton);
                        if (controller.ConveyorOutLight != null)
                            controller.ConveyorOutLight.SetValue(plcState.wasConveyorOutButton);
                    }
                    catch { }
                    
                    Debug.Log($"[EmergencyStopHandler] Restored PLC state: {controllerName} - AutoMode={plcState.wasAutomaticMode}, Robot={plcState.robotState}, Machine={plcState.machineState}, Entry={plcState.entryState}, Exit={plcState.exitState}");
                }
                else
                {
                    // No stored state - reset to initial state
                    Debug.LogWarning($"[EmergencyStopHandler] No stored PLC state for {controllerName}, resetting to initial state");
                    controller.AutomaticMode = true;
                    controller.RobotState = "WaitingForLoading";
                    controller.MachineState = "Empty";
                    controller.EntryState = "WatingForPart";
                    controller.ExitState = "Empty";
                    
                    // CRITICAL: Ensure OnSwitch is ON (required for conveyor)
                    try
                    {
                        if (controller.OnSwitch != null)
                            controller.OnSwitch.SetValue(true);
                    }
                    catch { }
                    
                    // Re-enable buttons
                    try
                    {
                        if (controller.AutomaticButton != null)
                            controller.AutomaticButton.SetValue(true);
                        if (controller.RobotButton != null)
                            controller.RobotButton.SetValue(true);
                        if (controller.ConveyorInButton != null)
                            controller.ConveyorInButton.SetValue(true);
                        if (controller.ConyeyorOutButton != null)
                            controller.ConyeyorOutButton.SetValue(true);
                        if (controller.AutomaticButtonLight != null)
                            controller.AutomaticButtonLight.SetValue(true);
                    }
                    catch { }
                    
                    // CRITICAL: Ensure robot program signals are OFF
                    try
                    {
                        if (controller.StartLoadingProgramm != null)
                            controller.StartLoadingProgramm.SetValue(false);
                        if (controller.StartUnloadingProgramm != null)
                            controller.StartUnloadingProgramm.SetValue(false);
                    }
                    catch { }
                }
            }
        }
        
        /// <summary>
        /// Static version of continuous enforcement (fallback if instance not available)
        /// MORE AGGRESSIVE: Always finds and stops ALL components
        /// </summary>
        private static void EnforceEmergencyStopDirect()
        {
            if (!IsHalted)
                return;
            
            try
            {
                // Find all drives - ALWAYS refresh to catch new components
                Drive[] allDrives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                if (allDrives != null)
                {
                    for (int i = 0; i < allDrives.Length; i++)
                    {
                        Drive drive = allDrives[i];
                        if (drive == null || drive.gameObject == null)
                            continue;
                        
                        // Category filtering
                        if (HaltCategory != null && HaltCategory != "all")
                        {
                            try
                            {
                                string driveCategory = DetermineDriveCategoryStatic(drive);
                                if (driveCategory != HaltCategory)
                                    continue;
                            }
                            catch { }
                        }
                        
                        try
                        {
                            // CRITICAL: Use ForceStop to completely freeze the drive
                            drive.ForceStop = true;
                            drive.Stop();
                            drive.JogForward = false;
                            drive.JogBackward = false;
                            drive.TargetStartMove = false;
                            
                            try
                            {
                                drive.TargetSpeed = 0;
                            }
                            catch { }
                        }
                        catch { }
                    }
                }
                
                // Find all sources - ALWAYS refresh
                Source[] allSources = FindObjectsByType<Source>(FindObjectsSortMode.None);
                if (allSources != null)
                {
                    for (int i = 0; i < allSources.Length; i++)
                    {
                        Source source = allSources[i];
                        if (source == null || source.gameObject == null)
                            continue;
                        
                        // Category filtering
                        if (HaltCategory != null && HaltCategory != "all" && HaltCategory != "sources")
                            continue;
                        
                try
                {
                    // AGGRESSIVE: Always force disabled
                    source.Enabled = false;
                    // Also stop generation flag to prevent one more MU from being generated
                    source.GenerateMU = false;
                }
                catch { }
                    }
                }
                
                // CRITICAL: Stop all Axis components (robots/CNC)
                Axis[] allAxes = null;
                try
                {
                    allAxes = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                }
                catch { }
                
                if (allAxes != null)
                {
                    for (int i = 0; i < allAxes.Length; i++)
                    {
                        Axis axis = allAxes[i];
                        if (axis == null || axis.gameObject == null)
                            continue;
                        
                        try
                        {
                            // Stop the Axis by stopping its associated Drive
                            Drive axisDrive = null;
                            try
                            {
                                axisDrive = axis.GetComponent<Drive>();
                            }
                            catch { }
                            
                            if (axisDrive != null)
                            {
                                // CRITICAL: Use ForceStop to completely freeze the axis drive
                                axisDrive.ForceStop = true;
                                axisDrive.Stop();
                                axisDrive.JogForward = false;
                                axisDrive.JogBackward = false;
                                axisDrive.TargetStartMove = false;
                                
                                try
                                {
                                    axisDrive.TargetSpeed = 0;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                
                // CRITICAL: Stop all Grip components (robots)
                Grip[] allGrips = null;
                try
                {
                    allGrips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                }
                catch { }
                
                if (allGrips != null)
                {
                    for (int i = 0; i < allGrips.Length; i++)
                    {
                        Grip grip = allGrips[i];
                        if (grip == null || grip.gameObject == null)
                            continue;
                        
                        try
                        {
                            // CRITICAL: DO NOT release gripped MUs - freeze them in place!
                            // Deactivate the grip to prevent any new operations, but keep MUs gripped
                            try
                            {
                                grip.DeActivate(true); // Disable grip logic without releasing MUs
                                
                                // Ensure all gripped MUs stay kinematic (frozen)
                                if (grip.PickedMUs != null && grip.PickedMUs.Count > 0)
                                {
                                    foreach (GameObject muObj in grip.PickedMUs)
                                    {
                                        if (muObj != null)
                                        {
                                            MU mu = muObj.GetComponent<MU>();
                                            if (mu != null && mu.Rigidbody != null)
                                            {
                                                mu.Rigidbody.isKinematic = true; // Keep frozen
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                            
                            // Stop grip operations
                            grip.PickObjects = false;
                            grip.PlaceObjects = false;
                            
                            // Also stop associated Drive if exists
                            Drive gripDrive = null;
                            try
                            {
                                gripDrive = grip.GetComponent<Drive>();
                            }
                            catch { }
                            
                            if (gripDrive != null)
                            {
                                // CRITICAL: Use ForceStop to completely freeze the grip drive
                                gripDrive.ForceStop = true;
                                gripDrive.Stop();
                                gripDrive.JogForward = false;
                                gripDrive.JogBackward = false;
                                gripDrive.TargetStartMove = false;
                                
                                try
                                {
                                    gripDrive.TargetSpeed = 0;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in direct enforcement: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Triggers emergency stop for all components or specific category
        /// CRITICAL: Ensures handler exists and starts enforcement
        /// </summary>
        public static void TriggerEmergencyStop(string reason, string category = null)
        {
            // Prevent multiple simultaneous stops
            if (isStopping)
            {
                Debug.LogWarning("[EmergencyStopHandler] Emergency stop already in progress, ignoring duplicate call");
                return;
            }
            
            isStopping = true;
            
            Debug.Log($"[EmergencyStopHandler] ========== EMERGENCY STOP TRIGGERED ==========");
            Debug.Log($"[EmergencyStopHandler] Reason: {reason}");
            Debug.Log($"[EmergencyStopHandler] Category: {category ?? "All"}");
            
            // CRITICAL: Ensure handler exists in scene
            EnsureHandlerExists();
            
            // CRITICAL: Store all component states BEFORE stopping (for resume functionality)
            if (instance != null)
            {
                instance.StoreAllComponentStates();
                Debug.Log($"[EmergencyStopHandler] Stored states for {instance.stateStorage.Count} components");
            }
            
            // Set halted state
            IsHalted = true;
            HaltReason = reason;
            HaltCategory = category;
            
            // Always use direct method for reliability (works even without instance)
            StopAllComponentsDirect(category);
            
            // Also try instance method if available (for better performance)
            if (instance != null)
            {
                // Force PLC logic into emergency state immediately before any Update loop runs
                instance.EnforceCncControllerEmergencyState();

                instance.StopAllComponents(category);
                // Start continuous enforcement coroutine
                instance.StartCoroutine(instance.ContinuousEnforcementCoroutine());
            }
            else
            {
                // Fallback: Use static direct enforcement
                Debug.LogWarning("[EmergencyStopHandler] No instance found, using direct enforcement only");
            }
            
            isStopping = false;
            Debug.Log("[EmergencyStopHandler] ========== EMERGENCY STOP COMPLETE ==========");
        }
        
        /// <summary>
        /// Ensures EmergencyStopHandler exists in the scene (auto-creates if missing)
        /// </summary>
        private static void EnsureHandlerExists()
        {
            if (instance != null && instance.gameObject != null && instance.gameObject.activeInHierarchy)
            {
                return; // Already exists and active
            }
            
            // Try to find existing handler
            EmergencyStopHandler existing = FindFirstObjectByType<EmergencyStopHandler>();
            if (existing != null)
            {
                instance = existing;
                if (!existing.gameObject.activeInHierarchy)
                {
                    existing.gameObject.SetActive(true);
                }
                Debug.Log("[EmergencyStopHandler] Found existing handler in scene");
                return;
            }
            
            // Create new handler GameObject
            GameObject handlerObj = new GameObject("EmergencyStopHandler");
            instance = handlerObj.AddComponent<EmergencyStopHandler>();
            DontDestroyOnLoad(handlerObj); // Persist across scenes
            Debug.Log("[EmergencyStopHandler] Created new handler GameObject");
        }
        
        /// <summary>
        /// Resumes production after emergency stop
        /// CRITICAL: Restores component states and stops enforcement
        /// </summary>
        public static void ResumeProduction()
        {
            if (!IsHalted)
            {
                Debug.LogWarning("[EmergencyStopHandler] Production is not halted, cannot resume");
                return;
            }
            
            Debug.Log("[EmergencyStopHandler] ========== RESUMING PRODUCTION ==========");
            
            // CRITICAL: Clear halted state FIRST to stop FixedUpdate() enforcement immediately
            // This prevents re-freezing during restoration
            IsHalted = false;
            HaltReason = null;
            HaltCategory = null;
            
            // Stop all enforcement coroutines immediately
            if (instance != null && instance.gameObject != null)
            {
                try
                {
                    // Stop the continuous enforcement coroutine
                    instance.StopAllCoroutines();
                    Debug.Log("[EmergencyStopHandler] Stopped all enforcement coroutines");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error stopping coroutines: {ex.Message}");
                }
            }
            
            // CRITICAL: Restore all component states AFTER clearing halted state
            // Do immediate synchronous restoration first, then use coroutine for final sync
            if (instance != null && instance.gameObject != null)
            {
                try
                {
                    // CRITICAL: Refresh component lists first to ensure we have all components
                    instance.RefreshComponentLists();
                    Debug.Log($"[EmergencyStopHandler] Refreshed component lists: {instance.allDrives.Count} drives, {instance.allAxes.Count} axes, {instance.allGrips.Count} grips");
                    
                    // IMMEDIATE: Clear all ForceStops synchronously (no delay)
                    // NOTE: DO NOT reactivate grips here - keep them deactivated to prevent MU drops!
                    instance.ClearAllForceStops();
                    // instance.ReactivateAllGrips(); // REMOVED - grips stay deactivated until safe
                    Debug.Log("[EmergencyStopHandler] Immediately cleared all ForceStops (grips kept deactivated)");
                    
                    // IMMEDIATE: Restore drive/axis states synchronously
                    instance.RestoreDriveAndAxisStates();
                    Debug.Log("[EmergencyStopHandler] Immediately restored drive/axis states");
                    
                    // IMMEDIATE: Restore grip states synchronously
                    instance.RestoreGripStatesSafely();
                    Debug.Log("[EmergencyStopHandler] Immediately restored grip states");
                    
                    // IMMEDIATE: Re-trigger movements synchronously (first pass)
                    instance.ReTriggerDriveMovements();
                    Debug.Log("[EmergencyStopHandler] Immediately re-triggered drive movements");
                    
                    // CRITICAL: Release PLC controllers to restore robot/CNC state machines
                    instance.ReleaseCncControllers();
                    Debug.Log("[EmergencyStopHandler] Released PLC controllers and restored state machines");
                    
                    // CRITICAL FIX: RESET IKPath to safe state (not continue from where it was!)
                    // This prevents robot from continuing to a Place target and dropping MU
                    // The PLC will naturally restart the robot program from a safe state
                    instance.RestoreIKPathStates();
                    Debug.Log("[EmergencyStopHandler] Reset IKPath to safe state - robot will restart via PLC");
                    
                    // CRITICAL FIX FOR CONVEYORS: Force all DriveBehaviors to re-sync signals
                    // This ensures conveyors resume by re-reading their PLC signals
                    instance.ForceDriveBehaviorSync();
                    Debug.Log("[EmergencyStopHandler] Forced DriveBehavior sync - conveyors should resume");
                    
                    // Then start coroutine for final sync and cleanup
                    instance.StartCoroutine(instance.FinalResumeSyncCoroutine());
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error during immediate restoration: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Debug.LogError("[EmergencyStopHandler] CRITICAL: No instance found for resume! Cannot restore states.");
            }
            
            Debug.Log("[EmergencyStopHandler] ========== PRODUCTION RESUMED ==========");
            Debug.Log("[EmergencyStopHandler] Components restored to previous state and can now operate normally");
        }
        
        /// <summary>
        /// Stops all components or components in specific category
        /// SAFE: All operations are null-checked
        /// </summary>
        private void StopAllComponents(string category = null)
        {
            RefreshComponentLists();
            
            int stoppedDrives = 0;
            int stoppedSources = 0;
            int stoppedAxes = 0;
            int stoppedGrips = 0;
            
            // Stop all drives
            for (int i = 0; i < allDrives.Count; i++)
            {
                Drive drive = allDrives[i];
                if (drive == null || drive.gameObject == null)
                    continue;
                
                if (category != null && category != "all")
                {
                    try
                    {
                        // Check if drive belongs to category
                        string driveCategory = DetermineDriveCategory(drive);
                        if (driveCategory != category)
                            continue;
                    }
                    catch
                    {
                        // Continue anyway if category check fails
                    }
                }
                
                try
                {
                    // CRITICAL: Use ForceStop to completely freeze the drive
                    drive.ForceStop = true;
                    drive.Stop();
                    drive.JogForward = false;
                    drive.JogBackward = false;
                    drive.TargetStartMove = false;
                    stoppedDrives++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error stopping drive: {ex.Message}");
                }
            }
            
            // Stop all sources (disable material generation)
            for (int i = 0; i < allSources.Count; i++)
            {
                Source source = allSources[i];
                if (source == null || source.gameObject == null)
                    continue;
                
                if (category != null && category != "all")
                {
                    // Sources are in "sources" category
                    if (category != "sources")
                        continue;
                }
                
                try
                {
                    source.Enabled = false;
                    source.GenerateMU = false; // Also stop generation flag
                    stoppedSources++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error stopping source: {ex.Message}");
                }
            }
            
            // CRITICAL: Stop all Axis components (robots/CNC)
            for (int i = 0; i < allAxes.Count; i++)
            {
                Axis axis = allAxes[i];
                if (axis == null || axis.gameObject == null)
                    continue;
                
                try
                {
                    // Stop the Axis by stopping its associated Drive
                    Drive axisDrive = null;
                    try
                    {
                        axisDrive = axis.GetComponent<Drive>();
                    }
                    catch { }
                    
                    if (axisDrive != null)
                    {
                        // CRITICAL: Use ForceStop to completely freeze the axis drive
                        axisDrive.ForceStop = true;
                        axisDrive.Stop();
                        axisDrive.JogForward = false;
                        axisDrive.JogBackward = false;
                        axisDrive.TargetStartMove = false;
                        
                        try
                        {
                            axisDrive.TargetSpeed = 0;
                        }
                        catch { }
                        
                        stoppedAxes++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error stopping axis: {ex.Message}");
                }
            }
            
            // CRITICAL: Stop all Grip components (robots)
            for (int i = 0; i < allGrips.Count; i++)
            {
                Grip grip = allGrips[i];
                if (grip == null || grip.gameObject == null)
                    continue;
                
                try
                {
                    // CRITICAL: DO NOT release gripped MUs - freeze them in place!
                    // Deactivate the grip to prevent any new operations, but keep MUs gripped
                    try
                    {
                        grip.DeActivate(true); // Disable grip logic without releasing MUs
                        
                        // Ensure all gripped MUs stay kinematic (frozen)
                        if (grip.PickedMUs != null && grip.PickedMUs.Count > 0)
                        {
                            foreach (GameObject muObj in grip.PickedMUs)
                            {
                                if (muObj != null)
                                {
                                    MU mu = muObj.GetComponent<MU>();
                                    if (mu != null && mu.Rigidbody != null)
                                    {
                                        mu.Rigidbody.isKinematic = true; // Keep frozen
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                    
                    // Stop grip operations
                    grip.PickObjects = false;
                    grip.PlaceObjects = false;
                    
                    // Also stop associated Drive if exists
                    Drive gripDrive = null;
                    try
                    {
                        gripDrive = grip.GetComponent<Drive>();
                    }
                    catch { }
                    
                    if (gripDrive != null)
                    {
                        // CRITICAL: Use ForceStop to completely freeze the grip drive
                        gripDrive.ForceStop = true;
                        gripDrive.Stop();
                        gripDrive.JogForward = false;
                        gripDrive.JogBackward = false;
                        gripDrive.TargetStartMove = false;
                        
                        try
                        {
                            gripDrive.TargetSpeed = 0;
                        }
                        catch { }
                    }
                    
                    stoppedGrips++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error stopping grip: {ex.Message}");
                }
            }
            
            // CRITICAL: Stop all IKPath components (robot path programs)
            // This prevents robots from continuing their path programs
            int stoppedIKPaths = 0;
            for (int i = 0; i < allIKPaths.Count; i++)
            {
                IKPath ikPath = allIKPaths[i];
                if (ikPath == null || ikPath.gameObject == null)
                    continue;
                
                try
                {
                    // CRITICAL: Set ForceStop to freeze the path program
                    ikPath.ForceStop = true;
                    stoppedIKPaths++;
                    
                    if (ikPath.PathIsActive)
                    {
                        Debug.Log($"[EmergencyStopHandler] Stopped ACTIVE IKPath: {ikPath.gameObject.name} at Target#{ikPath.NumTarget}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Error stopping IKPath: {ex.Message}");
                }
            }
            
            Debug.Log($"[EmergencyStopHandler] Emergency stop activated: {HaltReason}. Stopped {stoppedDrives} drives, {stoppedSources} sources, {stoppedAxes} axes, {stoppedGrips} grips, {stoppedIKPaths} IKPaths.");
        }
        
        /// <summary>
        /// Fallback method to stop components without instance (ALWAYS WORKS)
        /// </summary>
        private static void StopAllComponentsDirect(string category = null)
        {
            Debug.Log("[EmergencyStopHandler] Executing direct stop (no instance required)");
            
            // Find all drives (with null safety)
            Drive[] allDrives = null;
            try
            {
                allDrives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error finding drives: {ex.Message}");
                allDrives = new Drive[0]; // Empty array to prevent null reference
            }
            
            int stoppedDrives = 0;
            int skippedDrives = 0;
            
            if (allDrives == null)
            {
                Debug.LogWarning("[EmergencyStopHandler] allDrives array is null");
                allDrives = new Drive[0];
            }
            
            Debug.Log($"[EmergencyStopHandler] Found {allDrives.Length} drives in scene");
            
            for (int i = 0; i < allDrives.Length; i++)
            {
                Drive drive = allDrives[i];
                
                // Null check
                if (drive == null)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Drive at index {i} is null, skipping");
                    continue;
                }
                
                // Check if GameObject is valid
                if (drive.gameObject == null)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Drive GameObject is null at index {i}, skipping");
                    continue;
                }
                
                // Category filtering
                if (category != null && category != "all")
                {
                    try
                    {
                        string driveCategory = DetermineDriveCategoryStatic(drive);
                        if (driveCategory != category)
                        {
                            skippedDrives++;
                            continue;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error determining category: {ex.Message}");
                        // Continue anyway - stop all drives if category check fails
                    }
                }
                
                try
                {
                    // CRITICAL: Use ForceStop to completely freeze the drive
                    drive.ForceStop = true;
                    drive.Stop();
                    
                    // Force stop - set all movement flags to false
                    drive.JogForward = false;
                    drive.JogBackward = false;
                    drive.TargetStartMove = false;
                    
                    // Additional safety: Set speed to 0
                    try
                    {
                        drive.TargetSpeed = 0;
                    }
                    catch
                    {
                        // Speed property might not be accessible, ignore
                    }
                    
                    stoppedDrives++;
                    string driveName = drive.gameObject != null ? drive.gameObject.name : "Unknown";
                    Debug.Log($"[EmergencyStopHandler] Stopped drive: {driveName}");
                }
                catch (System.Exception ex)
                {
                    string driveName = drive.gameObject != null ? drive.gameObject.name : "Unknown";
                    Debug.LogError($"[EmergencyStopHandler] Error stopping drive {driveName}: {ex.Message}");
                }
            }
            
            // Stop all sources (with null safety)
            Source[] allSources = null;
            try
            {
                allSources = FindObjectsByType<Source>(FindObjectsSortMode.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error finding sources: {ex.Message}");
                allSources = new Source[0]; // Empty array
            }
            
            int stoppedSources = 0;
            
            if (allSources == null)
            {
                Debug.LogWarning("[EmergencyStopHandler] allSources array is null");
                allSources = new Source[0];
            }
            
            Debug.Log($"[EmergencyStopHandler] Found {allSources.Length} sources in scene");
            
            for (int i = 0; i < allSources.Length; i++)
            {
                Source source = allSources[i];
                
                // Null check
                if (source == null)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Source at index {i} is null, skipping");
                    continue;
                }
                
                // Check if GameObject is valid
                if (source.gameObject == null)
                {
                    Debug.LogWarning($"[EmergencyStopHandler] Source GameObject is null at index {i}, skipping");
                    continue;
                }
                
                if (category == null || category == "all" || category == "sources")
                {
                    try
                    {
                        source.Enabled = false;
                        source.GenerateMU = false; // Also stop generation flag
                        stoppedSources++;
                        string sourceName = source.gameObject != null ? source.gameObject.name : "Unknown";
                        Debug.Log($"[EmergencyStopHandler] Disabled source: {sourceName}");
                    }
                    catch (System.Exception ex)
                    {
                        string sourceName = source.gameObject != null ? source.gameObject.name : "Unknown";
                        Debug.LogError($"[EmergencyStopHandler] Error disabling source {sourceName}: {ex.Message}");
                    }
                }
            }
            
            // CRITICAL: Stop all IKPath components (robot path programs) - Direct method
            IKPath[] allIKPaths = null;
            int stoppedIKPaths = 0;
            try
            {
                allIKPaths = FindObjectsByType<IKPath>(FindObjectsSortMode.None);
            }
            catch { allIKPaths = new IKPath[0]; }
            
            if (allIKPaths != null)
            {
                for (int i = 0; i < allIKPaths.Length; i++)
                {
                    IKPath ikPath = allIKPaths[i];
                    if (ikPath == null || ikPath.gameObject == null)
                        continue;
                    
                    try
                    {
                        ikPath.ForceStop = true;
                        stoppedIKPaths++;
                        
                        if (ikPath.PathIsActive)
                        {
                            Debug.Log($"[EmergencyStopHandler] Stopped ACTIVE IKPath (direct): {ikPath.gameObject.name} at Target#{ikPath.NumTarget}");
                        }
                    }
                    catch { }
                }
            }
            
            Debug.Log($"[EmergencyStopHandler] ========== STOP SUMMARY ==========");
            Debug.Log($"[EmergencyStopHandler] Stopped Drives: {stoppedDrives}");
            Debug.Log($"[EmergencyStopHandler] Skipped Drives: {skippedDrives}");
            Debug.Log($"[EmergencyStopHandler] Stopped Sources: {stoppedSources}");
            Debug.Log($"[EmergencyStopHandler] Stopped IKPaths: {stoppedIKPaths}");
            Debug.Log($"[EmergencyStopHandler] Total Components Stopped: {stoppedDrives + stoppedSources + stoppedIKPaths}");
        }
        
        /// <summary>
        /// Refreshes lists of all controllable components
        /// SAFE: All operations are null-checked and wrapped in try-catch
        /// </summary>
        private void RefreshComponentLists()
        {
            try
            {
                allDrives.Clear();
                Drive[] drives = null;
                try
                {
                    drives = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error finding drives: {ex.Message}");
                    drives = new Drive[0];
                }
                
                if (drives != null)
                {
                    for (int i = 0; i < drives.Length; i++)
                    {
                        if (drives[i] != null)
                        {
                            allDrives.Add(drives[i]);
                        }
                    }
                }
                
                allSources.Clear();
                Source[] sources = null;
                try
                {
                    sources = FindObjectsByType<Source>(FindObjectsSortMode.None);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error finding sources: {ex.Message}");
                    sources = new Source[0];
                }
                
                if (sources != null)
                {
                    for (int i = 0; i < sources.Length; i++)
                    {
                        if (sources[i] != null)
                        {
                            allSources.Add(sources[i]);
                        }
                    }
                }
                
                // CRITICAL: Also find all Axis and Grip components (for robots/CNC)
                allAxes.Clear();
                Axis[] axes = null;
                try
                {
                    axes = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error finding axes: {ex.Message}");
                    axes = new Axis[0];
                }
                
                if (axes != null)
                {
                    for (int i = 0; i < axes.Length; i++)
                    {
                        if (axes[i] != null && axes[i].gameObject != null)
                        {
                            allAxes.Add(axes[i]);
                        }
                    }
                }
                
                allGrips.Clear();
                Grip[] grips = null;
                try
                {
                    grips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error finding grips: {ex.Message}");
                    grips = new Grip[0];
                }
                
                if (grips != null)
                {
                    for (int i = 0; i < grips.Length; i++)
                    {
                        if (grips[i] != null && grips[i].gameObject != null)
                        {
                            allGrips.Add(grips[i]);
                        }
                    }
                }

                // Track PLC-based CNC controllers so we can toggle their logic
                cncControllers.Clear();
                PLCDemoCNCLoadUnload[] controllers = null;
                try
                {
                    controllers = FindObjectsByType<PLCDemoCNCLoadUnload>(FindObjectsSortMode.None);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error finding PLCDemoCNCLoadUnload controllers: {ex.Message}");
                    controllers = new PLCDemoCNCLoadUnload[0];
                }

                if (controllers != null)
                {
                    for (int i = 0; i < controllers.Length; i++)
                    {
                        if (controllers[i] != null)
                        {
                            cncControllers.Add(controllers[i]);
                        }
                    }
                }
                
                // CRITICAL: Find all IKPath components (robot path programs)
                // These MUST be frozen during emergency stop to prevent robot restarting from Target 0
                allIKPaths.Clear();
                IKPath[] ikPaths = null;
                try
                {
                    ikPaths = FindObjectsByType<IKPath>(FindObjectsSortMode.None);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EmergencyStopHandler] Error finding IKPath components: {ex.Message}");
                    ikPaths = new IKPath[0];
                }
                
                if (ikPaths != null)
                {
                    for (int i = 0; i < ikPaths.Length; i++)
                    {
                        if (ikPaths[i] != null && ikPaths[i].gameObject != null)
                        {
                            allIKPaths.Add(ikPaths[i]);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] CRITICAL ERROR in RefreshComponentLists: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Determines the category of a drive component
        /// </summary>
        private string DetermineDriveCategory(Drive drive)
        {
            return DetermineDriveCategoryStatic(drive);
        }
        
        /// <summary>
        /// Static version for use without instance (with null safety)
        /// </summary>
        private static string DetermineDriveCategoryStatic(Drive drive)
        {
            if (drive == null) return "drives";
            
            try
            {
                if (drive.gameObject == null) return "drives";
                
                string name = drive.gameObject.name != null ? drive.gameObject.name.ToLower() : "";
                bool hasTransport = false;
                
                try
                {
                    hasTransport = drive.GetComponent<TransportSurface>() != null ||
                                   drive.GetComponentInChildren<TransportSurface>() != null ||
                                   drive.GetComponentInParent<TransportSurface>() != null;
                }
                catch
                {
                    // Component access failed, continue without transport check
                }
                
                if (!string.IsNullOrEmpty(name))
                {
                    if (name.Contains("conveyor") || name.Contains("belt") || name.Contains("transport") || hasTransport)
                        return "conveyors";
                }
                
                return "drives";
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EmergencyStopHandler] Error determining drive category: {ex.Message}");
                return "drives"; // Default fallback
            }
        }
        
        /// <summary>
        /// Stores all component states before emergency stop (for resume functionality)
        /// </summary>
        private void StoreAllComponentStates()
        {
            try
            {
                // Clear any existing states
                stateStorage.ClearStates();
                
                // Refresh component lists to ensure we have all components
                RefreshComponentLists();
                
                int storedCount = 0;
                
                // Get Drive type for reflection to access internal fields
                Type driveType = typeof(Drive);
                FieldInfo isDrivingToTargetField = driveType.GetField("_isdrivingtotarget", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo currentDestinationField = driveType.GetField("_currentdestination", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // Store drive states - INCLUDING INTERNAL STATE
                for (int i = 0; i < allDrives.Count; i++)
                {
                    Drive drive = allDrives[i];
                    if (drive == null || drive.gameObject == null)
                        continue;
                    
                    try
                    {
                        // Get internal state via reflection
                        bool isDrivingToTarget = false;
                        float currentDestination = drive.TargetPosition;
                        
                        if (isDrivingToTargetField != null)
                        {
                            isDrivingToTarget = (bool)isDrivingToTargetField.GetValue(drive);
                        }
                        if (currentDestinationField != null)
                        {
                            currentDestination = (float)currentDestinationField.GetValue(drive);
                        }
                        
                        var state = new ComponentStateStorage.DriveState
                        {
                            // Public flags
                            wasJogForward = drive.JogForward,
                            wasJogBackward = drive.JogBackward,
                            wasTargetStartMove = drive.TargetStartMove,
                            targetSpeed = drive.TargetSpeed,
                            targetPosition = drive.TargetPosition,
                            speedOverride = drive.SpeedOverride,
                            
                            // CRITICAL: Internal state - this is what ACTUALLY matters for resume
                            wasIsDrivingToTarget = isDrivingToTarget,
                            currentPosition = drive.CurrentPosition,
                            currentSpeed = drive.CurrentSpeed,
                            currentDestination = currentDestination,
                            wasStopped = drive.IsStopped
                        };
                        stateStorage.StoreState(drive.gameObject.name, state);
                        
                        // Also store ForceStop state
                        var forceStopState = new ComponentStateStorage.DriveForceStopState
                        {
                            wasForceStop = drive.ForceStop
                        };
                        stateStorage.StoreState(drive.gameObject.name + "_ForceStop", forceStopState);
                        
                        // Log if drive was actively moving (important for debugging)
                        if (isDrivingToTarget || drive.JogForward || drive.JogBackward)
                        {
                            Debug.Log($"[EmergencyStopHandler] Stored ACTIVE drive: {drive.gameObject.name} - IsDrivingToTarget={isDrivingToTarget}, JogF={drive.JogForward}, JogB={drive.JogBackward}, Pos={drive.CurrentPosition}, Dest={currentDestination}");
                        }
                        storedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error storing drive state: {ex.Message}");
                    }
                }
                
                // Store source states
                for (int i = 0; i < allSources.Count; i++)
                {
                    Source source = allSources[i];
                    if (source == null || source.gameObject == null)
                        continue;
                    
                    try
                    {
                        var state = new ComponentStateStorage.SourceState
                        {
                            wasEnabled = source.Enabled,
                            wasGenerateMU = source.GenerateMU
                        };
                        stateStorage.StoreState(source.gameObject.name, state);
                        storedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error storing source state: {ex.Message}");
                    }
                }
                
                // Store axis states (via their associated drives) - INCLUDING INTERNAL STATE
                for (int i = 0; i < allAxes.Count; i++)
                {
                    Axis axis = allAxes[i];
                    if (axis == null || axis.gameObject == null)
                        continue;
                    
                    try
                    {
                        Drive axisDrive = axis.GetComponent<Drive>();
                        if (axisDrive != null)
                        {
                            // Get internal state via reflection
                            bool isDrivingToTarget = false;
                            float currentDestination = axisDrive.TargetPosition;
                            
                            if (isDrivingToTargetField != null)
                            {
                                isDrivingToTarget = (bool)isDrivingToTargetField.GetValue(axisDrive);
                            }
                            if (currentDestinationField != null)
                            {
                                currentDestination = (float)currentDestinationField.GetValue(axisDrive);
                            }
                            
                            var state = new ComponentStateStorage.AxisState
                            {
                                wasTargetStartMove = axisDrive.TargetStartMove,
                                targetPosition = axisDrive.TargetPosition,
                                wasIsDrivingToTarget = isDrivingToTarget,
                                currentPosition = axisDrive.CurrentPosition,
                                currentDestination = currentDestination
                            };
                            stateStorage.StoreState(axis.gameObject.name, state);
                            
                            // Also store ForceStop state for axis drive
                            var forceStopState = new ComponentStateStorage.DriveForceStopState
                            {
                                wasForceStop = axisDrive.ForceStop
                            };
                            stateStorage.StoreState(axis.gameObject.name + "_AxisDrive_ForceStop", forceStopState);
                            
                            // Log if axis was actively moving
                            if (isDrivingToTarget)
                            {
                                Debug.Log($"[EmergencyStopHandler] Stored ACTIVE axis: {axis.gameObject.name} - IsDrivingToTarget={isDrivingToTarget}, Pos={axisDrive.CurrentPosition}, Dest={currentDestination}");
                            }
                            storedCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error storing axis state: {ex.Message}");
                    }
                }
                
                // Store grip states
                for (int i = 0; i < allGrips.Count; i++)
                {
                    Grip grip = allGrips[i];
                    if (grip == null || grip.gameObject == null)
                        continue;
                    
                    try
                    {
                        // Note: Grip doesn't expose Deactivated property, but we track it via DeActivate calls
                        // We'll store false here since grips are active before emergency stop
                        var state = new ComponentStateStorage.GripState
                        {
                            wasPickObjects = grip.PickObjects,
                            wasPlaceObjects = grip.PlaceObjects,
                            wasDeactivated = false // Grips are active before emergency stop
                        };
                        
                        // Store names of gripped MUs for restoration
                        if (grip.PickedMUs != null && grip.PickedMUs.Count > 0)
                        {
                            foreach (GameObject muObj in grip.PickedMUs)
                            {
                                if (muObj != null)
                                {
                                    state.grippedMUNames.Add(muObj.name);
                                }
                            }
                        }
                        
                        stateStorage.StoreState(grip.gameObject.name, state);
                        
                        // Also store ForceStop state for grip drive if it exists
                        Drive gripDrive = grip.GetComponent<Drive>();
                        if (gripDrive != null)
                        {
                            var forceStopState = new ComponentStateStorage.DriveForceStopState
                            {
                                wasForceStop = gripDrive.ForceStop
                            };
                            stateStorage.StoreState(grip.gameObject.name + "_GripDrive_ForceStop", forceStopState);
                        }
                        storedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error storing grip state: {ex.Message}");
                    }
                }
                
                // CRITICAL: Store PLC controller states - needed for robot/CNC resume
                int plcStoredCount = 0;
                for (int i = 0; i < cncControllers.Count; i++)
                {
                    PLCDemoCNCLoadUnload controller = cncControllers[i];
                    if (controller == null)
                        continue;
                    
                    try
                    {
                        var plcState = new ComponentStateStorage.PLCControllerState
                        {
                            wasAutomaticMode = controller.AutomaticMode,
                            robotState = controller.RobotState,
                            machineState = controller.MachineState,
                            entryState = controller.EntryState,
                            exitState = controller.ExitState,
                            
                            // Button states (read values from PLC inputs)
                            wasOnSwitch = controller.OnSwitch != null && controller.OnSwitch.Value,
                            wasRobotButton = controller.RobotButton != null && controller.RobotButton.Value,
                            wasConveyorInButton = controller.ConveyorInButton != null && controller.ConveyorInButton.Value,
                            wasConveyorOutButton = controller.ConyeyorOutButton != null && controller.ConyeyorOutButton.Value,
                            wasAutomaticButton = controller.AutomaticButton != null && controller.AutomaticButton.Value,
                            
                            // PLC output states
                            wasEntryConveyorStart = controller.EntryConveyorStart != null && controller.EntryConveyorStart.Value,
                            wasExitConveyorStart = controller.ExitConveyorStart != null && controller.ExitConveyorStart.Value,
                            wasStartLoadingProgramm = controller.StartLoadingProgramm != null && controller.StartLoadingProgramm.Value,
                            wasStartUnloadingProgramm = controller.StartUnloadingProgramm != null && controller.StartUnloadingProgramm.Value,
                            wasStartMachine = controller.StartMachine != null && controller.StartMachine.Value,
                            wasOpenDoor = controller.OpenDoor != null && controller.OpenDoor.Value
                        };
                        
                        string controllerName = controller.gameObject != null ? controller.gameObject.name : $"PLCController_{i}";
                        stateStorage.StoreState(controllerName + "_PLC", plcState);
                        plcStoredCount++;
                        
                        Debug.Log($"[EmergencyStopHandler] Stored PLC state: {controllerName} - AutoMode={plcState.wasAutomaticMode}, Robot={plcState.robotState}, Machine={plcState.machineState}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error storing PLC controller state: {ex.Message}");
                    }
                }
                
                // CRITICAL: Store IKPath states - robot path programs
                // Without this, robots would restart from Target 0 instead of continuing from where they were
                int ikPathStoredCount = 0;
                for (int i = 0; i < allIKPaths.Count; i++)
                {
                    IKPath ikPath = allIKPaths[i];
                    if (ikPath == null || ikPath.gameObject == null)
                        continue;
                    
                    try
                    {
                        var ikState = new ComponentStateStorage.IKPathState
                        {
                            wasPathIsActive = ikPath.PathIsActive,
                            wasPathIsFinished = ikPath.PathIsFinished,
                            numTarget = ikPath.NumTarget,
                            currentTargetName = ikPath.CurrentTarget != null ? ikPath.CurrentTarget.gameObject.name : null,
                            lastTargetName = ikPath.LastTarget != null ? ikPath.LastTarget.gameObject.name : null,
                            wasLinearPathActive = ikPath.LinearPathActive,
                            linearPathPos = ikPath.LinearPathPos,
                            wasWaitForSignal = ikPath.WaitForSignal,
                            wasForceStop = ikPath.ForceStop
                        };
                        
                        string pathName = ikPath.gameObject.name;
                        stateStorage.StoreState(pathName + "_IKPath", ikState);
                        ikPathStoredCount++;
                        
                        // Log if path was actively running (critical for debugging)
                        if (ikPath.PathIsActive)
                        {
                            Debug.Log($"[EmergencyStopHandler] Stored ACTIVE IKPath: {pathName} - Target#{ikPath.NumTarget}, CurrentTarget={ikState.currentTargetName}, Linear={ikPath.LinearPathActive}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error storing IKPath state: {ex.Message}");
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Stored states for {storedCount} components + {plcStoredCount} PLC controllers + {ikPathStoredCount} IKPaths");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] CRITICAL ERROR in StoreAllComponentStates: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Restores all component states after resume (for resume functionality)
        /// </summary>
        private void RestoreAllComponentStates()
        {
            try
            {
                // Refresh component lists to ensure we have all components
                RefreshComponentLists();
                
                int restoredCount = 0;
                
                // Restore drive states
                for (int i = 0; i < allDrives.Count; i++)
                {
                    Drive drive = allDrives[i];
                    if (drive == null || drive.gameObject == null)
                        continue;
                    
                    try
                    {
                        var state = stateStorage.GetState<ComponentStateStorage.DriveState>(drive.gameObject.name);
                        if (state != null)
                        {
                            // CRITICAL: Always clear ForceStop first (drives shouldn't be force-stopped before emergency stop)
                            drive.ForceStop = false;
                            
                            // Restore drive parameters
                            drive.TargetSpeed = state.targetSpeed;
                            drive.TargetPosition = state.targetPosition;
                            drive.SpeedOverride = state.speedOverride;
                            
                            // NOTE: This old method is no longer used - see RestoreDriveAndAxisStates()
                            // Just set the flags directly
                            drive.JogForward = state.wasJogForward;
                            drive.JogBackward = state.wasJogBackward;
                            drive.TargetStartMove = state.wasTargetStartMove;
                            
                            restoredCount++;
                        }
                        else
                        {
                            // Even if no stored state, clear ForceStop to unfreeze
                            drive.ForceStop = false;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring drive state: {ex.Message}");
                    }
                }
                
                // Restore source states
                for (int i = 0; i < allSources.Count; i++)
                {
                    Source source = allSources[i];
                    if (source == null || source.gameObject == null)
                        continue;
                    
                    try
                    {
                        var state = stateStorage.GetState<ComponentStateStorage.SourceState>(source.gameObject.name);
                        if (state != null)
                        {
                            source.Enabled = state.wasEnabled;
                            source.GenerateMU = state.wasGenerateMU;
                            restoredCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring source state: {ex.Message}");
                    }
                }
                
                // Restore axis states (via their associated drives)
                for (int i = 0; i < allAxes.Count; i++)
                {
                    Axis axis = allAxes[i];
                    if (axis == null || axis.gameObject == null)
                        continue;
                    
                    try
                    {
                        var state = stateStorage.GetState<ComponentStateStorage.AxisState>(axis.gameObject.name);
                        if (state != null)
                        {
                            Drive axisDrive = axis.GetComponent<Drive>();
                            if (axisDrive != null)
                            {
                                // CRITICAL: Always clear ForceStop first
                                axisDrive.ForceStop = false;
                                
                                // Restore axis drive parameters
                                axisDrive.TargetPosition = state.targetPosition;
                                
                                // Reset and restore TargetStartMove to ensure state change is detected
                                axisDrive.TargetStartMove = false;
                                axisDrive.TargetStartMove = state.wasTargetStartMove;
                                
                                restoredCount++;
                            }
                        }
                        else
                        {
                            // Even if no stored state, clear ForceStop to unfreeze
                            Drive axisDrive = axis.GetComponent<Drive>();
                            if (axisDrive != null)
                            {
                                axisDrive.ForceStop = false;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring axis state: {ex.Message}");
                    }
                }
                
                // Restore grip states
                // CRITICAL: Handle grips carefully to prevent edge detection from dropping MUs
                for (int i = 0; i < allGrips.Count; i++)
                {
                    Grip grip = allGrips[i];
                    if (grip == null || grip.gameObject == null)
                        continue;
                    
                    try
                    {
                        var state = stateStorage.GetState<ComponentStateStorage.GripState>(grip.gameObject.name);
                        if (state != null)
                        {
                            // CRITICAL: Re-enable grip FIRST
                            grip.DeActivate(false);
                            
                            // CRITICAL: Check if there are gripped MUs
                            bool hasGrippedMUs = (grip.PickedMUs != null && grip.PickedMUs.Count > 0);
                            
                            // CRITICAL: Restore pick/place flags carefully to avoid edge detection
                            // If there are gripped MUs, DO NOT restore PlaceObjects = true
                            // This prevents the edge detection from triggering Place() and dropping MUs
                            if (hasGrippedMUs)
                            {
                                // If grip has MUs, only restore PickObjects (not PlaceObjects)
                                // This prevents accidental release
                                grip.PickObjects = state.wasPickObjects;
                                grip.PlaceObjects = false; // Keep false to prevent dropping MUs
                                Debug.Log($"[EmergencyStopHandler] Restored grip {grip.gameObject.name} with {grip.PickedMUs.Count} gripped MUs - PlaceObjects kept false to prevent drop");
                            }
                            else
                            {
                                // No gripped MUs, safe to restore both flags
                                grip.PickObjects = state.wasPickObjects;
                                grip.PlaceObjects = state.wasPlaceObjects;
                            }
                            
                            // Restore ForceStop state for grip drive if it exists
                            Drive gripDrive = grip.GetComponent<Drive>();
                            if (gripDrive != null)
                            {
                                // CRITICAL: Always clear ForceStop first
                                gripDrive.ForceStop = false;
                            }
                            
                            restoredCount++;
                        }
                        else
                        {
                            // Even if no stored state, re-activate grip and clear ForceStop
                            grip.DeActivate(false);
                            Drive gripDrive = grip.GetComponent<Drive>();
                            if (gripDrive != null)
                            {
                                gripDrive.ForceStop = false;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring grip state: {ex.Message}");
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Restored states for {restoredCount} components");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] CRITICAL ERROR in RestoreAllComponentStates: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Final cleanup pass - clears ForceStop on ALL drives and RESTORES JogForward/JogBackward
        /// 
        /// CRITICAL FIX FOR CONVEYOR: The Drive needs JogForward=true to actually move.
        /// Just clearing ForceStop isn't enough - we must restore the jog state!
        /// 
        /// Uses FindObjectsByType to catch ALL drives, not just those in lists
        /// </summary>
        private void ClearAllForceStops()
        {
            try
            {
                int clearedCount = 0;
                int jogRestoredCount = 0;
                
                // CRITICAL: Use FindObjectsByType to catch ALL drives in the scene
                Drive[] allDrivesInScene = FindObjectsByType<Drive>(FindObjectsSortMode.None);
                if (allDrivesInScene != null)
                {
                    for (int i = 0; i < allDrivesInScene.Length; i++)
                    {
                        Drive drive = allDrivesInScene[i];
                        if (drive == null || drive.gameObject == null)
                            continue;
                        
                        try
                        {
                            // Step 1: Clear ForceStop
                            drive.ForceStop = false;
                            clearedCount++;
                            
                            // Step 2: CRITICAL FIX - Restore JogForward/JogBackward from stored state
                            // Without this, conveyors won't move even with ForceStop=false!
                            string driveName = drive.gameObject.name;
                            var driveState = stateStorage.GetState<ComponentStateStorage.DriveState>(driveName);
                            if (driveState != null)
                            {
                                // Restore jog states - this is what makes conveyors actually move!
                                drive.JogForward = driveState.wasJogForward;
                                drive.JogBackward = driveState.wasJogBackward;
                                
                                // Also restore speed settings
                                if (driveState.targetSpeed > 0)
                                    drive.TargetSpeed = driveState.targetSpeed;
                                
                                if (driveState.wasJogForward || driveState.wasJogBackward)
                                {
                                    jogRestoredCount++;
                                    Debug.Log($"[EmergencyStopHandler] Restored jog state for {driveName}: JogForward={driveState.wasJogForward}, JogBackward={driveState.wasJogBackward}");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[EmergencyStopHandler] Error restoring drive {drive.gameObject?.name}: {ex.Message}");
                        }
                    }
                }
                
                // Also clear ForceStop on all axis drives
                Axis[] allAxesInScene = FindObjectsByType<Axis>(FindObjectsSortMode.None);
                if (allAxesInScene != null)
                {
                    for (int i = 0; i < allAxesInScene.Length; i++)
                    {
                        Axis axis = allAxesInScene[i];
                        if (axis != null && axis.gameObject != null)
                        {
                            try
                            {
                                Drive axisDrive = axis.GetComponent<Drive>();
                                if (axisDrive != null)
                                {
                                    axisDrive.ForceStop = false;
                                    clearedCount++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                
                // Also clear ForceStop on all grip drives
                Grip[] allGripsInScene = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                if (allGripsInScene != null)
                {
                    for (int i = 0; i < allGripsInScene.Length; i++)
                    {
                        Grip grip = allGripsInScene[i];
                        if (grip != null && grip.gameObject != null)
                        {
                            try
                            {
                                Drive gripDrive = grip.GetComponent<Drive>();
                                if (gripDrive != null)
                                {
                                    gripDrive.ForceStop = false;
                                    clearedCount++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Cleared ForceStop on {clearedCount} drives, restored jog state on {jogRestoredCount} drives");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error clearing ForceStops: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Re-activates all grips to ensure they can operate normally
        /// Uses FindObjectsByType to catch ALL grips, not just those in lists
        /// </summary>
        private void ReactivateAllGrips()
        {
            try
            {
                int reactivatedCount = 0;
                
                // CRITICAL: Use FindObjectsByType to catch ALL grips in the scene
                // This ensures we don't miss any grips that might not be in our lists
                Grip[] allGripsInScene = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                if (allGripsInScene != null)
                {
                    for (int i = 0; i < allGripsInScene.Length; i++)
                    {
                        Grip grip = allGripsInScene[i];
                        if (grip != null && grip.gameObject != null)
                        {
                            try
                            {
                                grip.DeActivate(false); // Re-enable grip
                                reactivatedCount++;
                            }
                            catch { }
                        }
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Re-activated {reactivatedCount} grips (using FindObjectsByType)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error re-activating grips: {ex.Message}");
            }
        }
        
        /// <summary>
        /// CRITICAL FIX FOR CONVEYORS: Forces conveyor drives to resume based on PLC state
        /// 
        /// THE PROBLEM: The conveyor's Drive doesn't automatically get JogForward=true because
        /// the signal connection between PLCDemoCNCLoadUnload.EntryConveyorStart and the Drive
        /// might not be working or the DriveBehavior isn't processing.
        /// 
        /// THE FIX: Directly find conveyor drives (those with TransportSurface) and set their
        /// JogForward based on the PLCDemoCNCLoadUnload's conveyor start signals.
        /// </summary>
        private void ForceDriveBehaviorSync()
        {
            try
            {
                int syncedCount = 0;
                
                // Step 1: Clear ForceStop on ALL DriveBehaviors (Drive_Simple etc.)
                Drive_Simple[] allDriveSimple = FindObjectsByType<Drive_Simple>(FindObjectsSortMode.None);
                if (allDriveSimple != null)
                {
                    for (int i = 0; i < allDriveSimple.Length; i++)
                    {
                        Drive_Simple driveBehavior = allDriveSimple[i];
                        if (driveBehavior == null || driveBehavior.gameObject == null)
                            continue;
                        
                        try
                        {
                            driveBehavior.ForceStop = false;
                            
                            Drive parentDrive = driveBehavior.GetComponent<Drive>();
                            if (parentDrive != null)
                            {
                                parentDrive.ForceStop = false;
                                
                                // Check if this drive has Forward/Backward signals set
                                if (driveBehavior.Forward != null && driveBehavior.Forward.Value)
                                {
                                    parentDrive.JogForward = true;
                                    Debug.Log($"[EmergencyStopHandler] Set JogForward=true on {driveBehavior.gameObject.name}");
                                }
                                if (driveBehavior.Backward != null && driveBehavior.Backward.Value)
                                {
                                    parentDrive.JogBackward = true;
                                    Debug.Log($"[EmergencyStopHandler] Set JogBackward=true on {driveBehavior.gameObject.name}");
                                }
                            }
                            syncedCount++;
                        }
                        catch { }
                    }
                }
                
                // Step 2: Clear ForceStop on all TransportSurfaces
                TransportSurface[] allTransportSurfaces = FindObjectsByType<TransportSurface>(FindObjectsSortMode.None);
                if (allTransportSurfaces != null)
                {
                    for (int i = 0; i < allTransportSurfaces.Length; i++)
                    {
                        TransportSurface surface = allTransportSurfaces[i];
                        if (surface != null && surface.gameObject != null)
                        {
                            try
                            {
                                surface.ForceStop = false;
                                syncedCount++;
                            }
                            catch { }
                        }
                    }
                }
                
                // Step 3: CRITICAL - Directly control conveyor drives based on PLC state
                // This is the key fix - we read the PLC's conveyor start signals and apply
                // them directly to the conveyor drives
                ForceConveyorDrivesFromPLC();
                
                // Step 4: Also deactivate all Fixers to prevent Place() calls during resume
                DeactivateAllFixers();
                
                Debug.Log($"[EmergencyStopHandler] Synced {syncedCount} DriveBehaviors and TransportSurfaces");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in ForceDriveBehaviorSync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// CRITICAL FIX: Forces conveyor drives to resume using stored state OR PLC signals
        /// Priority: 1) Stored state (wasJogForward), 2) PLC signals as fallback
        /// </summary>
        private void ForceConveyorDrivesFromPLC()
        {
            try
            {
                // Get all PLCDemoCNCLoadUnload controllers and their conveyor signals
                if (cncControllers == null || cncControllers.Count == 0)
                {
                    RefreshComponentLists();
                }
                
                // Use reflection to clear internal stopped flags
                Type driveType = typeof(Drive);
                FieldInfo lastJogField = driveType.GetField("_lastjog", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo isStoppedField = driveType.GetField("IsStopped", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo stopDriveField = driveType.GetField("_StopDrive", BindingFlags.NonPublic | BindingFlags.Instance);
                
                int resumedCount = 0;
                
                // Find all TransportSurface components and their parent Drives
                TransportSurface[] allSurfaces = FindObjectsByType<TransportSurface>(FindObjectsSortMode.None);
                if (allSurfaces == null) return;
                
                foreach (TransportSurface surface in allSurfaces)
                {
                    if (surface == null) continue;
                    
                    try
                    {
                        // Get the Drive controlling this TransportSurface
                        Drive surfaceDrive = surface.GetDrive();
                        if (surfaceDrive == null)
                            surfaceDrive = surface.GetComponentInParent<Drive>();
                        
                        if (surfaceDrive == null) continue;
                        
                        // Clear ForceStop on the drive FIRST
                        surfaceDrive.ForceStop = false;
                        
                        // CRITICAL FIX: Check stored state FIRST
                        var storedState = stateStorage.GetState<ComponentStateStorage.DriveState>(surfaceDrive.gameObject.name);
                        bool shouldRunFromStoredState = (storedState != null && storedState.wasJogForward);
                        
                        if (shouldRunFromStoredState)
                        {
                            // CRITICAL: Prepare internal state for jog restart
                            if (lastJogField != null)
                                lastJogField.SetValue(surfaceDrive, false);
                            if (isStoppedField != null)
                                isStoppedField.SetValue(surfaceDrive, false);
                            if (stopDriveField != null)
                                stopDriveField.SetValue(surfaceDrive, false);
                            
                            // Restore speed and jog state from stored state
                            surfaceDrive.TargetSpeed = storedState.targetSpeed;
                            surfaceDrive.JogForward = true;
                            surfaceDrive.JogBackward = false;
                            resumedCount++;
                            Debug.Log($"[EmergencyStopHandler] Resumed conveyor {surfaceDrive.gameObject.name} from STORED STATE (JogForward=true, Speed={storedState.targetSpeed})");
                            continue; // Skip PLC check, stored state takes priority
                        }
                        
                        // Fallback: Check PLC signals if no stored state
                        string surfaceName = surface.gameObject.name.ToLower();
                        string driveName = surfaceDrive.gameObject.name.ToLower();
                        
                        bool isEntryConveyor = surfaceName.Contains("entry") || driveName.Contains("entry") ||
                                               surfaceName.Contains("conveyor1") || driveName.Contains("conveyor1");
                        bool isExitConveyor = surfaceName.Contains("exit") || driveName.Contains("exit") ||
                                              surfaceName.Contains("conveyor2") || driveName.Contains("conveyor2");
                        
                        // Find the corresponding PLC signal
                        foreach (PLCDemoCNCLoadUnload controller in cncControllers)
                        {
                            if (controller == null) continue;
                            
                            bool shouldRun = false;
                            
                            if (isEntryConveyor && controller.EntryConveyorStart != null)
                            {
                                shouldRun = controller.EntryConveyorStart.Value;
                            }
                            else if (isExitConveyor && controller.ExitConveyorStart != null)
                            {
                                shouldRun = controller.ExitConveyorStart.Value;
                            }
                            
                            if (shouldRun)
                            {
                                // Prepare internal state for jog
                                if (lastJogField != null)
                                    lastJogField.SetValue(surfaceDrive, false);
                                if (isStoppedField != null)
                                    isStoppedField.SetValue(surfaceDrive, false);
                                if (stopDriveField != null)
                                    stopDriveField.SetValue(surfaceDrive, false);
                                
                                surfaceDrive.JogForward = true;
                                surfaceDrive.JogBackward = false;
                                resumedCount++;
                                Debug.Log($"[EmergencyStopHandler] Forced conveyor {surfaceDrive.gameObject.name} to run from PLC signal");
                            }
                        }
                    }
                    catch { }
                }
                
                Debug.Log($"[EmergencyStopHandler] Resumed {resumedCount} conveyor drives");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in ForceConveyorDrivesFromPLC: {ex.Message}");
            }
        }
        
        /// <summary>
        /// CRITICAL FIX: Only deactivates Fixers, NOT Grips!
        /// Grips must stay active to keep MUs attached - deactivating grips causes MU dropping!
        /// </summary>
        private void DeactivateAllFixers()
        {
            try
            {
                int deactivatedCount = 0;
                
                // Deactivate all Fixers ONLY
                Fixer[] allFixers = FindObjectsByType<Fixer>(FindObjectsSortMode.None);
                if (allFixers != null)
                {
                    foreach (Fixer fixer in allFixers)
                    {
                        if (fixer == null) continue;
                        
                        try
                        {
                            fixer.DeActivate(true);
                            deactivatedCount++;
                        }
                        catch { }
                    }
                }
                
                // CRITICAL FIX: DO NOT deactivate Grips here!
                // Deactivating grips causes MUs to be released/dropped!
                // Grips should remain active with PlaceObjects=false to keep MUs attached.
                // The grip's PlaceObjects flag is already set to false in RestoreGripStatesSafely()
                // which prevents placing MUs without deactivating the entire grip.
                
                Debug.Log($"[EmergencyStopHandler] Deactivated {deactivatedCount} Fixers (grips kept active to prevent MU drops)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error deactivating Fixers: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reactivates all Fixers and Grips after a delay (called from coroutine)
        /// </summary>
        private void ReactivateAllFixers()
        {
            try
            {
                int reactivatedCount = 0;
                
                // Reactivate all Fixers
                Fixer[] allFixers = FindObjectsByType<Fixer>(FindObjectsSortMode.None);
                if (allFixers != null)
                {
                    foreach (Fixer fixer in allFixers)
                    {
                        if (fixer == null) continue;
                        
                        try
                        {
                            fixer.DeActivate(false);
                            reactivatedCount++;
                        }
                        catch { }
                    }
                }
                
                // Reactivate all Grips
                Grip[] allGrips = FindObjectsByType<Grip>(FindObjectsSortMode.None);
                if (allGrips != null)
                {
                    foreach (Grip grip in allGrips)
                    {
                        if (grip == null) continue;
                        
                        try
                        {
                            grip.DeActivate(false);
                            reactivatedCount++;
                        }
                        catch { }
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Reactivated {reactivatedCount} Fixers and Grips");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error reactivating Fixers/Grips: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Re-triggers drive movements after restoration to ensure drives resume properly
        /// SIMPLIFIED: Uses DriveTo() for drives that were actively moving
        /// </summary>
        private void ReTriggerDriveMovements()
        {
            try
            {
                int reTriggeredCount = 0;
                Type driveType = typeof(Drive);
                FieldInfo lastJogField = driveType.GetField("_lastjog", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo isStoppedField = driveType.GetField("IsStopped", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo stopDriveField = driveType.GetField("_StopDrive", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // Re-trigger regular drives
                for (int i = 0; i < allDrives.Count; i++)
                {
                    Drive drive = allDrives[i];
                    if (drive == null || drive.gameObject == null)
                        continue;
                    
                    try
                    {
                        // Ensure ForceStop is false
                        if (drive.ForceStop)
                        {
                            drive.ForceStop = false;
                        }
                        
                        var state = stateStorage.GetState<ComponentStateStorage.DriveState>(drive.gameObject.name);
                        if (state != null)
                        {
                            // If drive was actively moving to target, use DriveTo()
                            if (state.wasIsDrivingToTarget && !drive.IsAtTarget)
                            {
                                drive.DriveTo(state.currentDestination);
                                reTriggeredCount++;
                                Debug.Log($"[EmergencyStopHandler] Re-triggered via DriveTo for drive {drive.gameObject.name}");
                            }
                            // If drive was jogging, ensure jog is active
                            else if (state.wasJogForward || state.wasJogBackward)
                            {
                                if (lastJogField != null)
                                    lastJogField.SetValue(drive, false);
                                if (isStoppedField != null)
                                    isStoppedField.SetValue(drive, false);
                                if (stopDriveField != null)
                                    stopDriveField.SetValue(drive, false);
                                
                                drive.JogForward = state.wasJogForward;
                                drive.JogBackward = state.wasJogBackward;
                                reTriggeredCount++;
                                Debug.Log($"[EmergencyStopHandler] Re-triggered jog for drive {drive.gameObject.name}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error re-triggering drive {drive.gameObject.name}: {ex.Message}");
                    }
                }
                
                // Re-trigger axis drives
                for (int i = 0; i < allAxes.Count; i++)
                {
                    Axis axis = allAxes[i];
                    if (axis == null || axis.gameObject == null)
                        continue;
                    
                    try
                    {
                        Drive axisDrive = axis.GetComponent<Drive>();
                        if (axisDrive != null)
                        {
                            if (axisDrive.ForceStop)
                            {
                                axisDrive.ForceStop = false;
                            }
                            
                            var state = stateStorage.GetState<ComponentStateStorage.AxisState>(axis.gameObject.name);
                            if (state != null && state.wasIsDrivingToTarget && !axisDrive.IsAtTarget)
                            {
                                axisDrive.DriveTo(state.currentDestination);
                                reTriggeredCount++;
                                Debug.Log($"[EmergencyStopHandler] Re-triggered via DriveTo for axis {axis.gameObject.name}");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error re-triggering axis {axis.gameObject.name}: {ex.Message}");
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Re-triggered movements for {reTriggeredCount} drives/axes");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error re-triggering drive movements: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Final sync coroutine - runs after immediate restoration to ensure everything is synced
        /// This handles final state synchronization and cleanup
        /// </summary>
        private IEnumerator FinalResumeSyncCoroutine()
        {
            Debug.Log("[EmergencyStopHandler] Starting final resume sync coroutine");
            
            // Wait one frame to allow components to process the restored states
            yield return null;
            
            // Final safety pass - ensure ALL ForceStops are cleared (drives)
            try
            {
                ClearAllForceStops();
                Debug.Log("[EmergencyStopHandler] Final Drive ForceStop clearing pass completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in final ForceStop clearing: {ex.Message}");
            }
            
            // Final safety pass - ensure ALL IKPath ForceStops are cleared
            try
            {
                ClearAllIKPathForceStops();
                Debug.Log("[EmergencyStopHandler] Final IKPath ForceStop clearing pass completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in final IKPath ForceStop clearing: {ex.Message}");
            }
            
            // Wait one more frame
            yield return null;
            
            // Re-trigger drive movements to ensure they resume
            try
            {
                ReTriggerDriveMovements();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error re-triggering movements: {ex.Message}");
            }
            
            // Wait one more frame for movements to start
            yield return null;
            
            // CRITICAL: Final pass - Force DriveBehavior sync again
            // This ensures conveyors are definitely running after all other restorations
            try
            {
                ForceDriveBehaviorSync();
                Debug.Log("[EmergencyStopHandler] Final DriveBehavior sync completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in final DriveBehavior sync: {ex.Message}");
            }
            
            // Release PLC controllers
            try
            {
                ReleaseCncControllers();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error releasing CNC controllers: {ex.Message}");
            }
            
            // Wait one more frame for PLC to process
            yield return null;
            
            // CRITICAL: One more DriveBehavior sync after PLC has had a chance to update signals
            try
            {
                ForceDriveBehaviorSync();
                Debug.Log("[EmergencyStopHandler] Post-PLC DriveBehavior sync completed");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in post-PLC DriveBehavior sync: {ex.Message}");
            }
            
            // Wait a few more frames before reactivating Fixers
            // This ensures the robot has been fully reset and won't trigger Place() immediately
            yield return null;
            yield return null;
            yield return null;
            
            // Reactivate Fixers so they can work normally again
            try
            {
                ReactivateAllFixers();
                Debug.Log("[EmergencyStopHandler] Reactivated Fixers after delay");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error reactivating Fixers: {ex.Message}");
            }
            
            // Clear stored states
            try
            {
                stateStorage.ClearStates();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error clearing stored states: {ex.Message}");
            }
            
            Debug.Log("[EmergencyStopHandler] Final resume sync coroutine completed");
        }
        
        /// <summary>
        /// Restores drive and axis states using the proper method
        /// CRITICAL FIX: Uses DriveTo() for drives that were actively moving to target
        /// This ensures proper internal state setup instead of manual flag manipulation
        /// </summary>
        private void RestoreDriveAndAxisStates()
        {
            try
            {
                int restoredCount = 0;
                
                // Get Drive type for reflection
                Type driveType = typeof(Drive);
                FieldInfo lastJogField = driveType.GetField("_lastjog", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo isStoppedField = driveType.GetField("IsStopped", BindingFlags.Public | BindingFlags.Instance);
                FieldInfo stopDriveField = driveType.GetField("_StopDrive", BindingFlags.NonPublic | BindingFlags.Instance);
                
                // Restore drive states
                for (int i = 0; i < allDrives.Count; i++)
                {
                    Drive drive = allDrives[i];
                    if (drive == null || drive.gameObject == null)
                        continue;
                    
                    try
                    {
                        // CRITICAL: Always clear ForceStop FIRST
                        drive.ForceStop = false;
                        
                        var state = stateStorage.GetState<ComponentStateStorage.DriveState>(drive.gameObject.name);
                        if (state != null)
                        {
                            // Restore drive parameters
                            drive.TargetSpeed = state.targetSpeed;
                            drive.SpeedOverride = state.speedOverride;
                            
                            // CRITICAL FIX: Check if drive was ACTUALLY moving to target
                            // wasIsDrivingToTarget tells us the REAL state, not wasTargetStartMove (which auto-clears)
                            if (state.wasIsDrivingToTarget)
                            {
                                // Drive was actively moving to a target position
                                // Use DriveTo() which properly sets all internal flags
                                drive.DriveTo(state.currentDestination);
                                Debug.Log($"[EmergencyStopHandler] Resumed ACTIVE drive {drive.gameObject.name} via DriveTo({state.currentDestination}) from pos {state.currentPosition}");
                                restoredCount++;
                            }
                            else if (state.wasJogForward || state.wasJogBackward)
                            {
                                // Drive was jogging - prepare internal state and set jog flags
                                if (lastJogField != null)
                                    lastJogField.SetValue(drive, false);
                                if (isStoppedField != null)
                                    isStoppedField.SetValue(drive, false);
                                if (stopDriveField != null)
                                    stopDriveField.SetValue(drive, false);
                                
                                // Set jog flags
                                drive.JogForward = state.wasJogForward;
                                drive.JogBackward = state.wasJogBackward;
                                Debug.Log($"[EmergencyStopHandler] Resumed JOG drive {drive.gameObject.name}: JogF={state.wasJogForward}, JogB={state.wasJogBackward}");
                                restoredCount++;
                            }
                            else if (!state.wasStopped)
                            {
                                // Drive wasn't stopped but also not explicitly moving - restore basic state
                                drive.TargetPosition = state.targetPosition;
                                if (isStoppedField != null)
                                    isStoppedField.SetValue(drive, false);
                                Debug.Log($"[EmergencyStopHandler] Restored drive {drive.gameObject.name} to ready state");
                                restoredCount++;
                            }
                            else
                            {
                                // Drive was stopped - just restore parameters
                                drive.TargetPosition = state.targetPosition;
                                Debug.Log($"[EmergencyStopHandler] Restored STOPPED drive {drive.gameObject.name}");
                                restoredCount++;
                            }
                        }
                        else
                        {
                            // Even if no stored state, ensure ForceStop is cleared
                            drive.ForceStop = false;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring drive state for {drive.gameObject.name}: {ex.Message}");
                        try { drive.ForceStop = false; } catch { }
                    }
                }
                
                // Restore axis states
                for (int i = 0; i < allAxes.Count; i++)
                {
                    Axis axis = allAxes[i];
                    if (axis == null || axis.gameObject == null)
                        continue;
                    
                    try
                    {
                        Drive axisDrive = axis.GetComponent<Drive>();
                        if (axisDrive != null)
                        {
                            // CRITICAL: Always clear ForceStop FIRST
                            axisDrive.ForceStop = false;
                            
                            var state = stateStorage.GetState<ComponentStateStorage.AxisState>(axis.gameObject.name);
                            if (state != null)
                            {
                                // CRITICAL FIX: Check if axis was ACTUALLY moving to target
                                if (state.wasIsDrivingToTarget)
                                {
                                    // Axis was actively moving - use DriveTo()
                                    axisDrive.DriveTo(state.currentDestination);
                                    Debug.Log($"[EmergencyStopHandler] Resumed ACTIVE axis {axis.gameObject.name} via DriveTo({state.currentDestination})");
                                    restoredCount++;
                                }
                                else
                                {
                                    // Axis was not actively moving - just restore position
                                    axisDrive.TargetPosition = state.targetPosition;
                                    Debug.Log($"[EmergencyStopHandler] Restored axis {axis.gameObject.name} to position {state.targetPosition}");
                                    restoredCount++;
                                }
                            }
                            else
                            {
                                axisDrive.ForceStop = false;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring axis state for {axis.gameObject.name}: {ex.Message}");
                        try 
                        { 
                            Drive axisDrive = axis.GetComponent<Drive>();
                            if (axisDrive != null)
                                axisDrive.ForceStop = false; 
                        } 
                        catch { }
                    }
                }
                
                // Restore source states
                for (int i = 0; i < allSources.Count; i++)
                {
                    Source source = allSources[i];
                    if (source == null || source.gameObject == null)
                        continue;
                    
                    try
                    {
                        var state = stateStorage.GetState<ComponentStateStorage.SourceState>(source.gameObject.name);
                        if (state != null)
                        {
                            source.Enabled = state.wasEnabled;
                            source.GenerateMU = state.wasGenerateMU;
                            restoredCount++;
                            Debug.Log($"[EmergencyStopHandler] Restored source {source.gameObject.name}: Enabled={state.wasEnabled}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring source state: {ex.Message}");
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Restored drive/axis/source states for {restoredCount} components");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in RestoreDriveAndAxisStates: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Restores grip states safely, preventing MU dropping
        /// Uses reflection to sync internal _placeobjectsbefore and _pickobjectsbefore flags
        /// CRITICAL: Ensures all grips are re-activated and ForceStop is cleared
        /// </summary>
        private void RestoreGripStatesSafely()
        {
            try
            {
                int restoredCount = 0;
                
                // Get Grip type for reflection
                Type gripType = typeof(Grip);
                FieldInfo pickBeforeField = gripType.GetField("_pickobjectsbefore", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo placeBeforeField = gripType.GetField("_placeobjectsbefore", BindingFlags.NonPublic | BindingFlags.Instance);
                
                for (int i = 0; i < allGrips.Count; i++)
                {
                    Grip grip = allGrips[i];
                    if (grip == null || grip.gameObject == null)
                        continue;
                    
                    try
                    {
                        // CRITICAL FIX: Do NOT reactivate grip here!
                        // Keep grips deactivated to prevent Place() calls during resume
                        // Grips will be reactivated later in the coroutine after IKPath is reset
                        // grip.DeActivate(false); // REMOVED - keep deactivated!
                        
                        // CRITICAL: Clear ForceStop on grip drive to allow future movement
                        Drive gripDrive = grip.GetComponent<Drive>();
                        if (gripDrive != null)
                        {
                            gripDrive.ForceStop = false;
                        }
                        
                        // Check if there are gripped MUs
                        bool hasGrippedMUs = (grip.PickedMUs != null && grip.PickedMUs.Count > 0);
                        
                        var state = stateStorage.GetState<ComponentStateStorage.GripState>(grip.gameObject.name);
                        if (state != null)
                        {
                            // CRITICAL FIX: FIRST reactivate the grip BEFORE setting flags
                            // This ensures the grip can operate normally again
                            grip.DeActivate(false);
                            
                            if (hasGrippedMUs)
                            {
                                // CRITICAL: If grip has MUs, DO NOT restore PlaceObjects = true
                                // This prevents edge detection from triggering Place() and dropping MUs
                                
                                // Set internal flags FIRST to prevent edge detection
                                if (pickBeforeField != null)
                                    pickBeforeField.SetValue(grip, state.wasPickObjects);
                                if (placeBeforeField != null)
                                    placeBeforeField.SetValue(grip, false); // Match PlaceObjects = false
                                
                                // Now set the actual flags (no edge detection because internal flags match)
                                grip.PickObjects = state.wasPickObjects;
                                grip.PlaceObjects = false; // Keep false to prevent dropping
                                
                                // CRITICAL FIX: Ensure gripped MUs stay attached
                                // Set MUs kinematic and ensure they're parented to grip
                                foreach (GameObject muObj in grip.PickedMUs)
                                {
                                    if (muObj != null)
                                    {
                                        MU mu = muObj.GetComponent<MU>();
                                        if (mu != null)
                                        {
                                            // Keep MU kinematic to prevent falling
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
                                
                                Debug.Log($"[EmergencyStopHandler] Restored grip {grip.gameObject.name} with {grip.PickedMUs.Count} gripped MUs - PlaceObjects kept false to prevent drop");
                            }
                            else
                            {
                                // No gripped MUs, safe to restore both flags
                                // Set internal flags FIRST to match restored state
                                if (pickBeforeField != null)
                                    pickBeforeField.SetValue(grip, state.wasPickObjects);
                                if (placeBeforeField != null)
                                    placeBeforeField.SetValue(grip, state.wasPlaceObjects);
                                
                                // Now set the actual flags (no edge detection because internal flags match)
                                grip.PickObjects = state.wasPickObjects;
                                grip.PlaceObjects = state.wasPlaceObjects;
                            }
                            
                            restoredCount++;
                        }
                        else
                        {
                            // Even if no stored state, ensure grip is active and ForceStop is cleared
                            grip.DeActivate(false);
                            if (gripDrive != null)
                            {
                                gripDrive.ForceStop = false;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[EmergencyStopHandler] Error restoring grip state: {ex.Message}");
                        // Still try to re-activate and clear ForceStop even if restoration fails
                        try 
                        { 
                            grip.DeActivate(false);
                            Drive gripDrive = grip.GetComponent<Drive>();
                            if (gripDrive != null)
                                gripDrive.ForceStop = false;
                        } 
                        catch { }
                    }
                }
                
                Debug.Log($"[EmergencyStopHandler] Restored grip states for {restoredCount} grips");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EmergencyStopHandler] Error in RestoreGripStatesSafely: {ex.Message}");
            }
        }
    }
}

