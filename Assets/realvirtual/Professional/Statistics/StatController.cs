// // realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// // (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System.Linq;
using UnityEngine;

namespace realvirtual
{
    //! Orchestrates industrial statistics collection and periodic reset cycles for performance monitoring systems.
    //! This professional controller manages statistics gathering across automation systems, coordinating reset cycles
    //! for consistent measurement periods. Essential for production monitoring, OEE calculations, cycle time analysis,
    //! and performance benchmarking in virtual commissioning. Enables systematic data collection for process
    //! optimization and validation of automation concepts against production targets.
    public class StatController : MonoBehaviour
    {
        [Tooltip("Time in seconds after which all statistics are reset.")]
        public float ResetStatistics = 60f;



        private void Start()
        {
            Invoke("ResetAllStatistics", ResetStatistics);
        }

        /// <summary>
        /// Calls the StatReset method on all components implementing IStatReset.
        /// </summary>
        private void ResetAllStatistics()
        {
            // Find all components implementing IStatReset in the scene
            var behaviors = Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<IStatReset>();
            

            foreach (var behavior in behaviors)
            {
                behavior.StatReset();
            }
        }
    }
}