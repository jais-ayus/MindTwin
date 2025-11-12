// // realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// // (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    public class StatStates : MonoBehaviour, IStatReset, IStatDisplay
    {
     
        [Tooltip("List of states considered as 'free' (not utilized).")]
        public List<string> FreeStates = new List<string> { "Idle", "Stopped" };

        [HideInInspector] [SerializeField] public List<string> TrackedStates = new();
        [HideInInspector] [SerializeField] public List<float> TrackedDurations = new();
        [HideInInspector] [SerializeField] public List<float> TrackedPercentages = new();
        [HideInInspector] [SerializeField] public float UtilizationPercent = 0f;

        private Dictionary<string, float> stateDurations = new();
        private string currentState = null;
        private float stateStartTime;
        private float lastResetTime;

        
        public void StatReset()
        {
            ResetStatistics();
        }
        
        public string GetDisplay()
        {
            
            return $"<color=green><size=50%>Utilization [%]</size></color>\n<size=150%>{UtilizationPercent.ToString("F0")}</size>";
        }
        
        void Start()
        {
            lastResetTime = Time.time;
        }

        void Update()
        {
            float currentTime = Time.time;
            

            UpdateInspectorStats();
            CalculateUtilization();
        }

        /// <summary>
        /// Set a new state.
        /// </summary>
        public void State(string newState)
        {
            if (newState == currentState) return;

            if (!string.IsNullOrEmpty(currentState))
            {
                float elapsed = Time.time - stateStartTime;
                if (!stateDurations.ContainsKey(currentState))
                    stateDurations[currentState] = 0f;

                stateDurations[currentState] += elapsed;
            }

            currentState = newState;
            stateStartTime = Time.time;

            // Update statistics when the state changes
            UpdateInspectorStats();
            CalculateUtilization();
        }

        /// <summary>
        /// Reset all statistics.
        /// </summary>
        public void ResetStatistics()
        {
            stateDurations.Clear();
            TrackedStates.Clear();
            TrackedDurations.Clear();
            TrackedPercentages.Clear();
            UtilizationPercent = 0f;

            if (!string.IsNullOrEmpty(currentState))
                stateStartTime = Time.time;
        }

        /// <summary>
        /// Returns utilization as a float between 0 and 1.
        /// </summary>
        public float GetUtilization01() => UtilizationPercent / 100f;

        /// <summary>
        /// Returns utilization in percent (0-100).
        /// </summary>
        public float GetUtilizationPercent() => UtilizationPercent;

        private void UpdateInspectorStats()
        {
            TrackedStates.Clear();
            TrackedDurations.Clear();
            TrackedPercentages.Clear();

            float total = 0f;
            foreach (var kvp in stateDurations)
                total += kvp.Value;

            foreach (var kvp in stateDurations)
            {
                TrackedStates.Add(kvp.Key);
                TrackedDurations.Add(kvp.Value);
                float percent = (total > 0f) ? (kvp.Value / total * 100f) : 0f;
                TrackedPercentages.Add(percent);
            }
        }

        private void CalculateUtilization()
        {
            float total = 0f;
            float free = 0f;

            foreach (var kvp in stateDurations)
            {
                total += kvp.Value;
                if (FreeStates.Contains(kvp.Key))
                    free += kvp.Value;
            }

            UtilizationPercent = total > 0f ? (1f - free / total) * 100f : 0f;
        }



    }
}