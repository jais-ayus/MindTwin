// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using UnityEditor;
using UnityEngine;
using System.IO;

#if !UNITY_CLOUD_BUILD
namespace realvirtual
{
    class MyAllPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool Game4AutomationImport = false;
            bool GlobalCsImported = false;

            foreach (string str in importedAssets)
            {
                // Only trigger on specific important files
                if (str.Contains("Assets/realvirtual/private/Global.cs"))
                {
                    GlobalCsImported = true;
                    Game4AutomationImport = true;
                    break;
                }
                // Or if it's the initial import (checking for a core file)
                else if (str.Contains("Assets/realvirtual/private/Editor/realvirtual.editor.asmdef"))
                {
                    Game4AutomationImport = true;
                }
            }

#if !DEV
            if (Game4AutomationImport && !Application.isPlaying)
            {
                if (GlobalCsImported)
                    Logger.Message("Updating realvirtual");
                else
                    Logger.Message("Updating realvirtual - Initial import detected");
                // Disable Interact
                string MenuName = "realvirtual/Enable Interact (Pro)";
                EditorPrefs.SetBool(MenuName, false);
                ProjectSettingsTools.SetStandardSettings(false);

                EditorApplication.delayCall += () =>
                {
                    var window = ScriptableObject.CreateInstance<HelloWindow>();
                    window.Open();
                };
                
                // Delete old QuickToggle Location if existant
                if (Directory.Exists("Assets/realvirtual/private/Editor/QuickToggle"))
                {
                    Directory.Delete("Assets/realvirtual/private/Editor/QuickToggle",true);
                }
                
                // Delete old Planner if existant
                if (Directory.Exists("Assets/realvirtual/private/Planner"))
                {
                    Directory.Delete("Assets/realvirtual/private/Planner",true);
                }
               
            }
#endif

        }
    }
}
#endif