using UnityEngine;
using System.Collections.Generic;
using realvirtual;

namespace IoTDashboard
{
    /// <summary>
    /// Stores and restores component states for emergency stop/resume functionality
    /// </summary>
    public class ComponentStateStorage
    {
        // Drive state storage - CRITICAL: includes internal state for proper resume
        [System.Serializable]
        public class DriveState
        {
            // Public flags
            public bool wasJogForward;
            public bool wasJogBackward;
            public bool wasTargetStartMove;
            public float targetSpeed;
            public float targetPosition;
            public float speedOverride;
            
            // CRITICAL: Internal state - this tells us if drive was ACTUALLY moving
            public bool wasIsDrivingToTarget;  // _isdrivingtotarget - true if actively moving to target
            public float currentPosition;       // Exact position when stopped
            public float currentSpeed;          // Speed when stopped
            public float currentDestination;    // Where it was heading (_currentdestination)
            public bool wasStopped;             // IsStopped flag
        }
        
        // Source state storage
        [System.Serializable]
        public class SourceState
        {
            public bool wasEnabled;
            public bool wasGenerateMU;
        }
        
        // Axis state storage - includes internal state
        [System.Serializable]
        public class AxisState
        {
            public bool wasTargetStartMove;
            public float targetPosition;
            public bool wasIsDrivingToTarget;  // Internal: was it actively moving?
            public float currentPosition;
            public float currentDestination;
        }
        
        // Grip state storage
        [System.Serializable]
        public class GripState
        {
            public bool wasPickObjects;
            public bool wasPlaceObjects;
            public bool wasDeactivated;
            // Store names of gripped MUs (we'll restore by finding them)
            public List<string> grippedMUNames = new List<string>();
        }
        
        // Drive force stop state
        [System.Serializable]
        public class DriveForceStopState
        {
            public bool wasForceStop;
        }
        
        // PLC Controller state storage - CRITICAL for robot/CNC resume
        [System.Serializable]
        public class PLCControllerState
        {
            public bool wasAutomaticMode;
            public string robotState;
            public string machineState;
            public string entryState;
            public string exitState;
            
            // Button states
            public bool wasOnSwitch;
            public bool wasRobotButton;
            public bool wasConveyorInButton;
            public bool wasConveyorOutButton;
            public bool wasAutomaticButton;
            
            // PLC output states (what the PLC was commanding)
            public bool wasEntryConveyorStart;
            public bool wasExitConveyorStart;
            public bool wasStartLoadingProgramm;
            public bool wasStartUnloadingProgramm;
            public bool wasStartMachine;
            public bool wasOpenDoor;
        }
        
        // CRITICAL: IKPath state storage - stores robot path program state for proper resume
        // Without this, robot would restart from Target 0 instead of continuing from where it was
        [System.Serializable]
        public class IKPathState
        {
            public bool wasPathIsActive;        // Was the path running?
            public bool wasPathIsFinished;      // Was the path finished?
            public int numTarget;               // Current target index (where to resume from)
            public string currentTargetName;    // Name of current target (for finding it)
            public string lastTargetName;       // Name of last target
            public bool wasLinearPathActive;    // Was linear interpolation active?
            public float linearPathPos;         // Position on linear path
            public bool wasWaitForSignal;       // Was path waiting for a signal?
            public bool wasForceStop;           // Was ForceStop already set?
        }
        
        // Dictionary to store states by component name
        private Dictionary<string, object> storedStates = new Dictionary<string, object>();
        
        /// <summary>
        /// Stores state for a component
        /// </summary>
        public void StoreState(string componentName, object state)
        {
            if (string.IsNullOrEmpty(componentName) || state == null)
                return;
            
            storedStates[componentName] = state;
        }
        
        /// <summary>
        /// Gets stored state for a component
        /// </summary>
        public T GetState<T>(string componentName) where T : class
        {
            if (string.IsNullOrEmpty(componentName))
                return null;
            
            if (storedStates.TryGetValue(componentName, out object state))
            {
                return state as T;
            }
            
            return null;
        }
        
        /// <summary>
        /// Checks if state exists for a component
        /// </summary>
        public bool HasState(string componentName)
        {
            return !string.IsNullOrEmpty(componentName) && storedStates.ContainsKey(componentName);
        }
        
        /// <summary>
        /// Clears all stored states
        /// </summary>
        public void ClearStates()
        {
            storedStates.Clear();
        }
        
        /// <summary>
        /// Gets count of stored states
        /// </summary>
        public int Count
        {
            get { return storedStates.Count; }
        }
    }
}

