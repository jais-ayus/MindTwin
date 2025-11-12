using NaughtyAttributes;
using Simplex.Procedures;
using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class DistanceTracker
    {
        public Texture3D volume;
        public Texture3D sdf;

        public float threshold = 0.5f;
        ScalarTexture3DToSdfTexture3DProcedure _generator;

        [SerializeField] ScalarTexture3DToSdfTexture3DProcedure.DownSampling _downSampling =
            ScalarTexture3DToSdfTexture3DProcedure.DownSampling.None;

        [SerializeField] ScalarTexture3DToSdfTexture3DProcedure.Precision _precision =
            ScalarTexture3DToSdfTexture3DProcedure.Precision._32;

        public void LoadPersistent(VolumeTracker vt)
        {
            sdf = (Texture3D)UnityEngine.Resources.Load(vt.persistence.id + "_sdf");
        }

        [Button("Recompute")]
        public void CreateSDF(VolumeTracker vt)
        {
            vt.persistence.LoadVolume(vt);
            volume = vt.persistence.persistentVolume;


            _generator?.Release();
            _generator = new ScalarTexture3DToSdfTexture3DProcedure();

            _generator.Update(volume, threshold, _downSampling, _precision, false, false);


            sdf = new Texture3D(volume.width, volume.height, volume.depth, volume.format, false);
            sdf.filterMode = FilterMode.Trilinear;
            sdf.wrapMode = TextureWrapMode.Clamp;
            sdf.name = "SDF";

            string sdf_id = vt.persistence.id + "_sdf";

#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar("Saving SDF", "Saving SDF", 0);
#endif

            VolumeDownload download = new VolumeDownload(sdf, sdf_id, true);
            download.OnDownloadComplete.AddListener(() =>
            {
                vt.visualizerMode = DistanceVisualizer.Mode.Segments;
                vt.ApplyVisualizerMode();

#if UNITY_EDITOR
                UnityEditor.EditorUtility.ClearProgressBar();
#endif
            });
            download.DownloadVolume(_generator.sdfTexture);
        }
    }
}