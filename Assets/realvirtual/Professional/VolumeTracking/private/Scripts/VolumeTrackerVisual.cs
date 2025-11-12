// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using NaughtyAttributes;
using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class VolumeTrackerVisual : MonoBehaviour
    {
        public VolumeTracker tracker;
        public Material[] materials;


        [Button]
        public void Init()
        {
            tracker = GetComponentInParent<VolumeTracker>();
            transform.position = tracker.transform.position;
            transform.localScale = tracker.settings.size;
            transform.rotation = tracker.transform.rotation;

            UpdateMaterials();
        }

        void UpdateMaterials()
        {
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetTexture("_volumeTex", tracker.volumeTexture);
            }
        }
    }
}