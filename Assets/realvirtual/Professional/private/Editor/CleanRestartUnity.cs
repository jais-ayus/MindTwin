// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.IO;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    public static class CleanRestartUnity
    {
        [MenuItem("realvirtual/Settings/Clean Restart Unity (Pro)", false, 510)]
        private static void CleanRestartUnityEditor()
        {
#if REALVIRTUAL_PROFESSIONAL
            if (EditorUtility.DisplayDialog("Clean Restart Unity", 
                "This will:\n" +
                "• Close Unity\n" +
                "• Delete the Library folder (cache, imported assets, etc.)\n" +
                "• Restart Unity with a clean project state\n\n" +
                "This process may take several minutes as Unity will need to reimport all assets.\n\n" +
                "Continue?", 
                "Yes, Clean Restart", "Cancel"))
            {
                PerformCleanRestart();
            }
#else
            EditorUtility.DisplayDialog("Professional Feature", 
                "Clean Restart Unity is only available in realvirtual Professional.", "OK");
#endif
        }
        
#if REALVIRTUAL_PROFESSIONAL
        private static void PerformCleanRestart()
        {
            // Get the project path (parent of Assets folder)
            string projectPath = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            string libraryPath = Path.Combine(projectPath, "Library");
            
            Debug.Log($"Performing clean restart...");
            Debug.Log($"Project path: {projectPath}");
            Debug.Log($"Library path: {libraryPath}");
            
            // Save the current scene and project
            EditorUtility.DisplayProgressBar("Clean Restart", "Saving project...", 0.1f);
            AssetDatabase.SaveAssets();
            
            try
            {
                // Check if Library folder exists
                if (Directory.Exists(libraryPath))
                {
                    EditorUtility.DisplayProgressBar("Clean Restart", "Preparing to delete Library folder...", 0.3f);
                    
                    // Create a batch file/script to delete Library and restart Unity
                    string scriptPath = CreateCleanupScript(projectPath, libraryPath);
                    
                    EditorUtility.DisplayProgressBar("Clean Restart", "Starting cleanup process...", 0.5f);
                    
                    // Start the cleanup script and close Unity
                    System.Diagnostics.Process.Start(scriptPath);
                    
                    // Give the script a moment to start
                    System.Threading.Thread.Sleep(1000);
                    
                    // Close Unity
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogWarning("Library folder not found. Simply restarting Unity...");
                    EditorApplication.Exit(0);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error during clean restart: {ex.Message}");
                EditorUtility.DisplayDialog("Clean Restart Error", 
                    $"Failed to perform clean restart:\n{ex.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        
        private static string CreateCleanupScript(string projectPath, string libraryPath)
        {
            string scriptPath;
            string scriptContent;
            
#if UNITY_EDITOR_WIN
            // Windows batch script with improved process handling
            scriptPath = Path.Combine(Path.GetTempPath(), "unity_clean_restart.bat");
            scriptContent = $@"@echo off
echo Starting Unity Clean Restart...
echo Waiting for Unity to fully close...

:wait_for_unity
timeout /t 2 /nobreak >nul
tasklist /FI ""IMAGENAME eq Unity.exe"" 2>NUL | find /I /N ""Unity.exe"">NUL
if ""%ERRORLEVEL%""==""0"" (
    echo Unity still running, waiting...
    goto wait_for_unity
)

echo Unity has closed. Waiting additional 3 seconds for file handles to release...
timeout /t 3 /nobreak >nul

echo Attempting to delete Library folder...
if exist ""{libraryPath}"" (
    echo Deleting Library folder: {libraryPath}
    rmdir /s /q ""{libraryPath}"" 2>nul
    if exist ""{libraryPath}"" (
        echo First attempt failed, trying with force...
        timeout /t 2 /nobreak >nul
        for /d %%i in (""{libraryPath}\*"") do rmdir /s /q ""%%i"" 2>nul
        del /f /s /q ""{libraryPath}\*.*"" 2>nul
        rmdir /s /q ""{libraryPath}"" 2>nul
    )
    if exist ""{libraryPath}"" (
        echo Warning: Some files in Library folder could not be deleted.
        echo This may be due to antivirus or file system locks.
    ) else (
        echo Library folder successfully deleted.
    )
) else (
    echo Library folder not found.
)

echo Restarting Unity...
start """" ""{EditorApplication.applicationPath}"" -projectPath ""{projectPath}""

echo Cleanup script completed.
timeout /t 2 /nobreak >nul
del ""%~f0""
";
#else
            // Unix shell script (macOS/Linux) with improved process handling
            scriptPath = Path.Combine(Path.GetTempPath(), "unity_clean_restart.sh");
            
            // Build script content without # characters in C# string literals
            var shebang = "#!/bin/bash";
            var comment = "# Wait for Unity processes to fully terminate";
            
            scriptContent = shebang + @"
echo ""Starting Unity Clean Restart...""
echo ""Waiting for Unity to fully close...""

" + comment + @"
while pgrep -x ""Unity"" > /dev/null; do
    echo ""Unity still running, waiting...""
    sleep 2
done

echo ""Unity has closed. Waiting additional 3 seconds for file handles to release...""
sleep 3

echo ""Attempting to delete Library folder...""
if [ -d """ + libraryPath + @""" ]; then
    echo ""Deleting Library folder: " + libraryPath + @"""
    rm -rf """ + libraryPath + @"""
    if [ -d """ + libraryPath + @""" ]; then
        echo ""Warning: Library folder could not be completely deleted.""
        echo ""This may be due to file system locks or permissions.""
    else
        echo ""Library folder successfully deleted.""
    fi
else
    echo ""Library folder not found.""
fi

echo ""Restarting Unity...""";

#if UNITY_EDITOR_OSX
            scriptContent += @"
open -a """ + EditorApplication.applicationPath + @""" --args -projectPath """ + projectPath + @"""";
#else
            scriptContent += @"
""" + EditorApplication.applicationPath + @""" -projectPath """ + projectPath + @""" &";
#endif

            scriptContent += @"

echo ""Cleanup script completed.""
sleep 2
rm ""$0""
";
#endif

            // Write the script file
            File.WriteAllText(scriptPath, scriptContent);
            
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            // Make the script executable on Unix systems
            try
            {
                var chmodProcess = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{scriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(chmodProcess)?.WaitForExit();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Could not make script executable: {ex.Message}");
            }
#endif
            
            Debug.Log($"Created cleanup script: {scriptPath}");
            return scriptPath;
        }
#endif
    }
}