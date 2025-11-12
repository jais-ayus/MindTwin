using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NaughtyAttributes;
using UnityEditor;

namespace realvirtual
{
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/performance-optimizer")]
    //! Dramatically improves runtime performance by intelligently combining static meshes into optimized batches.
    //! This professional optimization tool analyzes kinematic groups and combines meshes to reduce draw calls,
    //! essential for running complex industrial facilities in real-time. Enables smooth visualization of
    //! large-scale production lines, warehouses, and complete factories even in VR/AR applications. Maintains
    //! kinematic relationships while achieving significant frame rate improvements through batch optimization.
    public class PerformanceOptimizer : MonoBehaviour
    {
        [ReadOnly] public bool IsOptimized = false; //! Is the gameobject already optimized
        [ReadOnly] public  List<GameObject> OptimizedMeshes = new List<GameObject>(); //! List of all optimized meshes
        
        [HideInInspector] public List<MeshRenderer> DeactivatedRenderers = new List<MeshRenderer>();
        
        [Button("Optimize")]
        public void StartCombine()
        {
            #if UNITY_EDITOR
            var opt= this.gameObject.GetComponentsInChildren<PerformanceOptimizer>();
            if (opt.Length>1)
            {
                EditorUtility.DisplayDialog("Error",
                    "There is already a PerformanceOptimizer in the hierarchy below "+opt[1].gameObject.name+". Please remove this first",
                    "Ok");
                return;
            }
            // Display Warning Popup
            if (EditorUtility.DisplayDialog("Optimize Mehes",
                    "Optimizing the scene will deactivate all meshes under this component and combine them into one mesh per kinematic group. This will improve performance. The new meshes can be found under OptimizedMeshes Do you want to continue?",
                    "Yes", "No"))
            {
                if (IsOptimized)
                {
                    if (EditorUtility.DisplayDialog("Optimize Meshes",
                            "Meshes are already optimized - please first disaple this and then optimize again ", "Ok",
                            ""))
                        return;
                }
                else
                { 
                    MeshCombinerEditor.OptimizeSelectObject(this.gameObject,ref OptimizedMeshes,ref DeactivatedRenderers,false);
                }
            }
            #endif
        }

        // Extension:
        [Button("Undo Optimize")]
        public void StartUndoCmobine()
        {
            UndoOptimize();
            this.gameObject.SetActive(true);
        }
        
        private void UndoOptimize()
        {
            // Loop through all gameobjects in OptimizedMeshes and destroy them
            var i = 0;
            while (i < OptimizedMeshes.Count)
            {
                if (OptimizedMeshes[i] != null)
                {
                    DestroyImmediate(OptimizedMeshes[i]);
                    OptimizedMeshes.RemoveAt(i);
                }
                else
                    i++;
            }
            foreach (var renderers in DeactivatedRenderers)
            {
                renderers.enabled = true;
            }
            DeactivatedRenderers.Clear();
            OptimizedMeshes.Clear();
            
            this.IsOptimized = false;
        }
        [Button("Finalize Optimization")]
        public void FinalizeOptimization()
        {
#if UNITY_EDITOR
            if (EditorUtility.DisplayDialog("Finalize Meshes",
                    "Finalize the optimized structure. Undo is no longer possible. Do you want to continue?",
                    "Ok", "Cancel"))
            {
                foreach (var renderer in DeactivatedRenderers)
                {
                    MeshFilter meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
                    GameObject.DestroyImmediate(renderer);
                    GameObject.DestroyImmediate(meshFilter);
                }
                MeshCombinerEditor.FinalizeHierarchy(gameObject);
               

            }
#endif
        }


      
    }
}