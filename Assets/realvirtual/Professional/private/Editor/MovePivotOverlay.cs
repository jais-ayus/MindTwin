// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

#if UNITY_2021_2_OR_NEWER && REALVIRTUAL_PROFESSIONAL
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace realvirtual
{
    //! Move Pivot overlay for Scene view - provides quick access to pivot manipulation in overlay mode
    [Overlay(typeof(SceneView), "Move Pivot", true)]
    [Icon("d_ToolHandlePivot")]
    public class MovePivotOverlay : Overlay
    {
        private MovePivotToolContent content;
        
        public override VisualElement CreatePanelContent()
        {
            content = new MovePivotToolContent();
            var root = new VisualElement();
            root.style.width = 400;
            root.style.minWidth = 400;
            
            content.CreateGUI(root);
            return root;
        }
        
        public override void OnCreated()
        {
            base.OnCreated();
            
            // Set default display state based on mode preference
            bool useOverlay = EditorPrefs.GetBool("realvirtual_UseMovePivotOverlay", true);
            displayed = useOverlay && EditorPrefs.GetBool("realvirtual_MovePivotOverlayVisible", true);
        }
        
        public override void OnWillBeDestroyed()
        {
            // Save visibility state
            EditorPrefs.SetBool("realvirtual_MovePivotOverlayVisible", displayed);
            
            content?.Cleanup();
            base.OnWillBeDestroyed();
        }
    }
}
#endif