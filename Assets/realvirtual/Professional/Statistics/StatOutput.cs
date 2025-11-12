// // realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// // (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System;
using UnityEngine;

namespace realvirtual
{
    public class StatOutput : MonoBehaviour, IStatReset, IStatDisplay
    {
        public float HoursPerDay = 16;
        public bool PartsPerDayInDisplay = false;
        public string PartsDescription = "Output";
        [Header("Output Statistics")]
        [ReadOnly] public int TotalCount = 0;
        [ReadOnly] public float PartsPerHour;
        [ReadOnly] public float PartsPerDay;
        
        
        private float resetTime = 0f;

        /// <summary>
        /// Adds output count (default is 1).
        /// </summary>
        /// <param name="count">How many parts to add</param>
        public void Output(int count = 1)
        {
            TotalCount += count;
            
        }

        private void Update()
        {
            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            float elapsedHours = (Time.time - resetTime) / 3600f;
            PartsPerHour = elapsedHours > 0 ? TotalCount / elapsedHours : 0;
            // calculate parts per day
            PartsPerDay = PartsPerHour * HoursPerDay; // Calculate parts per day
        }

        /// <summary>
        /// Resets output statistics.
        /// </summary>
        public void StatReset()
        {
            ResetStatistics();
        }

        public void ResetStatistics()
        {
            TotalCount = 0;
            resetTime = Time.time;
        }

        /// <summary>
        /// Returns the current display string for the UI.
        /// </summary>
        public string GetDisplay()
        {
            float elapsedHours = (Time.time - resetTime) / 3600f;
            float partsPerHour = elapsedHours > 0 ? TotalCount / elapsedHours : 0;
            string line1 = "";
            if (PartsPerDayInDisplay)
            {
                 line1 = $"<color=green><size=50%>{PartsDescription}[1/day]</size></color>\n<size=150%>{PartsPerDay:F0}</size>";
            }
            else
            {
                 line1 = $"<color=green><size=50%>{PartsDescription}[1/h]</size></color>\n<size=150%>{PartsPerHour:F0}</size>";
            }
         
            string line2 = $"<size=50%>Total</size>\n<size=150%>{TotalCount}</size>";
            return $"{line1}\n\n{line2}";
        }

        public float GetResetTime()
        {
            return resetTime;
        }

       

        private void Start()
        {
            resetTime = Time.time;
        }
    }
}