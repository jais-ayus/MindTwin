using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


namespace realvirtual
{
#pragma warning disable 0414
    [InitializeOnLoad]
    //! Class to handle the creation of the realvirtual menu
    public class MeasureTool : EditorWindow 
    {
        private static Vector3 startpoint; 
        private static Vector3 endpoint;
        private static Vector3 thirdpoint;
        private static List<Vector3> points = new List<Vector3>();
        private static Vector3 centerpoint=Vector3.zero;
        private static float radius=0;
        private static float diameter=0;
        private static Vector3 normal=Vector3.zero;
        private float currentDistance=0f;
        private Vector2 scrollPos;
        private static string currentSelection;
        private string drawGizmo = "";
        private static bool startpointSelected = false;
        private static bool endpointSelected = false;
        private static bool thirdpointSelected = false;
        private static Color activeButtonColor = new Color(0.87f, 0.3f, 0.49f, 1f) / 0.345f;
        private static Color ButtonColor = new Color(0.35f,0.35f,0.35f,1f);
        private static int scaleUnit = 1;
        private static bool useScaleUnit = false;
        
        public static bool select = false;
        
        private static float handleScaleFactor = 0.05f;
        
        [MenuItem("realvirtual/Measurement (Pro)", false, 400)]
        static void Init()
        {
            currentSelection = "Select first point";
            select = true;
            if (points.Contains(startpoint))
                points.Remove(startpoint);
            SceneView.duringSceneGui += OnSceneGUI;
            MeasureTool tool =
             (MeasureTool) EditorWindow.GetWindow(typeof(MeasureTool));
            tool.titleContent = new GUIContent("Measure");
            tool.minSize = new Vector2(230, 250);
            tool.Show();
        }
        void OnGUI()
        {
#if UNITY_EDITOR
           

           
                
                 
            
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
            float width = position.width;
            GUILayout.BeginVertical();
            GUILayout.Width(10);
            GUILayout.EndVertical();

            var selected = Selection.objects.Count();
            EditorGUILayout.Separator();

            GUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                Color oldColor = GUI.backgroundColor;
                if(currentSelection=="Select first point")
                    GUI.backgroundColor = activeButtonColor;
                
                if (GUILayout.Button("Point 1", GUILayout.Width((width / 3) - 10)))
                {
                    // selection by click
                    currentSelection = "Select first point";
                    select = true;
                    if (points.Contains(startpoint))
                        points.Remove(startpoint);
                    SceneView.duringSceneGui += OnSceneGUI;
                }
                GUI.backgroundColor = oldColor;
                // field to show a vector3
                EditorGUILayout.Vector3Field("", startpoint);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();

                EditorGUILayout.BeginHorizontal();
                oldColor = GUI.backgroundColor;
                if(currentSelection== "Select second point")
                    GUI.backgroundColor = activeButtonColor;
                if (GUILayout.Button("Point 2", GUILayout.Width((width / 3) - 10)))
                {
                    currentSelection = "Select second point";
                    select = true;
                    endpointSelected = true;
                    if (points.Contains(endpoint))
                        points.Remove(endpoint);
                    SceneView.duringSceneGui += OnSceneGUI;
                }
                GUI.backgroundColor = oldColor;
                EditorGUILayout.Vector3Field("", endpoint);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                useScaleUnit = EditorGUILayout.Toggle("Unit mm", useScaleUnit, GUILayout.Width((width / 3) - 10));
                if (useScaleUnit)
                    scaleUnit = 1000;
                else
                {
                    scaleUnit = 1;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                currentDistance = (Vector3.Distance(startpoint, endpoint))*scaleUnit;
                string formDist = currentDistance.ToString("F3");
                EditorGUILayout.TextField("Distance:", formDist);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                string formX = ((Math.Abs(endpoint.x - startpoint.x))*scaleUnit).ToString("F3");
                EditorGUILayout.TextField("Distance X:",formX );
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                string formY = ((Math.Abs(endpoint.y - startpoint.y))*scaleUnit).ToString("F3");
                EditorGUILayout.TextField("Distance Y:", formY);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                string formZ = ((Math.Abs(endpoint.z - startpoint.z))*scaleUnit).ToString("F3");
                EditorGUILayout.TextField("Distance Z:", formZ);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Measure centerpoint");
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                oldColor = GUI.backgroundColor;
                if(currentSelection== "Select third point")
                    GUI.backgroundColor = activeButtonColor;
                if (GUILayout.Button("Point 3", GUILayout.Width((width / 3) - 10)))
                {
                    currentSelection = "Select third point";
                    select = true;
                    if (points.Contains(thirdpoint))
                        points.Remove(thirdpoint);
                    if (points.Contains(centerpoint))
                        points.Remove(centerpoint);
                    
                    SceneView.duringSceneGui += OnSceneGUI;
                }
                GUI.backgroundColor = oldColor;
                EditorGUILayout.Vector3Field("", thirdpoint);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Centerpoint:",GUILayout.Width((width / 3) - 10));
                if (startpointSelected && endpointSelected && thirdpointSelected)
                {
                    centerpoint =  Global.CalculateCenterpoint(startpoint, endpoint, thirdpoint);
                    normal = Vector3.Cross(startpoint - centerpoint, endpoint - centerpoint);
                    points.Add(centerpoint);
                    radius= Vector3.Distance(centerpoint, startpoint);
                    diameter = radius * 2;
                }
                EditorGUILayout.Vector3Field("", centerpoint);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Radius:",GUILayout.Width((width / 4) - 10));
                EditorGUILayout.TextField(radius.ToString("F3"),GUILayout.Width((width / 4) - 10));
                EditorGUILayout.LabelField("Diameter:",GUILayout.Width((width / 4) - 10));
                EditorGUILayout.TextField(diameter.ToString("F3"),GUILayout.Width((width / 4) - 10));

                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset", GUILayout.Width((width / 3) - 10)))
                {
                    startpoint = Vector3.zero;
                    endpoint = Vector3.zero;
                    thirdpoint = Vector3.zero;
                    centerpoint = Vector3.zero;
                    startpointSelected = false;
                    endpointSelected = false;
                    thirdpointSelected = false;
                    currentDistance = 0f;
                    points.Clear();
                    currentSelection = "Select first point";
                    select = true;
                    SceneView.duringSceneGui += OnSceneGUI;
                    var window = GetWindow<MeasureTool>();
                    window.Repaint();
                }
            
                
                EditorGUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
#endif
        }

        
        static void OnSceneGUI(SceneView sceneView)
        {

            Event current = Event.current;
            float handleSize = 0.05f;
            if (select)
            {
                Handles.color = Color.red;
                Vector2 mousePos = current.mousePosition;
                bool found = HandleUtility.FindNearestVertex(mousePos, out Vector3 vertex);
                if (found)
                {
                    handleSize =  Global.CalculateHandleSize(vertex,handleScaleFactor);
                    Handles.SphereHandleCap(0, vertex, Quaternion.identity, handleSize, EventType.Repaint);
                    Handles.Label(vertex, vertex.ToString());
                }
                if (current.type == EventType.MouseDown && current.button == 0 && found)
                {
                    if (currentSelection == "Select first point")
                    {
                        startpoint =  Global.RoundVector(vertex,3);
                        points.Add(vertex);
                        startpointSelected = true;
                        currentSelection = "Select second point";
                        select = true;
                        current.Use();
                    }
                    else if (currentSelection == "Select second point")
                    {
                        endpoint =  Global.RoundVector(vertex,3);;
                        points.Add(vertex);
                        endpointSelected = true;
                        currentSelection = "";
                        select = false;
                    }
                    else if(currentSelection == "Select third point")
                    {
                        thirdpoint = Global.RoundVector(vertex,3);;
                        points.Add(vertex);
                        thirdpointSelected = true;
                        currentSelection = "";
                        select = false;
                    }
                }
            }
            for (int i = 0; i < points.Count; i++)
            {
               
                Handles.color = Color.green;
                handleSize=Global.CalculateHandleSize(points[i],handleScaleFactor);
                Handles.SphereHandleCap(0, points[i], Quaternion.identity, handleSize, EventType.Repaint);
                Handles.Label(points[i], points[i].ToString());
                if (current.type == EventType.Layout)
                {
                    HandleUtility.Repaint();
                }
            }
            if(startpointSelected && endpointSelected)
            {
                Handles.color = Color.green;
                Handles.DrawLine(startpoint, endpoint);
            }
            if (startpointSelected && endpointSelected && thirdpointSelected)
            {
                Handles.color = Color.blue;
                Handles.DrawLine(startpoint, centerpoint);
                Handles.DrawLine(thirdpoint, centerpoint);
                Handles.DrawLine(endpoint, centerpoint);
                Handles.DrawWireDisc(centerpoint, normal, radius);
            }
            if (current.type == EventType.Layout)
            {
                HandleUtility.Repaint();
            }
        }

        private void OnEnable()
        {
            startpoint = Vector3.zero;
            endpoint= Vector3.zero;
            currentDistance = 0f;
            points.Clear();
        }

        void OnDisable()
        {
           SceneView.duringSceneGui -= OnSceneGUI;
           currentSelection = "";
           startpoint = Vector3.zero;
           endpoint= Vector3.zero;
           currentDistance = 0f;
           points.Clear();
           startpointSelected = false;
           endpointSelected = false;
           thirdpointSelected = false;
           select = false;
        }

        
    }
}
