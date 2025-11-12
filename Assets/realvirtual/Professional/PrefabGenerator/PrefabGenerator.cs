using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace realvirtual
{
    //! Automates creation of optimized prefabs with embedded mesh assets for efficient industrial model management.
    //! This professional utility handles complex prefab generation from imported CAD models, automatically
    //! embedding dynamically created meshes as sub-assets. Essential for converting imported industrial
    //! equipment into reusable prefabs while preserving mesh data and optimizations. Streamlines workflow
    //! for building libraries of standardized industrial components for virtual commissioning projects.
    public static class PrefabGenerator
    {
        #if UNITY_EDITOR
        public static void CreatePrefab(GameObject go)
        {
            Selection.activeGameObject = go;
            CreatePrefabWithSubAssets();
        }
        
        [MenuItem("GameObject/realvirtual/Create Prefab (Pro)", false, 10)]
        public static void CreatePrefabWithSubAssets()
        {
            // Select the GameObject to create a prefab from
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
            {
                Debug.LogError("No GameObject selected. Please select a GameObject to create a prefab.");
                return;
            }
            

            // Define the save path under Assets/
            string savePath = "Assets/" + selectedObject.name + ".prefab";
            
            // progress bar
            EditorUtility.DisplayProgressBar("Creating Prefab", "Creating prefab with sub-assets for object " + selectedObject.name, 0.5f);

            // Create an empty prefab asset
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(selectedObject, savePath, InteractionMode.UserAction);

            if (prefab == null)
            {
                Debug.LogError($"Failed to create prefab at {savePath}");
                return;
            }
            // Process dynamically created meshes in child MeshFilters
            MeshFilter[] meshFilters = selectedObject.GetComponentsInChildren<MeshFilter>();
            Dictionary<string, int> meshNameCount = new Dictionary<string, int>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;

                if (mesh != null && AssetDatabase.GetAssetPath(mesh) == "")
                {
                    // Check if the mesh name already exists
                    string meshName = mesh.name;
                    if (meshNameCount.ContainsKey(meshName))
                    {
                        meshNameCount[meshName]++;
                        meshName = $"{meshName} ({meshNameCount[meshName]})";
                        mesh.name = meshName;
                    }
                    else
                    {
                        meshNameCount.Add(meshName, 1);
                    }
                    
                    // Add the duplicated mesh as a sub-asset to the prefab
                    AssetDatabase.AddObjectToAsset(mesh, savePath);

                    // Reassign the new mesh to the MeshFilter
                    //meshFilter.sharedMesh = newMesh;

                    Debug.Log($"Added mesh {mesh.name} as a sub-asset to {savePath}");
                }
            }
            
            // Save all assets
            PrefabUtility.ApplyPrefabInstance(selectedObject, InteractionMode.UserAction);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);


            Debug.Log($"Prefab created at {savePath} with meshes stored as sub-assets.");
            
            EditorUtility.ClearProgressBar();
            
            // select prefab in project
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(savePath);
            
            // popup with save location
            EditorUtility.DisplayDialog("Prefab Created", $"Prefab created at {savePath}", "OK");
            
        }
        #endif
    }
}
