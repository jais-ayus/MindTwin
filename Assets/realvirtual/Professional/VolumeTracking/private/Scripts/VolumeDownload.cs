// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace realvirtual.VolumeTracking
{
    public class VolumeDownload
    {
        public Texture3D dst;
        public NativeArray<float> data;
        public string id;
        public bool saveAsset;

        public UnityEvent OnDownloadComplete = new UnityEvent();

        public VolumeDownload(Texture3D dst, string id, bool saveAsset)
        {
            this.dst = dst;
            this.id = id;
            this.saveAsset = saveAsset;
        }

        public void DownloadVolume(RenderTexture src)
        {
            data = new NativeArray<float>(src.width * src.height * src.volumeDepth, Allocator.Persistent);

            AsyncGPUReadback.RequestIntoNativeArray(ref data, src, 0, OnCompleteReadback);
        }

        void OnCompleteReadback(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.LogError("GPU Readback encountered an error.");
                return;
            }

            // Process the native array data here
            Debug.Log("GPU Readback completed successfully.");

            dst.SetPixelData(data, 0);
            dst.Apply();

            if (saveAsset)
            {
                SaveTexture3DAsset(dst,
                    "Assets/realvirtual/Professional/VolumeTracking/VolumeAssets/Resources/" + id + ".asset");
            }

            data.Dispose();

            OnDownloadComplete.Invoke();
        }

        void SaveTexture3DAsset(Texture3D texture3D, string path)
        {
            #if UNITY_EDITOR
            // Save the Texture3D asset to disk
            AssetDatabase.CreateAsset(texture3D, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            #endif
        }
    }
}