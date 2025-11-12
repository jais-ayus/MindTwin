// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class VolumeTracker : MonoBehaviour
    {
        public VolumeTrackerSettings settings;
        [HideInInspector] public VolumePersistence persistence = new VolumePersistence();


        public DistanceTracker distanceTracker = new DistanceTracker();
        public float minOccupationTime = 0.0f;


        public VolumetricGrid[] targets;
        [NaughtyAttributes.ReadOnly] public int trackedCellCount;
        public bool targetGizmos = true;

        public DistanceVisualizer.Mode visualizerMode;

        [HideInInspector] public RenderTexture volumeTexture;
        private VolumeTrackerGPU gpuTracker;
        private bool isTracking;

        public float surfaceLevel = 0.5f;

        [Range(0, 1)] public float segment1 = 0.1f;
        [Range(0, 1)] public float segment2 = 0.2f;
        [Range(0, 1)] public float segment3 = 0.3f;

        public List<DistanceVisualizer> visualizers = new List<DistanceVisualizer>();


        public void UpdateMaterialPropertyBlock(MeshRenderer renderer)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetTexture("_SDF", distanceTracker.sdf);
            mpb.SetMatrix("_WorldToVolume", transform.worldToLocalMatrix);
            mpb.SetFloat("_Scale", transform.localScale.x);
            mpb.SetFloat("_Segment1", segment1);
            mpb.SetFloat("_Segment2", segment2);
            mpb.SetFloat("_Segment3", segment3);
            renderer.SetPropertyBlock(mpb);
        }

        public void UpdateMaterialPropertyBlocks()
        {
            if (visualizers == null)
            {
                return;
            }

            List<MeshRenderer> renderers = new List<MeshRenderer>();

            foreach (DistanceVisualizer visualizer in visualizers)
            {
                if (visualizer == null)
                {
                    continue;
                }

                if (visualizer.applyToChildren)
                {
                    renderers.AddRange(visualizer.GetComponentsInChildren<MeshRenderer>());
                }
                else
                {
                    renderers.Add(visualizer.GetComponent<MeshRenderer>());
                }
            }

            foreach (MeshRenderer renderer in renderers)
            {
                UpdateMaterialPropertyBlock(renderer);
            }
        }


        public void SetScale()
        {
            transform.localScale = settings.size;
        }

        public void ApplyVisualizerMode()
        {
            if (distanceTracker.sdf == null)
            {
                return;
            }


            UpdateMaterialPropertyBlocks();
            foreach (DistanceVisualizer visualizer in visualizers)
            {
                if (visualizer == null)
                {
                    continue;
                }

                visualizer.ChangeMode(visualizerMode);
            }
        }

        private void OnValidate()
        {
            if (persistence != null)
            {
                persistence.id = settings.id;
                persistence.LoadVolume(this);
            }
            SetScale();
            ApplyVisualizerMode();
        }

        public void RefreshTracking()
        {
            RefreshTrackingVisualizers();
            RefreshTrackingTargets();
        }


        public void RefreshTrackingVisualizers()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Initializing Targets", "Initializing Targets", 0.1f);
#endif


            //Destroy all existing visualizers


            for (int i = 0; i < visualizers.Count; i++)
            {
                DestroyImmediate(visualizers[i]);
            }

            visualizers = new List<DistanceVisualizer>();

            Group[] groupObjects =
                GameObject.FindObjectsByType<Group>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> groupNamesVisual = new HashSet<string>(settings.visualizingGroups);


            foreach (Group group in groupObjects)
            {
                if (group == null)
                {
                    continue;
                }

                if (groupNamesVisual.Contains(group.GroupName))
                {
                    GameObject root = group.gameObject;

                    if (root.GetComponent<DistanceVisualizer>() == null)
                    {
                        DistanceVisualizer visualizer = root.AddComponent<DistanceVisualizer>();

                        visualizer.applyToChildren = true;
                        visualizer.tracker = this;
                        visualizers.Add(visualizer);
                    }
                }
            }


#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }


        [Button("Setup Targets")]
        public void RefreshTrackingTargets()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Initializing Targets", "Initializing Targets", 0.1f);
#endif

            trackedCellCount = 0;

            //Destroy all existing volumetric grids
            foreach (VolumetricGrid target in targets)
            {
                DestroyImmediate(target);
            }


            // for each groupname in tracked groups find all objects with that tag

            Group[] groupObjects = GameObject.FindObjectsByType<Group>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            HashSet<string> groupNamesTracked = new HashSet<string>(settings.trackedGroups);
            HashSet<string> groupNamesVisual = new HashSet<string>(settings.visualizingGroups);

            List<VolumetricGrid> volumetricGrids = new List<VolumetricGrid>();

            foreach (Group group in groupObjects)
            {
                if (group == null)
                {
                    continue;
                }

                if (groupNamesTracked.Contains(group.GroupName))
                {
                    GameObject root = group.gameObject;

                    // find all mesh renderers in the root

                    MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>();

                    // for each mesh renderer create a volumetric grid

                    targets = new VolumetricGrid[meshRenderers.Length];

                    for (int i = 0; i < meshRenderers.Length; i++)
                    {
                        MeshRenderer meshRenderer = meshRenderers[i];
                        VolumetricGrid grid = meshRenderer.GetComponent<VolumetricGrid>();
                        if (grid == null)
                        {
                            grid = meshRenderer.gameObject.AddComponent<VolumetricGrid>();
                        }

                        grid.resolution = settings.resolution;
                        grid.mesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
                        grid.Init();
                        volumetricGrids.Add(grid);
                        trackedCellCount += grid.cellPositions.Length;
                    }
                }
            }

            targets = volumetricGrids.ToArray();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }

        [Button("Start Tracking")]
        public void StartTracking()
        {
            ReleaseTracker();
            InitTracker();
            InitVisuals();
            isTracking = true;
        }

        [Button("Stop Tracking")]
        public void StopTracking()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Saving Volume", "Saving Volume", 0);
#endif

            SaveTracking();
        }

        public bool IsTracking()
        {
            return isTracking;
        }

        void SaveCompleted()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Creating SDF", "Creating SDF", 0);
#endif

            RecomputeDistanceMap();
        }

        public void RecomputeDistanceMap()
        {
            distanceTracker.threshold = minOccupationTime;
            distanceTracker.CreateSDF(this);
        }


        void SaveTracking()
        {
            isTracking = false;

            visualizerMode = DistanceVisualizer.Mode.Original;
            ApplyVisualizerMode();

            persistence.OnSaveComplete.RemoveAllListeners();
            persistence.OnSaveComplete.AddListener(SaveCompleted);
            persistence.SaveVolume();
        }

        private void FixedUpdate()
        {
            if (isTracking)
            {
                gpuTracker.Iterate();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, settings.size);

            if (targetGizmos)
            {
                foreach (VolumetricGrid target in targets)
                {
                    target.DrawGizmos(Color.green);
                }
            }
        }

        private void InitVisuals()
        {
            VolumeTrackerVisual trackerVisual = GetComponentInChildren<VolumeTrackerVisual>(true);
            if (trackerVisual == null)
            {
                return;
            }

            trackerVisual.gameObject.SetActive(true);
            trackerVisual.Init();
        }

        public void InitTracker()
        {
            Vector3Int capacities = new Vector3Int(
                Mathf.CeilToInt(settings.size.x / settings.resolution),
                Mathf.CeilToInt(settings.size.y / settings.resolution),
                Mathf.CeilToInt(settings.size.z / settings.resolution)
            );

            volumeTexture = new RenderTexture(capacities.x, capacities.y, 0, settings.format);
            volumeTexture.name = "VolumeTexture";
            volumeTexture.wrapMode = TextureWrapMode.Clamp;
            volumeTexture.filterMode = FilterMode.Trilinear;
            volumeTexture.enableRandomWrite = true;
            volumeTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            volumeTexture.volumeDepth = capacities.z;


            gpuTracker = new VolumeTrackerGPU();
            gpuTracker.Init(targets, transform, settings.size, settings.resolution, trackedCellCount, capacities,
                volumeTexture, settings.timeTracking);
        }

        public void ReleaseTracker()
        {
            if (gpuTracker != null)
            {
                gpuTracker.Release();
            }

            if (volumeTexture != null && volumeTexture.IsCreated())
            {
                volumeTexture.Release();
            }
        }

        void OnDestroy()
        {
            ReleaseTracker();
        }

        private void OnApplicationQuit()
        {
            ReleaseTracker();
        }
    }

    [System.Serializable]
    public class VolumeTrackerSettings
    {
        public string id;
        public Vector3 size = new Vector3(1, 1, 1);
        public float resolution = 0.1f;
        public bool timeTracking = false;
        [HideInInspector] public RenderTextureFormat format = RenderTextureFormat.RFloat;
        public string[] trackedGroups;
        public string[] visualizingGroups;
    }
}