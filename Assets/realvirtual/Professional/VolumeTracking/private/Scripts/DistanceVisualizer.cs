using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class DistanceVisualizer : MonoBehaviour
    {
        public VolumeTracker tracker;

        public enum Mode
        {
            Original,
            Gradient,
            Segments,
            Cutout
            //Slice
        }


        [OnValueChanged("UpdateMode")] public Mode mode = Mode.Original;
        [SerializeField] private Mode currentMode = Mode.Original;
        public bool applyToChildren = false;

        [SerializeField] private List<OriginalMaterials> originalMaterials;

        public void ChangeMode(Mode mode)
        {
            this.mode = mode;
            UpdateMode();
        }

        void UpdateMode()
        {
            if (mode != currentMode)
            {
                if (currentMode == Mode.Original || originalMaterials == null)
                {
                    GetOriginalMaterials();
                }

                if (mode == Mode.Original)
                {
                    ApplyOriginalMaterials();
                }

                if (mode == Mode.Gradient)
                {
                    ApplyMaterial((Material)UnityEngine.Resources.Load("VolumeDistanceGradient"));
                }

                if (mode == Mode.Segments)
                {
                    ApplyMaterial((Material)UnityEngine.Resources.Load("VolumeDistanceSegments"));
                }

                if (mode == Mode.Cutout)
                {
                    ApplyMaterial((Material)UnityEngine.Resources.Load("VolumeDistanceSegmentsCutout"));
                }


                currentMode = mode;
            }
        }

        void ApplyOriginalMaterials()
        {
            foreach (OriginalMaterials originalMaterial in originalMaterials)
            {
                originalMaterial.renderer.sharedMaterials = originalMaterial.materials;
            }
        }

        void ApplyMaterial(Material material)
        {
            foreach (OriginalMaterials originalMaterial in originalMaterials)
            {
                Material[] materials = new Material[originalMaterial.materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }

                originalMaterial.renderer.sharedMaterials = materials;
            }
        }


        void GetOriginalMaterials()
        {
            originalMaterials = new List<OriginalMaterials>();


            MeshRenderer[] renderers = null;

            if (applyToChildren)
            {
                renderers = GetComponentsInChildren<MeshRenderer>(true);
            }
            else
            {
                renderers = GetComponents<MeshRenderer>();
            }

            foreach (MeshRenderer renderer in renderers)
            {
                originalMaterials.Add(new OriginalMaterials()
                {
                    renderer = renderer,
                    materials = renderer.sharedMaterials
                });
            }
        }
    }

    [System.Serializable]
    public class OriginalMaterials
    {
        public MeshRenderer renderer;
        public Material[] materials;
    }
}