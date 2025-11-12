// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Reflection;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Overlays;
#endif

namespace realvirtual
{
    //! Move Pivot Tool - Entry point for pivot manipulation functionality
    public class MovePivotTool
    {
        [MenuItem("realvirtual/Move Pivot (Pro)", false, 400)]
        public static void ShowWindow()
        {
            #if UNITY_2021_2_OR_NEWER && REALVIRTUAL_PROFESSIONAL
            // Check if we should use overlay or window mode (default to overlay for clean installs)
            bool useOverlay = EditorPrefs.GetBool("realvirtual_UseMovePivotOverlay", true);
            
            if (useOverlay)
            {
                // Close any open window when using overlay mode
                var window = EditorWindow.GetWindow<MovePivotWindow>(false);
                if (window != null)
                    window.Close();
                
                // Show overlay mode - find or create the overlay
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    // Set the preference
                    EditorPrefs.SetBool("realvirtual_MovePivotOverlayVisible", true);
                    
                    // Force the overlay to display
                    #if UNITY_2022_1_OR_NEWER
                    // In Unity 2022.1+, we can access overlays directly
                    var overlayCanvas = sceneView.overlayCanvas;
                    if (overlayCanvas != null)
                    {
#if UNITY_6000_0_OR_NEWER
                        var movePivotOverlay = overlayCanvas.overlays.FirstOrDefault(o => o.GetType().Name == "MovePivotOverlay");
                        if (movePivotOverlay != null)
                        {
                            movePivotOverlay.displayed = true;
                            movePivotOverlay.collapsed = false; // Ensure it's expanded
                        }
#else
                        // Unity 2022 compatibility - overlays property doesn't exist, use reflection
                        var overlaysField = overlayCanvas.GetType().GetField("m_Overlays", BindingFlags.NonPublic | BindingFlags.Instance);
                        var overlaysList = overlaysField?.GetValue(overlayCanvas) as System.Collections.IEnumerable;
                        var movePivotOverlayObj = overlaysList?.Cast<object>().FirstOrDefault(o => o.GetType().Name == "MovePivotOverlay");
                        if (movePivotOverlayObj != null)
                        {
                            // Use reflection to access properties
                            var displayedProperty = movePivotOverlayObj.GetType().GetProperty("displayed");
                            var collapsedProperty = movePivotOverlayObj.GetType().GetProperty("collapsed");
                            displayedProperty?.SetValue(movePivotOverlayObj, true);
                            collapsedProperty?.SetValue(movePivotOverlayObj, false);
                        }
#endif
                    }
                    #else
                    // For older versions, try to find and display the overlay
                    #if REALVIRTUAL_PROFESSIONAL
                    var overlays = UnityEngine.Resources.FindObjectsOfTypeAll<MovePivotOverlay>();
                    if (overlays != null && overlays.Length > 0)
                    {
                        overlays[0].displayed = true;
                    }
                    #endif
                    #endif
                    
                    // Focus the scene view
                    sceneView.Focus();
                }
                else
                {
                    // Fallback to window mode if no scene view
                    MovePivotWindow.ShowWindow();
                }
            }
            else
            {
                // Show window mode
                MovePivotWindow.ShowWindow();
            }
            #else
            // For older Unity versions or non-professional, always use window mode
            MovePivotWindow.ShowWindow();
            #endif
        }
        
        // Toggle method is now internal, not exposed as menu item
        internal static void ToggleMovePivotMode()
        {
            #if UNITY_2021_2_OR_NEWER && REALVIRTUAL_PROFESSIONAL
            bool useOverlay = EditorPrefs.GetBool("realvirtual_UseMovePivotOverlay", true);
            
            if (useOverlay)
            {
                // Switch to window mode
                EditorPrefs.SetBool("realvirtual_UseMovePivotOverlay", false);
                
                // Hide overlay visibility
                EditorPrefs.SetBool("realvirtual_MovePivotOverlayVisible", false);
                
                // Actually hide the overlay
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    Overlay movePivotOverlay;
                    if (sceneView.TryGetOverlay("Move Pivot", out movePivotOverlay))
                    {
                        movePivotOverlay.displayed = false;
                    }
                }
                
                // Show the window
                MovePivotWindow.ShowWindow();
            }
            else
            {
                // Switch to overlay mode
                EditorPrefs.SetBool("realvirtual_UseMovePivotOverlay", true);
                EditorPrefs.SetBool("realvirtual_MovePivotOverlayVisible", true);
                
                // Close any open windows
                var window = EditorWindow.GetWindow<MovePivotWindow>(false);
                if (window != null)
                    window.Close();
                
                // Force the overlay to display
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    Overlay movePivotOverlay;
                    if (sceneView.TryGetOverlay("Move Pivot", out movePivotOverlay))
                    {
                        movePivotOverlay.displayed = true;
                        movePivotOverlay.collapsed = false; // Ensure it's expanded
                    }
                    
                    // Focus the scene view and request repaint
                    sceneView.Focus();
                    sceneView.Repaint();
                    
                    // Also try to force display after a frame delay
                    EditorApplication.delayCall += () =>
                    {
                        if (sceneView != null)
                        {
                            Overlay overlay;
                            if (sceneView.TryGetOverlay("Move Pivot", out overlay))
                            {
                                overlay.displayed = true;
                                overlay.collapsed = false;
                            }
                            sceneView.Repaint();
                        }
                    };
                }
            }
            #else
            // Overlay mode requires Unity 2021.2+ and realvirtual Professional
            #endif
        }
    }
}