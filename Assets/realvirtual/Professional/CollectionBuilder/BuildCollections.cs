using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    public static class BuildCollections
    {
        public static string Path;
        public static string PathScenes;
        public static bool BuildAllCollections = true;
        public static bool BuildForWindows = true;
        public static bool BuildForMac = true;
        public static bool BuildForWebGL = true;
        public static string CollectionName;
        public static List<string> AssetBundlePrefabNames = new List<string>();
        public static List<string> AssetBundleSceneNames = new List<string>();

        public static void BuildAllAssetBundles()
        {
#if UNITY_EDITOR
            string assetBundleDirectory="";
            string assetBundleDirectoryScenes="";
            string ending=".rvc";
            if (AssetBundlePrefabNames.Contains(CollectionName))
            {
                if (Path == null)
                    assetBundleDirectory = "Assets/StreamingAssets/realvirtual/libraries";
                else
                    assetBundleDirectory = Path;
                
                ending = ".rvc";
            }
            else if(AssetBundleSceneNames.Contains(CollectionName))
            {
                if(PathScenes==null)
                    assetBundleDirectoryScenes = "Assets/StreamingAssets/realvirtual/scenes";
                else
                    assetBundleDirectoryScenes = PathScenes;
                
                ending=".realvirtual";
            }
            BuildAllAssetBundles(assetBundleDirectory,assetBundleDirectoryScenes,ending);
#endif
        }

        public static string GetCurrentPath()
        {
            string assetBundleDirectory;
            if (Path == null)
                assetBundleDirectory = "Assets/StreamingAssets/realvirtual/libraries";
            else
                assetBundleDirectory = Path;
            return assetBundleDirectory;
        }
        public static string GetCurrentScenePath()
        {
            string assetBundleDirectory;
            if (PathScenes == null)
                assetBundleDirectory = "Assets/StreamingAssets/realvirtual/scenes";
            else
                assetBundleDirectory = PathScenes;
            return assetBundleDirectory;
        }

        public static void BuildAllAssetBundles(string path,string scenePath,string ending)
        {
#if UNITY_EDITOR
            AssetBundleBuild[] buildMap = null;
            if (BuildAllCollections)
            {
                //get all asset bundles
                foreach (var assetBundleName in AssetBundlePrefabNames)
                    buildMap = new AssetBundleBuild[]
                    {
                        new()
                        {
                            assetBundleName = assetBundleName + ".rvc",
                            assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName)
                        }
                    };
                BuildAsset(buildMap,path);
                
                foreach (var assetBundleName in AssetBundleSceneNames)
                    buildMap = new AssetBundleBuild[]
                    {
                        new()
                        {
                            assetBundleName = assetBundleName + ".realvirtual",
                            assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName)
                        }
                    };
                BuildAsset(buildMap,scenePath);
            }
            else
            {
                var conveyorsBundle = new AssetBundleBuild
                {
                    assetBundleName = CollectionName + ending,
                    assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(CollectionName)
                };
                buildMap = new[] { conveyorsBundle };
                if(path!="")
                    BuildAsset(buildMap,path);
                if(scenePath!="")
                    BuildAsset(buildMap,scenePath);

            }
#endif
        }
#if UNITY_EDITOR
        private static void BuildAsset(AssetBundleBuild[] buildMap, string path)
        {

            if (BuildForWindows)
            {
                var assetBundleDirectoryWin = path + "/WIN";
                if (!Directory.Exists(assetBundleDirectoryWin)) Directory.CreateDirectory(assetBundleDirectoryWin);
                BuildPipeline.BuildAssetBundles(assetBundleDirectoryWin, buildMap,
                    BuildAssetBundleOptions.None,
                    BuildTarget.StandaloneWindows);

                Debug.Log("Collection created in " + assetBundleDirectoryWin);
            }

            if (BuildForWebGL)
            {
                var assetBundleDirectoryWWW = path + "/WWW";
                if (!Directory.Exists(assetBundleDirectoryWWW)) Directory.CreateDirectory(assetBundleDirectoryWWW);
                BuildPipeline.BuildAssetBundles(assetBundleDirectoryWWW, buildMap,
                    BuildAssetBundleOptions.None,
                    BuildTarget.WebGL);

                Debug.Log("Collection created in " + assetBundleDirectoryWWW);
            }

            if (BuildForMac)
            {
                var assetBundleDirectoryMac = path + "/MAC";
                if (!Directory.Exists(assetBundleDirectoryMac)) Directory.CreateDirectory(assetBundleDirectoryMac);
                BuildPipeline.BuildAssetBundles(assetBundleDirectoryMac, buildMap,
                    BuildAssetBundleOptions.None,
                    BuildTarget.StandaloneOSX);

                Debug.Log("Collection created in " + assetBundleDirectoryMac);
            }
        }
#endif
    }
}