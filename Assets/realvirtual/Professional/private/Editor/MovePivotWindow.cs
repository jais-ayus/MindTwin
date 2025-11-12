// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace realvirtual
{
    //! Move Pivot dockable window - provides pivot manipulation in a standard Unity window
    public class MovePivotWindow : EditorWindow
    {
        private MovePivotToolContent content;
        
        public static void ShowWindow()
        {
            var window = GetWindow<MovePivotWindow>();
            window.titleContent = new GUIContent("Move Pivot", EditorGUIUtility.IconContent("d_ToolHandlePivot").image);
            window.minSize = new Vector2(400, 250);
            window.maxSize = new Vector2(800, 1000);
            window.position = new Rect(window.position.x, window.position.y, 400, 250);
            window.Show();
        }
        
        void CreateGUI()
        {
            content = new MovePivotToolContent();
            content.CreateGUI(rootVisualElement);
        }
        
        private void OnDestroy()
        {
            content?.Cleanup();
        }
    }
}