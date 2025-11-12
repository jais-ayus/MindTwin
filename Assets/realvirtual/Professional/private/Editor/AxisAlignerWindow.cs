// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace realvirtual
{
    public class AxisAlignerWindow : EditorWindow
    {
        private GameObject selectedObject;
        private AxisDirection axisToAlign = AxisDirection.Y;
        
        public enum AxisDirection
        {
            X,
            Y,
            Z,
            NegativeX,
            NegativeY,
            NegativeZ
        }
        
      
        
        [MenuItem("realvirtual/Axis Aligner (Pro)", false, 400)]
        public static void ShowWindow()
        {
            AxisAlignerWindow window = GetWindow<AxisAlignerWindow>();
            window.titleContent = new GUIContent("Axis Aligner");
            window.minSize = new Vector2(250, 300);
            window.maxSize = new Vector2(250, 300);
            window.Show();
        }
        
        void OnEnable()
        {
            if (Selection.activeGameObject != null)
            {
                selectedObject = Selection.activeGameObject;
            }
        }
        
        void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // Object selection
            EditorGUILayout.LabelField("Object to Transform", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            selectedObject = (GameObject)EditorGUILayout.ObjectField(selectedObject, typeof(GameObject), true);
            if (GUILayout.Button("Use Selected", GUILayout.Width(85)))
            {
                if (Selection.activeGameObject != null)
                    selectedObject = Selection.activeGameObject;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(15);
            
            // Align Axis Section
            EditorGUILayout.LabelField("Align Axis Upward", EditorStyles.boldLabel);
            axisToAlign = (AxisDirection)EditorGUILayout.EnumPopup(axisToAlign);
            
            EditorGUI.BeginDisabledGroup(selectedObject == null);
            if (GUILayout.Button("Align Axis", GUILayout.Height(25)))
            {
                AlignAxis();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(15);
            
            // Rotate 90 Degrees Section
            EditorGUILayout.LabelField("Rotate 90 Degrees", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(selectedObject == null);
            if (GUILayout.Button("X", GUILayout.Height(25)))
            {
                Rotate90Degrees(Vector3.right);
            }
            if (GUILayout.Button("Y", GUILayout.Height(25)))
            {
                Rotate90Degrees(Vector3.up);
            }
            if (GUILayout.Button("Z", GUILayout.Height(25)))
            {
                Rotate90Degrees(Vector3.forward);
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(15);
            
            // Undo button
            if (GUILayout.Button("Undo", GUILayout.Height(25)))
            {
                Undo.PerformUndo();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Children orientation always preserved", EditorStyles.centeredGreyMiniLabel);
            
            // Handle selection changes
            if (Selection.activeGameObject != selectedObject && Selection.activeGameObject != null)
            {
                selectedObject = Selection.activeGameObject;
                Repaint();
            }
        }
        
        Vector3 GetAxisVector(Transform transform, AxisDirection axis)
        {
            switch (axis)
            {
                case AxisDirection.X: return transform.right;
                case AxisDirection.Y: return transform.up;
                case AxisDirection.Z: return transform.forward;
                case AxisDirection.NegativeX: return -transform.right;
                case AxisDirection.NegativeY: return -transform.up;
                case AxisDirection.NegativeZ: return -transform.forward;
                default: return transform.up;
            }
        }
        
        void AlignAxis()
        {
            if (selectedObject == null)
                return;
            
            // Check if selected object has a mesh that should keep its orientation
            MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = selectedObject.GetComponent<MeshRenderer>();
            bool hasMesh = meshFilter != null && meshRenderer != null;
            
            if (hasMesh)
            {
                AlignAxisWithMesh();
                return;
            }
            
            Undo.RecordObject(selectedObject.transform, "Align Axis");
            
            // Store child positions and rotations before alignment (always preserve children)
            Dictionary<Transform, Vector3> childPositions = new Dictionary<Transform, Vector3>();
            Dictionary<Transform, Quaternion> childRotations = new Dictionary<Transform, Quaternion>();
            foreach (Transform child in selectedObject.transform)
            {
                childPositions[child] = child.position;
                childRotations[child] = child.rotation;
                Undo.RecordObject(child, "Preserve Child Transform");
            }
            
            // Calculate and apply rotation
            Vector3 currentAxis = GetAxisVector(selectedObject.transform, axisToAlign);
            Quaternion deltaRotation = Quaternion.FromToRotation(currentAxis, Vector3.up);
            selectedObject.transform.rotation = deltaRotation * selectedObject.transform.rotation;
            
            // Restore child positions and rotations
            foreach (var child in childPositions.Keys)
            {
                if (child != null)
                {
                    child.position = childPositions[child];
                    child.rotation = childRotations[child];
                }
            }
        }
        
        void AlignAxisWithMesh()
        {
            // For mesh objects, inform the user and stop
            EditorUtility.DisplayDialog("Cannot Align Mesh Object", 
                "Objects with meshes cannot be axis-aligned as this would change their visual appearance.\n\n" +
                "Please use this workflow instead:\n" +
                "1. Create an empty parent object\n" +
                "2. Use the Axis Aligner on the parent\n" +
                "3. Move the mesh object under the aligned parent\n\n" +
                "This preserves the mesh appearance while providing the aligned coordinate system.", 
                "OK");
        }
        
        void Rotate90Degrees(Vector3 axis)
        {
            if (selectedObject == null)
                return;
            
            // Check if selected object has a mesh that should keep its orientation
            MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = selectedObject.GetComponent<MeshRenderer>();
            bool hasMesh = meshFilter != null && meshRenderer != null;
            
            if (hasMesh)
            {
                Rotate90DegreesWithMesh(axis);
                return;
            }
            
            Undo.RecordObject(selectedObject.transform, "Rotate 90 Degrees");
            
            // Store child positions and rotations before rotation (always preserve children)
            Dictionary<Transform, Vector3> childPositions = new Dictionary<Transform, Vector3>();
            Dictionary<Transform, Quaternion> childRotations = new Dictionary<Transform, Quaternion>();
            foreach (Transform child in selectedObject.transform)
            {
                childPositions[child] = child.position;
                childRotations[child] = child.rotation;
                Undo.RecordObject(child, "Preserve Child Transform");
            }
            
            // Apply 90-degree rotation around specified local axis
            selectedObject.transform.Rotate(axis, 90f, Space.Self);
            
            // Restore child positions and rotations
            foreach (var child in childPositions.Keys)
            {
                if (child != null)
                {
                    child.position = childPositions[child];
                    child.rotation = childRotations[child];
                }
            }
        }
        
        void Rotate90DegreesWithMesh(Vector3 axis)
        {
            string axisName = axis == Vector3.right ? "X" : axis == Vector3.up ? "Y" : "Z";
            
            // For mesh objects, inform the user and stop
            EditorUtility.DisplayDialog("Cannot Rotate Mesh Object", 
                $"Objects with meshes cannot be rotated as this would change their visual appearance.\n\n" +
                "Please use this workflow instead:\n" +
                "1. Create an empty parent object\n" +
                "2. Use the Axis Aligner on the parent\n" +
                "3. Move the mesh object under the rotated parent\n\n" +
                "This preserves the mesh appearance while providing the rotated coordinate system.", 
                "OK");
        }
    }
}