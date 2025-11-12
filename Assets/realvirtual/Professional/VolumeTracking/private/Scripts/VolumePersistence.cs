// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace realvirtual.VolumeTracking
{
    [System.Serializable]
    public class VolumePersistence
    {
        [HideInInspector] public VolumeTracker tracker;

        public string id;
        public TextureFormat format;

        public Texture3D persistentVolume;

        public UnityEvent OnSaveComplete = new UnityEvent();

        public void LoadVolume(VolumeTracker tracker)
        {
            this.tracker = tracker;
            persistentVolume = (Texture3D)UnityEngine.Resources.Load(id + "_volume");
            DistanceTracker sdf = tracker.distanceTracker;

            sdf.volume = persistentVolume;
            sdf.LoadPersistent(tracker);
        }


        [Button]
        public void SaveVolume()
        {
            persistentVolume = new Texture3D(tracker.volumeTexture.width, tracker.volumeTexture.height,
                tracker.volumeTexture.volumeDepth, format, false);
            persistentVolume.filterMode = FilterMode.Trilinear;
            persistentVolume.wrapMode = TextureWrapMode.Clamp;

            VolumeDownload download = new VolumeDownload(persistentVolume, id + "_volume", true);

            download.OnDownloadComplete.AddListener(() => { OnSaveComplete.Invoke(); });

            download.DownloadVolume(tracker.volumeTexture);
        }
    }
}