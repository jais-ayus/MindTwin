// // realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// // (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using UnityEngine;

namespace realvirtual
{
    //! Measures and analyzes production cycle times for performance optimization and validation in industrial systems.
    //! This professional statistics component tracks minimum, maximum, and average cycle times with automatic
    //! calculation of key performance indicators. Essential for validating production targets, identifying
    //! bottlenecks, and optimizing process efficiency in virtual commissioning. Provides real-time metrics
    //! for OEE calculations and supports data-driven decision making for production line improvements.
    public class StatCycleTime : MonoBehaviour, IStatReset, IStatDisplay
    {
        [Header("Cycle Time Statistics (in seconds)")] [SerializeField]
        [ReadOnly]private float minCycleTime = float.MaxValue;

        [ReadOnly][SerializeField] private float maxCycleTime = float.MinValue;
        [ReadOnly][SerializeField] private float averageCycleTime = 0f;
        [ReadOnly][SerializeField] private float currentCycleTime = 0f;
        [ReadOnly][SerializeField] private int CycleCount = 0;

        public void StatReset()
        {
            ResetStatistics();
        }
        
        public string GetDisplay()
        {
            string line1 = $"<color=yellow><size=50%>CycleTime [s]</size></color>\n<size=150%>{averageCycleTime.ToString("F0")}</size>";
            string line2 = $"<size=50%>Cycles</size>\n<size=150%>{CycleCount}</size>";
            return $"{line1}\n{line2}";
        }
        
        public float MinCycleTime
        {
            get => minCycleTime;
            private set => minCycleTime = value;
        }

        public float MaxCycleTime
        {
            get => maxCycleTime;
            private set => maxCycleTime = value;
        }

        public float AverageCycleTime
        {
            get => CycleCount > 0 ? totalCycleTime / CycleCount : 0f;
            private set => averageCycleTime = value;
        }

        public float CurrentCycleTime
        {
            get => currentCycleTime;
            private set => currentCycleTime = value;
        }

        private float cycleStartTime = -1f; // Initialize to -1 to indicate no cycle is currently running
        private float totalCycleTime = 0f;
        private float resetTime = 0f; // Time when statistics were last reset

        /// <summary>
        /// Starts a new cycle measurement.
        /// </summary>
        public void StartCycle()
        {
            cycleStartTime = Time.time;
        }

        /// <summary>
        /// Stops the current cycle measurement and updates statistics.
        /// </summary>
        public void StopCycle()
        {
            if (cycleStartTime >= 0)
            {
                float cycleTime = Time.time - cycleStartTime;

                // Update statistics
                if (cycleTime < MinCycleTime)
                    MinCycleTime = cycleTime;
                if (cycleTime > MaxCycleTime)
                    MaxCycleTime = cycleTime;
                
                totalCycleTime += cycleTime;
                CycleCount++;
                averageCycleTime = totalCycleTime / CycleCount;
                CurrentCycleTime = cycleTime;
                cycleStartTime = -1; // Reset start time
            }
            else
            {
                Debug.LogWarning("StartCycle must be called before StopCycle.");
            }
        }

        /// <summary>
        /// Resets all recorded statistics.
        /// </summary>
        public void ResetStatistics()
        {
            // Stop the current cycle calculation
            cycleStartTime = -1f;

            // Reset all statistics
            MinCycleTime = float.MaxValue;
            MaxCycleTime = float.MinValue;
            totalCycleTime = 0f;
            CycleCount = 0;
            CurrentCycleTime = 0f;
            resetTime = Time.time; // Record the reset time
        }

        private void Update()
        {
            if (cycleStartTime >= 0)
            {
                CurrentCycleTime = Time.time - cycleStartTime;
            }
        }

        /// <summary>
        /// Gets the time when statistics were last reset.
        /// </summary>
        public float GetResetTime()
        {
            return resetTime;
        }


 
    }
}