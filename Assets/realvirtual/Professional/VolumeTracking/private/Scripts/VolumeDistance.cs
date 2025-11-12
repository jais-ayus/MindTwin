// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class VolumeDistance : MonoBehaviour
    {
        public VolumeTracker tracker;

        public float distance;

        private void OnDrawGizmosSelected()
        {
            if (tracker == null || tracker.distanceTracker.sdf == null)
            {
                return;
            }

            Vector3 volumePos = tracker.transform.InverseTransformPoint(transform.position) + Vector3.one * 0.5f;

            Color color = tracker.distanceTracker.sdf.GetPixelBilinear(volumePos.x, volumePos.y, volumePos.z);

            distance = color.r * tracker.transform.localScale.x;
            transform.localScale = Vector3.one * distance * 2;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, distance);
        }
    }
}