using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
#if UNITY_EDITOR
    public class CollectionBuilderWindow : EditorWindow
    {
        private bool BuildForMac;
        private bool BuildForWebGL;
        private bool BuildForWindows = true;
        private string CollectionName;
        private string Path;
        private string ScenePath;
        private List<string> assetBundlePrefabNames=new List<string>();
        private List<string> assetBundleSceneNames=new List<string>();

        private Vector2 scrollPos;

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
            var width = position.width;
            GUILayout.BeginVertical();
            GUILayout.Width(10);
            GUILayout.EndVertical();

            EditorGUILayout.Separator();

            GUILayout.BeginVertical();
            
            EditorGUILayout.BeginHorizontal();
            // Label for set build platform
            GUILayout.Label("Choose build platform: ", GUILayout.Width(width / 2 - 10));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Build for Windows", GUILayout.Width(width / 4 - 10));
            BuildForWindows = EditorGUILayout.Toggle("", BuildForWindows, GUILayout.Width(width / 4 - 10));
            BuildCollections.BuildForWindows = BuildForWindows;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Build for WebGL", GUILayout.Width(width / 4 - 10));
            BuildForWebGL = EditorGUILayout.Toggle("", BuildForWebGL, GUILayout.Width(width / 4 - 10));
            BuildCollections.BuildForWebGL = BuildForWebGL;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Build for Mac", GUILayout.Width(width / 4 - 10));
            BuildForMac = EditorGUILayout.Toggle("", BuildForMac, GUILayout.Width(width / 4 - 10));
            BuildCollections.BuildForMac = BuildForMac;

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Save Path Libraries:", GUILayout.Width(width / 4 - 10));
            Path = BuildCollections.GetCurrentPath();
            Path = EditorGUILayout.TextField(Path);
            BuildCollections.Path = Path;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Save Path Scenes:", GUILayout.Width(width / 4 - 10));
            ScenePath = BuildCollections.GetCurrentScenePath();
            ScenePath = EditorGUILayout.TextField(ScenePath);
            BuildCollections.PathScenes = ScenePath;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();
            
            EditorGUILayout.BeginHorizontal();
            string buttontext;
            buttontext = "Build All Bundles";
            if (GUILayout.Button(buttontext))
            {
                var window =
                    (CollectionBuilderWindow)GetWindow(typeof(CollectionBuilderWindow));
                window.Close();
                BuildCollections.AssetBundlePrefabNames=new List<string>();
                BuildCollections.AssetBundleSceneNames=new List<string>();
                BuildCollections.AssetBundlePrefabNames = assetBundlePrefabNames;
                BuildCollections.AssetBundleSceneNames = assetBundleSceneNames;
                BuildCollections.BuildAllCollections = true;
                BuildCollections.BuildAllAssetBundles();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();
            
            // get all available asset bundles in the project
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Available Bundles for build: ", GUILayout.Width(width / 2 - 10));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Separator();
            
            if ((assetBundlePrefabNames == null && assetBundleSceneNames == null) || (assetBundlePrefabNames.Count == 0 && assetBundleSceneNames.Count == 0))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("No prefab asset bundles found.");
                if (GUILayout.Button("Refresh"))
                {
                    RefreshAssetBundleList();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                foreach (var bundleName in assetBundlePrefabNames)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Build "+bundleName))
                    {
                        OnBundleButtonClicked(bundleName);
                    }
                    EditorGUILayout.EndHorizontal();
            
                    EditorGUILayout.Separator();
                }
                foreach (var bundleName in assetBundleSceneNames)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Build "+bundleName))
                    {
                        OnBundleButtonClicked(bundleName);
                    }
                    EditorGUILayout.EndHorizontal();
            
                    EditorGUILayout.Separator();
                }
            }
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }


        [MenuItem("realvirtual/Build Bundles (Pro)",false,502)]
        private static void Init()
        {
            var window =
                (CollectionBuilderWindow)GetWindow(typeof(CollectionBuilderWindow));
            window.titleContent = new GUIContent("Bundle Builder");
            window.Show();
        }
        
        private void OnEnable()
        {
            RefreshAssetBundleList();
        }
        
        private void OnBundleButtonClicked(string bundleName)
        {
            BuildCollections.AssetBundlePrefabNames=new List<string>();
            BuildCollections.AssetBundleSceneNames=new List<string>();
            BuildCollections.AssetBundlePrefabNames = assetBundlePrefabNames;
            BuildCollections.AssetBundleSceneNames = assetBundleSceneNames;
            BuildCollections.CollectionName = bundleName;
            BuildCollections.BuildAllCollections = false;
            BuildCollections.BuildAllAssetBundles();
        }
        private void RefreshAssetBundleList()
        {
            assetBundlePrefabNames.Clear();
            assetBundleSceneNames.Clear();
            string[] assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            string[] guidsScene=AssetDatabase.FindAssets("t:Scene");
            string[] guidsPrefab=AssetDatabase.FindAssets("t:Prefab");
            foreach (var name in assetBundleNames)
            {
                foreach (string guid in guidsScene)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                     AssetImporter importer=AssetImporter.GetAtPath(assetPath);
                    if(importer.assetBundleName==name)
                    {
                        if(!assetBundleSceneNames.Contains(name))
                            assetBundleSceneNames.Add(name);
                    }
                }
                foreach (string prefab in guidsPrefab)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(prefab);
                    AssetImporter importer=AssetImporter.GetAtPath(assetPath);
                    if(importer.assetBundleName==name)
                    {
                        if(!assetBundlePrefabNames.Contains(name))
                            assetBundlePrefabNames.Add(name);
                        
                    }
                }
            }
        }
    }
#endif
}