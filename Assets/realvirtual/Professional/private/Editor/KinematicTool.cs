// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace realvirtual
{
#pragma warning disable 0414

    public class KinematicTool : EditorWindow
    {
        // window settings
        private Vector2 scrollPos;
        private Vector2 scrollPosComps = Vector2.zero;
        private static float handleScaleFactor = 0.05f;
        private static Color activeButtonColor;
        private static Color ButtonColor = new Color(0.345f, 0.345f, 0.345f, 1f);

        private static EditorGizmoOptions EditorGizmoOptions;

        //Axis settings
        private static KinematicTool tool;
        private static bool select = false;
        private static bool AxisGOcreated = false;
        private static bool driveAdded = false;
        private static Drive currDrive;
        private static GameObject AxisGO;
        private static Axis currentAxis;
        private static Kinematic currentKinematic;
        private static string Axisname;
        private static string Tmpname;
        private static Axis connectedAxis;
        private static GameObject refObj = null;
        private static GameObject GroupPrefixGO = null;
        private static bool IsSecondaryAxis = false;
        private static bool IsBalljoint = false;
        private static meshGizmo hoveredMeshGizmo;
        private static List<meshGizmo> hoveredMeshGizmos = new List<meshGizmo>();
        private static List<GameObject> currentMeshes;
        private static List<GameObject> CurrentComponents = new List<GameObject>();
        private static string currentSelection = "";
        private static string currentAxisPosType = "Pivot";
        private static String[] directionOptions = new[]
        {
            "LinearX", "LinearY", "LinearZ", "RotationX", "RotationY",
            "RotationZ", "Balljoint"
        };
        private static Vector3 axisEulerRotation = Vector3.zero;
        private static Quaternion eulerRotation = Quaternion.identity;
        private static Quaternion newRotation = Quaternion.identity;
        private static Quaternion AxisGoBaseRot = Quaternion.identity;
        private static int selectedRotation = 0;
        private static Vector3[] RotationOptions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(90, 0, 0),
            new Vector3(0, 90, 0),
            new Vector3(0, 0, 90),
            new Vector3(180, 0, 0),
            new Vector3(0, 180, 0),
            new Vector3(0, 0, 180)
        };
        private static string axisIcon;
        private static int selectedDirection = 0;
        private static DIRECTION currentDirection = DIRECTION.LinearX;
        private static float gizmoDistance = 0.5f;
        private static bool showPivotCoords = true;
        private static bool uselimits;
        private static float lowerlimit = 0f;
        private static float upperlimit = 0f;
        private static Vector3 Point1;
        private static Vector3 Point2;
        private static Vector3 Point3;
        private static List<Vector3> points = new List<Vector3>();
        private static Vector3 centerpoint = Vector3.zero;
        private static Vector3 normal = Vector3.zero;
        private static bool Point1Selected = false;
        private static bool Point2Selected = false;
        private static bool Point3Selected = false;
        private static string isolategroup = "";
        private static bool axishidden = false;
        private static bool axisisolate = false;
        private static bool axisshowAll = false;
        private static MeshFilter mesh;
        //drawing DriveHandle
        private static float sizecones = 0.5f;
        private static float sizecubes = 0.3f;
        private static float distancecenter = 0.2f;
        private static float transparency = 0.8f;
        private static float sizearc = 1.5f;
        private static float fontsize = 25;

        private static Vector3 posactive;
        private static Vector3 posinactive1;
        private static Vector3 posinactive2;
        private static Vector3 posrevert;
        private static Vector3 posmin;
        private static int idactive;
        private static int idnonactive1;
        private static int idnonactive2;
        private static int idrevert;
        private static int idposmin;
        private static float size;
        private static float distanceclick = 0.2f;
        private static DIRECTION dirnotused1;
        private static DIRECTION dirnotused2;
        private static Texture2D activeTex;
        private static Texture2D inactiveTex;
        private static bool selectNeighbours = false;
        private static bool selectConnectedMeshes = false;
        private static float contactTolerance = 0;
        private static bool overrideGroups = false;

        private static Color32 oldCol = ButtonColor;
        private static string newName; //new name for axis via Inputfield
        private static List<GameObject> hiddenbyIsolate = new List<GameObject>();
        private static List<GameObject> hiddenbyHide = new List<GameObject>();
        
        [MenuItem("realvirtual/Kinematic Tool (Pro) (Alt+K) &k", false, 400)]
         static void Init()
        {
            currentMeshes = new List<GameObject>();
            AxisGO = null;
            connectedAxis = null;
            driveAdded = false;
            axisEulerRotation = Vector3.zero;
            tool = (KinematicTool)EditorWindow.GetWindow(typeof(KinematicTool));
            if (Global.g4acontrollernotnull)
            {
                EditorGizmoOptions = Global.realvirtualcontroller.GetGizmoOptions();
                Global.realvirtualcontroller.ResetSelectedMeshes();
                activeButtonColor = EditorGizmoOptions.ActiveButtonBackground;
            }
            activeTex = Global.CreateTexture(activeButtonColor);
            inactiveTex = Global.CreateTexture(ButtonColor);
            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Axis>() != null)
            {
                UpdateWindowContent();
            }
            tool.titleContent = new GUIContent("Kinematic");
            tool.minSize = new Vector2(230, 250);
            tool.Show();
        }
      
        #region UnityCallbacks
        private void OnEnable()
        {
            if (Global.g4acontrollernotnull)
                Global.realvirtualcontroller.ResetSelectedMeshes();
            currentMeshes = new List<GameObject>();
            CurrentComponents.Clear();
            AxisGO = null;
            Axisname = "";
            connectedAxis = null;
            AxisGOcreated = false;
            points.Clear();
            UpdateWindowContent();
            if (!Application.isPlaying)
                SceneView.duringSceneGui += OnSceneGUI;
        }
        void OnDisable()
        {
            if (!Application.isPlaying)
                SceneView.duringSceneGui -= OnSceneGUI;
            if (axishidden)
            {
               HideAxis();  
            }
            if(axisisolate)
            {
                IsolateAxis();
            }
            
            if (currentAxis != null)
                currentAxis.activeInKinTool = false;
            AxisGO = null;
            currentAxis = null;
            Axisname = "";
            AxisGOcreated = false;
            currentKinematic = null;
            currentSelection = "";
            select = false;
            ResetHoveredMesh();
            if (Global.g4acontrollernotnull)
                Global.realvirtualcontroller.ResetSelectedMeshes();
        }

        private void OnDidOpenScene()
        {
            Init();
        }

        void OnHierarchyChange()
        {
            if (AxisGO == null)
            {
                AxisGOcreated = false;
                DeleteAxis();
            }
            else
            {
                if (EditorWindow.focusedWindow != null)
                {
                    if (EditorWindow.focusedWindow.titleContent.text != "KinematicTool")
                    {
                        if (AxisGO != null)
                        {
                            if (AxisGO.name != Axisname)
                                RenameAxis(AxisGO.name, AxisGO);
                        }
                        else
                        {
                            AxisGOcreated = false;
                            DeleteAxis();
                        }
                    }
                }
            }
        }
        private void OnDestroy()
        {
            Tools.current = Tool.Move;
            if (currentAxis != null)
                currentAxis.activeInKinTool = false;
        }

        private void OnFocus()
        {
            if (AxisGO == null)
            {
                OnEnable();
            }
        }
        #endregion
       
        private void OnGUI()
        {
#if UNITY_EDITOR
          
            if (Global.g4acontrollernotnull)
                EditorGizmoOptions = Global.realvirtualcontroller.GetGizmoOptions();
            
            else
            {
                // if the controller is not available write a global prompt that it needs to be set in realvirtualcontroller
                EditorUtility.DisplayDialog("Error", "The realvirtual controller needs to have the seeting EditorGizmoSettings to be set - please assign file EditorGizmoOptionsDefault", "OK");
                return;
            }
            activeButtonColor = EditorGizmoOptions.ActiveButtonBackground / 0.345f;
            if (currentAxis != null && currentAxis.secondaryAxis.Count > 0)
            {
                for (int i = currentAxis.secondaryAxis.Count - 1; i >= 0; i--)
                {
                    if (currentAxis.secondaryAxis[i] == null)
                        currentAxis.secondaryAxis.Remove(currentAxis.secondaryAxis[i]);
                }
            }
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);

            float width = position.width;
            EditorGUILayout.BeginVertical();
            GUILayout.Width(10);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Separator();
            
            EditorGUILayout.BeginHorizontal();
          
            EditorGUILayout.LabelField("Axis Name:", GUILayout.Width((width / 3) - 10));
            EditorGUI.BeginChangeCheck();
            Axisname = EditorGUILayout.TextField("", Axisname, GUILayout.Width((width / 3) * 2 - 10));
            if (EditorGUI.EndChangeCheck())
            {
                newName = Axisname;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            string buttonText;
            if (currentAxis != null)
                buttonText = "New Axis";
            
            else
                buttonText = "Create Axis";
            
            if (GUILayout.Button(new GUIContent(buttonText), GUILayout.Width((width / 3) - 10)))
            {
                if (buttonText == "New Axis" && newName == AxisGO.name)
                    newName = "";
                if (CheckIfNameIsUsed(newName, null))
                {
                    EditorUtility.DisplayDialog("Error", "The Axisname is already used!", "OK");
                }
                else
                {
                    if (AxisGO != null)
                    {
                        currentAxis.activeInKinTool = false;
                    }
                    CurrentComponents.Clear();
                    points.Clear();
                    if (newName != "")
                    {
                        CreateAxis();
                    }
                    else
                    {
                        AxisGOcreated = false;
                        AxisGO = null;
                        currentAxis = null;
                        currentKinematic = null;
                        currentSelection = "";
                        select = false;
                    }
                }
            }

            EditorGUI.BeginDisabledGroup(AxisGO == null);
           if (GUILayout.Button("Rename Axis", GUILayout.Width((width / 3) - 10)))
            {
                RenameAxis(newName, AxisGO);
                newName = "";
            }

            if (GUILayout.Button("Delete Axis", GUILayout.Width((width /3) - 10)))
            {
                string msg = "You want to delete " + Axisname + " with all group connections and sub objects?";
                bool sel = EditorUtility.DisplayDialog("Warning", msg, "Ok", "Cancel");
                if (sel)
                {
                    DeleteAxis();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
            GUILayout.EndVertical();

            EditorGUILayout.Separator();

            if (AxisGOcreated)
            {
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Axis:", GUILayout.Width((width / 2) - 10));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Secondary Axis:", GUILayout.Width((width / 4) - 10));
                IsSecondaryAxis = EditorGUILayout.Toggle("", IsSecondaryAxis, GUILayout.Width((width / 4) - 10));
                CheckStatusSecAxis();
                currentAxis.IsSecondaryAxis = IsSecondaryAxis;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                EditorGUILayout.BeginHorizontal();
                if (!driveAdded)
                    EditorGUILayout.LabelField("Connected Axis:", GUILayout.Width((width / 3) - 10));
                else
                {
                    EditorGUILayout.LabelField("Upper Axis:", GUILayout.Width((width / 3) - 10));
                }
                connectedAxis = (Axis)EditorGUILayout.ObjectField("", connectedAxis, typeof(Axis), true,
                    GUILayout.Width((width / 3) * 2 - 10)) as Axis;
                if (connectedAxis != currentAxis.ConnectedAxis)
                {
                    UpdateAxisConnections();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                EditorGUILayout.BeginHorizontal();
                Color oldColor = GUI.backgroundColor;
                if (currentSelection == "Set Axis")
                    GUI.backgroundColor = activeButtonColor;

                if (GUILayout.Button("Set Axis Reference", GUILayout.Width((width / 3) - 10)))
                {
                    currentSelection = "Set Axis";
                    select = true;
                    if (refObj != null)
                    {
                        refObj = null;
                        points.Remove(currentAxis.axisReferencePosition);
                        currentAxis.AxisReferenceGameObject = null;
                        points.Clear();
                    }
                }
                GUI.backgroundColor = oldColor;
                EditorGUILayout.ObjectField(refObj, typeof(GameObject), true, GUILayout.Width((width / 3) * 2 - 10));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(
                        new GUIContent("Align to reference",
                            "Update the Axis position after moving the axis reference object."),
                        GUILayout.Width((width / 3) - 10)))
                {
                    if (points.Contains(currentAxis.axisReferencePosition))
                        points.Remove(currentAxis.axisReferencePosition);
                    currentAxis.AlignToReference();
                    AxisGoBaseRot = AxisGO.transform.rotation;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Separator();
                if (refObj != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("AxisPosition:", GUILayout.Width((width / 2) - 10));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    EditorGUILayout.BeginHorizontal();
                    oldColor = GUI.backgroundColor;
                    if (currentAxisPosType == "Pivot")
                        GUI.backgroundColor = activeButtonColor;

                    if (GUILayout.Button("Pivot", GUILayout.Width((width / 3) - 10)))
                    {
                       SetPivotCenter();
                    }

                    GUI.backgroundColor = oldColor;
                    EditorGUI.BeginDisabledGroup(!refObj.GetComponent<MeshFilter>() ||
                                                 refObj.GetComponentsInChildren<MeshFilter>().Length == 0);
                    oldColor = GUI.backgroundColor;
                    if (currentAxisPosType == "Box")
                        GUI.backgroundColor = activeButtonColor;

                    if (GUILayout.Button("Bounding Box Center", GUILayout.Width((width / 3) - 10)))
                    {
                       SetBoundingBoxCenter();
                    }

                    GUI.backgroundColor = oldColor;
                    EditorGUI.EndDisabledGroup();
                    
                    oldColor = GUI.backgroundColor;
                    if (currentAxisPosType == "Center")
                        GUI.backgroundColor = activeButtonColor;

                    if (GUILayout.Button("Radius Center", GUILayout.Width((width / 3) - 10)))
                    {
                        SetRadiusCenter();
                    }
                    GUI.backgroundColor = oldColor;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    if (currentAxisPosType == "Center")
                    {
                        EditorGUILayout.BeginHorizontal();
                        oldColor = GUI.backgroundColor;
                        if (currentSelection == "Select first point")
                            GUI.backgroundColor = activeButtonColor;

                        if (GUILayout.Button("Point 1", GUILayout.Width((width / 3) - 10)))
                        {
                            currentSelection = "Select first point";
                            select = true;
                            if (points.Contains(Point1))
                                points.Remove(Point1);
                            SceneView.duringSceneGui += OnSceneGUI;
                        }

                        GUI.backgroundColor = oldColor;
                        EditorGUILayout.Vector3Field("", Point1);
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(Point1Selected == false);
                        oldColor = GUI.backgroundColor;
                        if (currentSelection == "Select second point")
                            GUI.backgroundColor = activeButtonColor;

                        if (GUILayout.Button("Point 2", GUILayout.Width((width / 3) - 10)))
                        {
                            currentSelection = "Select second point";
                            select = true;
                            Point2Selected = true;
                            if (points.Contains(Point2))
                                points.Remove(Point2);
                            SceneView.duringSceneGui += OnSceneGUI;
                        }

                        GUI.backgroundColor = oldColor;
                        EditorGUILayout.Vector3Field("", Point2);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(Point2Selected == false);
                        oldColor = GUI.backgroundColor;
                        if (currentSelection == "Select third point")
                            GUI.backgroundColor = activeButtonColor;

                        if (GUILayout.Button("Point 3", GUILayout.Width((width / 3) - 10)))
                        {
                            currentSelection = "Select third point";
                            select = true;
                            if (points.Contains(Point3))
                                points.Remove(Point3);
                            if (points.Contains(centerpoint))
                                points.Remove(centerpoint);

                            SceneView.duringSceneGui += OnSceneGUI;
                        }

                        GUI.backgroundColor = oldColor;
                        EditorGUILayout.Vector3Field("", Point3);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Separator();
                    
                    if (Event.current.type == EventType.KeyDown)
                    {
                        if (Event.current.modifiers == EventModifiers.Control && Event.current.keyCode == KeyCode.D)
                        {
                            if (selectedDirection == directionOptions.Length - 1)
                                selectedDirection = 0;
                            else
                                selectedDirection++;
                            OnDirectionSelected(selectedDirection);
                        }
                    }
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        new GUIContent("Axis Direction:",
                            "Use 'Ctrl + D' to select the axis direction or the drop-down menu. "),
                        GUILayout.Width((width / 3) - 10));
                    if (GUILayout.Button(directionOptions[selectedDirection].ToString(), EditorStyles.popup,
                            GUILayout.Width((width / 2) - 10)))
                    {
                        GenericMenu menu = new GenericMenu();
                        for (int i = 0; i < directionOptions.Length; i++)
                        {
                            int index = i;
                            menu.AddItem(new GUIContent(directionOptions[i].ToString()), i == selectedDirection,
                                () => OnDirectionSelected(index));
                        }

                        menu.ShowAsContext();
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();
                    if (Event.current.type == EventType.KeyDown)
                    {
                        if (Event.current.modifiers == EventModifiers.Control && Event.current.keyCode == KeyCode.R)
                        {
                            if (selectedRotation == RotationOptions.Length - 1)
                                selectedRotation = 0;
                            else
                                selectedRotation++;

                            axisEulerRotation = RotationOptions[selectedRotation];
                        }
                    }
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        new GUIContent("Axis Rotation:", "Use 'Ctrl + R' to select the rotation offset"),
                        GUILayout.Width((width / 3) - 10));
                    axisEulerRotation = EditorGUILayout.Vector3Field("", axisEulerRotation);
                    eulerRotation = Quaternion.Euler(axisEulerRotation);
                    newRotation = AxisGoBaseRot * eulerRotation;
                    if (AxisGO != null)
                    {
                        AxisGO.transform.rotation = newRotation;
                        currentAxis.RotationOffset = axisEulerRotation;
                    }

                    SceneView.RepaintAll();
                    Repaint();
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Gizmos distance:", GUILayout.Width((width / 3) - 10));
                    CalculateGizmoDistance(width);
                    currentAxis.GizmoDistance = gizmoDistance;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    EditorGUILayout.BeginHorizontal();
                    uselimits = EditorGUILayout.Toggle("Use Limits: ", uselimits);
                    currentAxis.UseLimits = uselimits;
                    if (driveAdded)
                    {
                        if (AxisGO.GetComponent<Drive>())
                            AxisGO.GetComponent<Drive>().UseLimits = uselimits;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();

                    EditorGUI.BeginDisabledGroup(!uselimits);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Lower Limit: ", GUILayout.Width(width / 4 - 20));
                    lowerlimit = EditorGUILayout.FloatField("", lowerlimit, GUILayout.Width(width / 4 - 10));
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Upper Limit: ", GUILayout.Width(width / 4 - 20));
                    upperlimit = EditorGUILayout.FloatField("", upperlimit, GUILayout.Width(width / 4 - 10));
                    currentAxis.UpperLimit = upperlimit;
                    if (driveAdded)
                        AxisGO.GetComponent<Drive>().UpperLimit = upperlimit;

                    currentAxis.LowerLimit = lowerlimit;
                    if (driveAdded)
                        AxisGO.GetComponent<Drive>().LowerLimit = lowerlimit;
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.Separator();

                    if (!IsSecondaryAxis)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Parts:", GUILayout.Width((width / 2) - 10));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        oldColor = GUI.backgroundColor;
                        if (currentSelection == "SelectComponent")
                            GUI.backgroundColor = activeButtonColor;
                        if (GUILayout.Button("Select Parts", GUILayout.Width((width / 3) - 10)))
                        {
                            if (currentSelection == "SelectComponent")
                            {
                                Tools.current = Tool.Move;
                                currentSelection = "";
                                select = false;
                                ResetHoveredMesh();
                                GameObject[] empty= new GameObject[0];
                                Selection.objects = empty;
                            }
                            else
                            {
                                select = true;
                                currentSelection = "SelectComponent";
                                if (CurrentComponents.Count > 0)
                                {
                                    Selection.objects = CurrentComponents.ToArray();
                                }
                            }
                        }

                        GUI.backgroundColor = oldColor;

                        if (currentSelection == "SelectGroups")
                            GUI.backgroundColor = activeButtonColor;

                        if (GUILayout.Button("Select Groups", GUILayout.Width((width / 3) - 10)))
                        {
                            if (currentSelection == "SelectGroups")
                            {
                                Tools.current = Tool.Move;
                                currentSelection = "";
                                select = false;
                                ResetHoveredMesh();
                            }
                            else
                            {
                                select = true;
                                currentSelection = "SelectGroups";
                            }
                        }

                        GUI.backgroundColor = oldColor;

                        EditorGUI.BeginDisabledGroup(Selection.activeGameObject == null);
                        if (GUILayout.Button("Add selected parts", GUILayout.Width((width / 3) - 10)))
                        {
                           AddSelectedParts(null);
                        }
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUI.BeginDisabledGroup(CurrentComponents.Count == 0);
#if REALVIRTUAL_BURST
                        GUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth (false));
                        float toleranceScale = 100000000000;
                        //EditorGUILayout.Space((40));
                        if (GUILayout.Button("Select Neighbours",GUILayout.Width((width / 3) - 10)))
                        {
                            Tools.current = Tool.Move;
                            currentSelection = "";
                            select = false;
                            ResetHoveredMesh();
                            
                            List<GameObject> gos = new List<GameObject>();
                            for (int i = 0; i < CurrentComponents.Count; i++)
                            {   
                                foreach(var obj in ContactDetection.FindContacts(CurrentComponents[i], CurrentComponents, contactTolerance/toleranceScale, overrideGroups, 0))
                                {
                                    if(!gos.Contains(obj))
                                        gos.Add(obj);
                                    Debug.Log(obj.name + " " + obj.GetComponent<Group>() + " fu");
                                }
                            }
                            if(gos.Count>0){
                                Selection.objects = gos.ToArray();
                            }
                        }
                        
                        if (GUILayout.Button("Fill", GUILayout.Width((width /3) - 10)))
                        {
                            Tools.current = Tool.Move;
                            currentSelection = "";
                            select = false;
                            ResetHoveredMesh();
                            List<GameObject> remaining = new List<GameObject>();
                            List<GameObject> total = new List<GameObject>();
                            List<GameObject> added = new List<GameObject>();
                            
                            for (int i = 0; i < CurrentComponents.Count; i++)
                            {
                                
                                total.Add(CurrentComponents[i]);
                            }
                            remaining.Add(Selection.activeGameObject);
                            total.Add(Selection.activeGameObject);
                            added.Add(Selection.activeGameObject);

                            int iteration = 0;

                            while(remaining.Count > 0){

                                iteration++;
                                List<GameObject> newObs = new List<GameObject>();
                      
                                for (int i = 0; i < remaining.Count; i++)
                                {
                                    List<GameObject> contacts = ContactDetection.FindContacts(remaining[i], total, contactTolerance/toleranceScale, overrideGroups, 0);
                                    
                                    for (int j = 0; j < contacts.Count; j++)
                                    {
                                        if(!total.Contains(contacts[j])){
                                            total.Add(contacts[j]);
                                            newObs.Add(contacts[j]);
                                            added.Add(contacts[j]);
                                        }
                                    }
                                }
                                remaining = newObs;
                            }
                            
                            GameObject[] gos = added.ToArray();
                            Selection.objects = gos;
                            if(gos.Length>0){
                                Selection.objects = gos;
                            }
                        }

                        if (GUILayout.Button("Select Connected",GUILayout.Width((width / 3) - 10)))
                        {
                            Tools.current = Tool.Move;
                            currentSelection = "";
                            select = false;
                            ResetHoveredMesh();
                            List<GameObject> remaining = new List<GameObject>();
                            List<GameObject> total = new List<GameObject>();

                            for (int i = 0; i < CurrentComponents.Count; i++)
                            {
                                remaining.Add(CurrentComponents[i]);
                                total.Add(CurrentComponents[i]);
                            }

                            int iteration = 0;

                            while(remaining.Count > 0){

                                Debug.Log("Iteration: " + iteration + " " + remaining.Count);
                                iteration++;
                                
                                List<GameObject> newObs = new List<GameObject>();

                                for (int i = 0; i < remaining.Count; i++)
                                {
                                    List<GameObject> contacts = ContactDetection.FindContacts(remaining[i], total,
                                        contactTolerance / toleranceScale, overrideGroups, 0);

                                    for (int j = 0; j < contacts.Count; j++)
                                    {
                                        if (!total.Contains(contacts[j]))
                                        {
                                            total.Add(contacts[j]);
                                            newObs.Add(contacts[j]);
                                        }
                                    }
                                }
                                remaining = newObs;
                            }
                            GameObject[] gos = total.ToArray();
                            Selection.objects = gos;
                            if(gos.Length>0){
                                Selection.objects = gos;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        overrideGroups = EditorGUILayout.Toggle("Override Groups", overrideGroups, GUILayout.Width(20));
                        GUILayout.EndVertical();

#else
                        // writer a message that burst compiler needs to be installed via package manager and compiler define #REALVIRTUAL_BURST needs to be set
                        EditorGUILayout.HelpBox("For using contact detection based selection Burst Compiler needs to be installed via package manager and compiler define REALVIRTUAL_BURST needs to be set", MessageType.Info);
#endif
                        EditorGUI.EndDisabledGroup();

                        EditorGUILayout.Separator();

                        EditorGUI.BeginDisabledGroup(CurrentComponents.Count == 0);
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Remove all Parts", GUILayout.Width((width / 2) - 10)))
                        {
                            RemoveAllParts();
                            gizmoDistance = 0.5f;
                            points.Clear();
                        }

                        if (GUILayout.Button("Remove selected Parts", GUILayout.Width((width / 2) - 10)))
                        {
                            RemoveSelectedParts(null);
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Current Parts: ", GUILayout.Width(width / 3 - 10));
                        if (CurrentComponents.Count == 0)
                            scrollPosComps = EditorGUILayout.BeginScrollView(scrollPosComps, GUILayout.Height(50),
                                GUILayout.ExpandWidth(true));
                        else
                        {
                            scrollPosComps = EditorGUILayout.BeginScrollView(scrollPosComps, GUILayout.Height(100),
                                GUILayout.ExpandWidth(true));
                        }

                        for (int i = 0; i < CurrentComponents.Count; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.ObjectField(CurrentComponents[i], typeof(GameObject), true);
                            if (currentKinematic != null)
                            {
                                if (CurrentComponents[i].GetComponent<Group>() != null)
                                    CurrentComponents[i].GetComponent<Group>().GroupNamePrefix =
                                        currentKinematic.GroupNamePrefix;
                                CurrentComponents[i].isStatic = false;
                            }

                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.EndScrollView();
                        GUILayout.Space((10));
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.EndDisabledGroup();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Drives:", GUILayout.Width((width / 2) - 10));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginDisabledGroup(driveAdded || IsBalljoint);
                        if (GUILayout.Button("Add Drive", GUILayout.Width((width / 2) - 10)))
                        {
                            if (currentAxis.secondaryAxis.Count > 0)
                            {
                                EditorUtility.DisplayDialog("Warning",
                                    "This axis has a secondary axis. Please correct the structure before adding a drive",
                                    "OK");
                            }
                            else
                            {
                                currDrive = AxisGO.AddComponent<Drive>();
                                driveAdded = true;
                                AxisGO.GetComponent<Drive>().Direction = currentAxis.Direction;
                            }

                            EditorUtility.SetDirty(AxisGO);
                        }

                        EditorGUI.EndDisabledGroup();
                        EditorGUI.BeginDisabledGroup(!driveAdded);
                        if (GUILayout.Button("Remove Drive", GUILayout.Width((width / 2) - 10)))
                        {
                            var drive = AxisGO.GetComponent<Drive>();
                            var behaviour = AxisGO.GetComponent<BehaviorInterface>();
                            if (behaviour != null)
                                DestroyImmediate(behaviour);

                            DestroyImmediate(drive);
                            driveAdded = false;
                            EditorUtility.SetDirty(AxisGO);
                        }

                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.EndHorizontal();
                        
                    }
                    else
                    {
                        EditorGUILayout.Separator();
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("A secondary axis cannot contain parts or drives. This parameter is set by default when the axis is created below another axis without a drive component.");
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Separator();
                    
                    EditorGUILayout.BeginHorizontal();
                    if (!IsSecondaryAxis)
                    {
                        EditorGUILayout.LabelField("View:", GUILayout.Width((width / 2) - 10));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginHorizontal();
                        oldColor = GUI.backgroundColor;
                        if (axisisolate)
                            GUI.backgroundColor = activeButtonColor;

                        if (GUILayout.Button("Isolate", GUILayout.Width((width / 3) - 10)))
                        {
                            IsolateAxis();
                            var window = GetWindow<KinematicTool>();
                            window.Repaint();
                        }

                        GUI.backgroundColor = oldColor;
                        oldColor = GUI.backgroundColor;

                        if (axishidden)
                            GUI.backgroundColor = activeButtonColor;

                        if (GUILayout.Button("Hide", GUILayout.Width((width / 3) - 10)))
                        {
                            HideAxis();
                            var window = GetWindow<KinematicTool>();
                            window.Repaint();
                        }

                        GUI.backgroundColor = oldColor;
                    }

                    if (GUILayout.Button("Ok & Close", GUILayout.Width((width / 3) - 10)))
                    {
                        tool = (KinematicTool)EditorWindow.GetWindow(typeof(KinematicTool));
                        if (tool.docked)
                        {
                            Tools.current = Tool.Move;
                            currentSelection = "";
                            select = false;
                            ResetHoveredMesh();
                            OnDisable();
                            Type inspectorWindowType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                            EditorWindow inspectorWindow = EditorWindow.GetWindow(inspectorWindowType);
                            if (inspectorWindow != null)
                            {
                                inspectorWindow.Focus();
                            }
                        }
                        else
                        {
                            tool.Close();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            Repaint();
#endif
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            Event current = Event.current;
            if (select)
            {
                float handleSize = 0.05f;
                Handles.color = Color.red;
                Vector2 mousePos = current.mousePosition;
                GameObject foundobj;
                bool found = HandleUtility.FindNearestVertex(mousePos, out Vector3 vertex, out foundobj);
                if (Global.g4acontrollernotnull)
                {
                    ResetHoveredMesh();
                    if (found && currentSelection != "SelectGroups")
                    {
                        showHoveredMesh(foundobj);
                    }
                }

                if (currentSelection == "Set Axis")
                {
                    if (current.type == EventType.MouseDown && current.button == 0)
                    {
                        if (found)
                        {
                            SetAxisReference(foundobj);
                            Tools.current = Tool.Move;
                            currentSelection = "";
                            select = false;
                        }
                        ResetHoveredMesh();
                        var window = GetWindow<KinematicTool>();
                        window.Repaint();
                    }
                }
                else if (currentSelection == "SelectComponent")
                {
                    if (found)
                    {
                        //Deselect
                        if (current.type == EventType.MouseDown && current.button == 0 && current.shift)
                        {
                            RemoveSelectedParts(foundobj);
                            return;
                        }
                        //Select 
                        if (current.type == EventType.MouseDown && current.button == 0)
                        {
                            AddSelectedParts(foundobj);
                            currentKinematic.GroupName = Axisname;
                            ResetHoveredMesh();
                        }
                    }
                }
                else if (currentSelection == "SelectGroups")
                {
                    ResetHoveredMeshes();
                    if (found)
                    {
                        //find objects in group
                        var highlightObjects = new List<MeshFilter>();

                        var groups = foundobj.GetComponents<Group>();
                        foreach (var gr in groups)
                        {
                            var allGroups =
                                FindObjectsByType<Group>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                            foreach (var g in allGroups)
                            {
                                if (gr.GroupName != AxisGO.name && g.GroupName == gr.GroupName)
                                {
                                    MeshFilter mf = g.GetComponent<MeshFilter>();
                                    if (mf != null && !highlightObjects.Contains(mf))
                                    {
                                        highlightObjects.Add(mf);
                                    }
                                }
                            }
                        }
                        foreach (var mf in highlightObjects)
                        {
                            CreateMeshGizmo(mf.gameObject);
                        }

                        void CheckCreateAxisGroup(GameObject go)
                        {
                            var groups = go.GetComponents<Group>();
                            bool groupAvail = false;
                            foreach (var gr in groups)
                            {
                                if (gr.GroupName == AxisGO.name)
                                    groupAvail = true;
                            }

                            if (!groupAvail)
                            {
                                var objGroup = go.AddComponent<Group>();
                                objGroup.GroupName = AxisGO.transform.name;
                                objGroup.GroupNamePrefix = currentKinematic.GroupNamePrefix;
                            }
                        }
                        //Select 
                        if (current.type == EventType.MouseDown && current.button == 0)
                        {
                            foreach (var mf in highlightObjects)
                            {
                                CheckCreateAxisGroup(mf.gameObject);
                            }

                            currentKinematic.GroupName = Axisname;
                            ResetHoveredMeshes();

                            foreach (var mf in highlightObjects)
                            {
                                if (axishidden)
                                {
                                    mf.gameObject.SetActive(false);
                                }

                                if (!CurrentComponents.Contains(mf.gameObject))
                                    CurrentComponents.Add(mf.gameObject);
                            }
                        }
                        //Deselect
                        if (current.type == EventType.MouseDown && current.button == 0 && current.shift)
                        {
                            //CurrentComponents.Remove(foundobj);
                            //DestroyImmediate(foundobj.GetComponent<Group>());
                        }
                    }
                }
                else
                {
                    if (EditorGizmoOptions != null)
                        Handles.color = EditorGizmoOptions.DefaultColorSelectionSphere;
                    else
                        Handles.color = Color.white;
                    if (found)
                    {
                        handleSize = Global.CalculateHandleSize(vertex, handleScaleFactor);
                        Handles.SphereHandleCap(0, vertex, Quaternion.identity, handleSize, EventType.Repaint);
                        Handles.Label(vertex, vertex.ToString());
                    }

                    if (current.type == EventType.MouseDown && current.button == 0 && found)
                    {
                        if (currentSelection == "Select first point")
                        {
                            Point1 = Global.RoundVector(vertex, 3);
                            points.Add(vertex);
                            Point1Selected = true;
                            currentSelection = "Select second point";
                            select = true;
                            current.Use();
                        }
                        else if (currentSelection == "Select second point")
                        {
                            Point2 = Global.RoundVector(vertex, 3);
                            Point2Selected = true;
                            points.Add(vertex);
                            currentSelection = "Select third point";
                            select = true;
                            current.Use();
                        }
                        else if (currentSelection == "Select third point")
                        {
                            Point3 = Global.RoundVector(vertex, 3);
                            Point3Selected = true;
                            centerpoint = Global.CalculateCenterpoint(Point1, Point2, Point3);
                            normal = Vector3.Cross(Point1 - centerpoint, Point2 - centerpoint).normalized;
                            points.Add(centerpoint);
                            currentSelection = "";
                            select = false;
                            currentAxis.axisReferencePosition = centerpoint;
                            AxisGO.transform.position = currentAxis.axisReferencePosition;
                            Quaternion rotation = Quaternion.LookRotation(normal);
                            AxisGO.transform.rotation = rotation;
                            AxisGoBaseRot = AxisGO.transform.rotation;
                            if (!points.Contains(currentAxis.axisReferencePosition))
                                points.Add(currentAxis.axisReferencePosition);
                        }

                        ResetHoveredMesh();
                    }
                }
            }

            Handles.color = Color.magenta;
            if (current.isKey && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                select = false;
                currentSelection = "";
                var window = GetWindow<KinematicTool>();
                window.Repaint();
            }

            if (current.type == EventType.MouseDown && current.button == 0 && current.shift &&
                Selection.activeGameObject != null)
            {
                if (CurrentComponents.Contains(Selection.activeGameObject))
                {
                    CurrentComponents.Remove(Selection.activeGameObject);
                    DestroyImmediate(Selection.activeGameObject.GetComponent<Group>());
                    select = false;
                    currentSelection = "";
                }
            }

            if (Event.current.type == EventType.KeyDown && refObj != null)
            {
                if (Event.current.modifiers == EventModifiers.Control && Event.current.keyCode == KeyCode.R)
                {
                    if (selectedRotation == RotationOptions.Length - 1)
                        selectedRotation = 0;
                    else
                        selectedRotation++;
                    axisEulerRotation = RotationOptions[selectedRotation];
                    var window = GetWindow<KinematicTool>();
                    window.Repaint();
                    SceneView.RepaintAll();
                }

                if (Event.current.modifiers == EventModifiers.Control && Event.current.keyCode == KeyCode.D)
                {
                    if (selectedDirection == directionOptions.Length - 1)
                        selectedDirection = 0;
                    else
                        selectedDirection++;

                    OnDirectionSelected(selectedDirection);
                    var window = GetWindow<KinematicTool>();
                    window.Repaint();
                    SceneView.RepaintAll();
                }
            }

            if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Axis>() != null &&
                Selection.activeGameObject != AxisGO)
            {
                UpdateWindowContent();
            }

            if (!Application.isPlaying)
            {
                drawPoints();
                drawPivotCoords();
                SceneView.RepaintAll();
            }
        }

        void Start()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        static void OnDirectionSelected(int index)
        {
            if (directionOptions[index] == "Balljoint")
            {
                IsBalljoint = true;
                selectedDirection = index;
                currentAxis.Balljoint = IsBalljoint;
                currentAxis.icon = "rotationaxis.png";
            }
            else
            {
                currentDirection = (DIRECTION)Enum.Parse(typeof(DIRECTION), directionOptions[index]);
                selectedDirection = index;
                IsBalljoint = false;
                currentAxis.Balljoint = IsBalljoint;
                currentAxis.Direction = currentDirection;
                if (driveAdded)
                    currDrive.Direction = currentAxis.Direction;

                if (currentDirection == DIRECTION.LinearX || currentDirection == DIRECTION.LinearY ||
                    currentDirection == DIRECTION.LinearZ)
                {
                    currentAxis.icon = "linearaxis.png";
                }
                else
                {
                    currentAxis.icon = "rotationaxis.png";
                }
            }

            EditorUtility.SetDirty(AxisGO);
        }

        #region AxisCreationandBaseParameter
        private static void CreateAxis()
        {
            AxisGO = new GameObject(newName);
            Axisname = AxisGO.name;
            AxisGO.transform.position = Vector3.zero;
            AxisGO.transform.rotation = Quaternion.identity;
            AxisGO.AddComponent<Axis>();
            currentAxis = AxisGO.GetComponent<Axis>();
            currentAxis.activeInKinTool = true;
            AxisGOcreated = true;
            connectedAxis = null;
            selectedRotation = 0;
            IsSecondaryAxis = false;
            IsBalljoint = false;
            refObj = null;
            if (Selection.activeGameObject != null)
            {
                AxisGO.transform.parent = Selection.activeGameObject.transform;
                var parent = Selection.activeGameObject;
                if (parent.GetComponent<Kinematic>() && parent.GetComponent<Axis>() &&
                    !parent.GetComponent<Drive>())
                {
                    currentAxis.IsSecondaryAxis = true;
                    IsSecondaryAxis = true;
                    currentAxis.parentAxis = parent.GetComponent<Axis>();
                    parent.GetComponent<Axis>().secondaryAxis.Add(AxisGO);
                }
            }

            Selection.activeGameObject = AxisGO;
            driveAdded = false;
            currentSelection = "Set Axis";
            select = true;
        }
        private static void DeleteAxis()
        {
            if (currentAxis != null)
                currentAxis.AxisDelete();
            if (refObj != null)
            {
                DestroyImmediate(refObj.GetComponent<Group>());
            }

            if (CurrentComponents.Count > 0 && CurrentComponents[0] != null)
            {
                foreach (var obj in CurrentComponents)
                {
                    var group = obj.GetComponent<Group>();
                    if (group != null)
                    {
                        DestroyImmediate(group);
                    }
                }
            }

            Global.realvirtualcontroller.ResetSelectedMeshes();
            DestroyImmediate(AxisGO);
            AxisGO = null;
            GroupPrefixGO = null;
            currentAxis = null;
            currentKinematic = null;
            connectedAxis = null;
            points.Clear();
            AxisGOcreated = false;
            driveAdded = false;
            currDrive = null;
            Point1Selected = false;
            Point2Selected = false;
            Point3Selected = false;
        }

        private static void RenameAxis(string name, GameObject selectedGO)
        {
            if (CheckIfNameIsUsed(name, selectedGO))
            {
                if (name == selectedGO.name) // call fron OnHierarchyChange
                    selectedGO.name = Axisname;
                else // call from OnGUI
                {
                    GUI.FocusControl(null);
                    Axisname = selectedGO.name;
                }
                EditorUtility.DisplayDialog("Error", "The Axisname is already used!", "OK");
            }
            else
            {
                AxisGO.name = name;
                Axisname = name;
                if (!IsSecondaryAxis)
                    if (currentKinematic != null)
                        currentKinematic.GroupName = name;
                
                if (CurrentComponents.Count > 0)
                {
                    foreach (var obj in CurrentComponents)
                    {
                        var group = obj.GetComponent<Group>();
                        if (group != null)
                        {
                            group.GroupName = AxisGO.name;
                            group.GroupNamePrefix = GroupPrefixGO;
                        }
                    }
                }
            }
        }

        private static void UpdateAxisConnections()
        {
            if (connectedAxis != null)
            {
                if (driveAdded)
                {
                    if (connectedAxis != currentAxis.ConnectedAxis && currentAxis.ConnectedAxis != null)
                    {
                        currentAxis.ConnectedAxis.GetComponent<Axis>().SignalSubDriveAxis(currentAxis, false);
                    }

                    connectedAxis.SignalSubDriveAxis(currentAxis, true);
                }

                currentAxis.ConnectedAxis = connectedAxis.gameObject;
            }
            else
            {
                if (driveAdded && currentAxis.ConnectedAxis != null)
                {
                    currentAxis.ConnectedAxis.GetComponent<Axis>().SignalSubDriveAxis(currentAxis, false);
                }

                currentAxis.ConnectedAxis = null;
            }
        }

        private static void SetAxisReference(GameObject foundobj)
        {
            currentAxis.AxisReferenceGameObject = foundobj;
            currentAxis.GizmoDistance = gizmoDistance;
            currentAxis.MeshColor = EditorGizmoOptions.KT_SelectedMeshColor;
            refObj = foundobj;
            if (!IsSecondaryAxis)
            {
                if (!AxisGO.GetComponent<Kinematic>())
                {
                    currentKinematic = AxisGO.AddComponent<Kinematic>();
                    currentKinematic.IntegrateGroupEnable = true;
                    currentKinematic.GroupName = AxisGO.transform.name;
                   
                }

                CurrentComponents = currentKinematic.GetAllMeshesWithGroup(Axisname);
                currentKinematic.GroupName = Axisname;
                if (AxisGO.GetComponent<Drive>() != null)
                {
                    driveAdded = true;
                    currDrive = AxisGO.GetComponent<Drive>();
                }
            }
            currentAxisPosType = "Pivot";
            currentAxis.PositionMode = Axis.AxisPositionTypes.Pivot;
            if (points.Contains(currentAxis.axisReferencePosition))
                points.Remove(currentAxis.axisReferencePosition);
            currentAxis.axisReferencePosition = currentAxis.AxisReferenceGameObject.transform.position;
            AxisGO.transform.position = currentAxis.axisReferencePosition;
            AxisGO.transform.rotation = currentAxis.AxisReferenceGameObject.transform.rotation;
            AxisGoBaseRot = currentAxis.AxisReferenceGameObject.transform.rotation;
            OnDirectionSelected(selectedDirection);
            selectedRotation = 0;
            axisEulerRotation = RotationOptions[selectedRotation];
            if (!points.Contains(currentAxis.axisReferencePosition))
                points.Add(currentAxis.axisReferencePosition);
        }

        private static void CheckStatusSecAxis()
        {
            if (currentAxis.IsSecondaryAxis != IsSecondaryAxis)
            {
                if (IsSecondaryAxis) //become secondary axis
                {
                    if (AxisGO.GetComponent<Kinematic>())
                    {
                        DestroyImmediate(AxisGO.GetComponent<Kinematic>());
                    }
                    CurrentComponents.Clear();
                    var parent = currentAxis.gameObject.transform.parent.gameObject.GetComponent<Axis>();
                    if (parent != null && parent != currentAxis)
                    {
                        currentAxis.parentAxis = parent;
                        parent.secondaryAxis.Add(AxisGO);
                    }
                    else
                    {
                        string msg2 = "To be a secondary axis the parent has to be a physical axis.";
                        bool sel = EditorUtility.DisplayDialog("Info", msg2, "Ok");
                        IsSecondaryAxis = false;
                    }
                }
                else
                {
                    currentAxis.parentAxis.removeSecondaryAxis(currentAxis.gameObject);
                    if (!AxisGO.GetComponent<Kinematic>())
                    {
                        currentKinematic = AxisGO.AddComponent<Kinematic>();
                        currentKinematic.IntegrateGroupEnable = true;
                        currentKinematic.GroupName = AxisGO.transform.name;
                    }
                    CurrentComponents = currentKinematic.GetAllMeshesWithGroup(Axisname);
                    currentKinematic.GroupName = Axisname;
                }
            }
        }

        private static void SetPivotCenter()
        {
            if (currentAxisPosType == "Center")
                ResetCenterPoints();
            currentAxisPosType = "Pivot";
            currentAxis.PositionMode = Axis.AxisPositionTypes.Pivot;
            if (points.Contains(currentAxis.axisReferencePosition))
                points.Remove(currentAxis.axisReferencePosition);
            currentAxis.axisReferencePosition = currentAxis.AxisReferenceGameObject.transform.position;
            AxisGO.transform.position = currentAxis.axisReferencePosition;
            AxisGO.transform.rotation = currentAxis.AxisReferenceGameObject.transform.rotation;
            if (!points.Contains(currentAxis.axisReferencePosition))
                points.Add(currentAxis.axisReferencePosition);
        }

        private static void SetBoundingBoxCenter()
        {
            if (currentAxisPosType == "Center")
                ResetCenterPoints();
            currentAxisPosType = "Box";
            currentAxis.PositionMode = Axis.AxisPositionTypes.BoundingBoxCenter;
            mesh = refObj.GetComponent<MeshFilter>();
            if (mesh == null)
            {
                var li = refObj.GetComponentsInChildren<MeshFilter>();
                mesh = li[0];
            }

            var boundscenterlocal = mesh.sharedMesh.bounds.center;
            if (points.Contains(currentAxis.axisReferencePosition))
                points.Remove(currentAxis.axisReferencePosition);
            currentAxis.axisReferencePosition = mesh.transform.TransformPoint(boundscenterlocal);
            AxisGO.transform.position = currentAxis.axisReferencePosition;
            if (!points.Contains(currentAxis.axisReferencePosition))
                points.Add(currentAxis.axisReferencePosition);
            SceneView.RepaintAll();
        }
        
        private static void SetRadiusCenter()
        {
            if (points.Contains(currentAxis.axisReferencePosition))
                points.Remove(currentAxis.axisReferencePosition);
            currentAxisPosType = "Center";
            currentAxis.PositionMode = Axis.AxisPositionTypes.RadiusCenter;
            if (Point1Selected && Point2Selected && Point3Selected)
            {
                currentAxis.axisReferencePosition = centerpoint;
                AxisGO.transform.position = currentAxis.axisReferencePosition;
            }
        }

        #endregion
        
        #region ObjSelection

        private static void UpdateGroupParameter(GameObject obj)
        {
        
            List<Kinematic> kinematicGroups = GetAllGroupsConnectedtoKinematic();
            //create a list with all name from kinematic groups
            List<string> kinematicGroupNames = kinematicGroups.Select(k => k.GetGroupName()).ToList();
                            
            if (obj.GetComponent<Group>() == null)
            {
                var objGroup = obj.AddComponent<Group>();
                objGroup.GroupName = AxisGO.transform.name;
                objGroup.GroupNamePrefix = currentKinematic.GroupNamePrefix;
            }
            else
            {
                var groups = obj.GetComponents<Group>();
                bool groupSet = false;
                foreach (var gr in groups)
                {
                    if (kinematicGroupNames.Contains(gr.GroupName))
                    {
                        gr.GroupName = AxisGO.transform.name;
                        gr.GroupNamePrefix = currentKinematic.GroupNamePrefix;
                        groupSet = true;
                    }
                    if(gr.GroupName == AxisGO.transform.name && gr.GroupNamePrefix == currentKinematic.GroupNamePrefix)
                        groupSet = true;
                }
                if (!groupSet)
                {
                    var objGroup = obj.AddComponent<Group>();
                    objGroup.GroupName = AxisGO.transform.name;
                    objGroup.GroupNamePrefix = currentKinematic.GroupNamePrefix;
                }
            }
            CheckParentGroupForAxis(obj, kinematicGroupNames);
        }
        private static void CheckParentGroupForAxis(GameObject obj, List<string> kinematicGroupNames)
        {
            var parent = obj.transform.parent;
            var parentgroups= parent.GetComponents<Group>();
            foreach (var gr in parentgroups)
            {
                if (!Selection.gameObjects.Contains(gr.gameObject) && gr.GroupName!=AxisGO.name && kinematicGroupNames.Contains(gr.GroupName))
                {
                    DestroyImmediate(gr);
                }

            }
        }
        private static void AddSelectedParts(GameObject foundobj)
        {
            List<GameObject> objectsToAdd = new List<GameObject>();
            if(foundobj==null)
            {
                // called by Add selected parts button
                objectsToAdd = Selection.gameObjects
                    .SelectMany(obj =>
                        obj.GetComponent<MeshFilter>() == null
                            ? obj.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                            : new[] { obj })
                    .Distinct()
                    .ToList();
            }
            else
            {
                // Add the selected mesh from "Select Parts" / called by OnSceneGUI
                objectsToAdd = new List<GameObject> {foundobj};
            }
            //filter out object where the object or children  or parent have a component realvirtualconrtoller or axis
            objectsToAdd = objectsToAdd.Where(obj =>
            {
                if (obj.GetComponent<realvirtualController>() != null || obj.GetComponent<Axis>() != null)
                    return false;
                var parent = obj.transform.parent;
                while (parent != null)
                {
                    if (parent.GetComponent<realvirtualController>() != null || parent.GetComponent<Axis>() != null)
                        return false;
                    parent = parent.parent;
                }
                return true;
            }).ToList();
            
            foreach (var obj in objectsToAdd)
            {
               AddObjecttoAxis(obj);
            }
        }

        private static void AddObjecttoAxis(GameObject obj)
        {
            UpdateGroupParameter(obj);
            if (axishidden)
            {
                obj.SetActive(false);
                hiddenbyHide.Add(obj);
            }
            if(axisisolate)
            {
                obj.SetActive(true);
                hiddenbyIsolate.Remove(obj);
            }
            if (!CurrentComponents.Contains(obj))
                CurrentComponents.Add(obj);
        }
        private static void RemoveSelectedParts(GameObject foundobj)
        {
            List<GameObject> objectsToAdd = new List<GameObject>();
            if(foundobj==null)
            {
                 objectsToAdd = Selection.gameObjects
                    .SelectMany(obj =>
                        obj.GetComponent<MeshFilter>() == null
                            ? obj.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                            : new[] { obj })
                    .Distinct()
                    .ToList();
            }
            else
            {
                 objectsToAdd = new List<GameObject> {foundobj};
            }
            foreach (var obj in objectsToAdd)
            {
               RemoveObjectfromAxis(obj);
            }
        }
        private static void RemoveObjectfromAxis(GameObject obj)
        {
            var groups = obj.GetComponents<Group>();
            foreach (var group in groups)
            {
                if (group.GroupName == AxisGO.name)
                {
                    CurrentComponents.Remove(obj);
                    DestroyImmediate(group);
                }
            }
            if (axishidden)
            {
                obj.SetActive(true);
                hiddenbyHide.Remove(obj);
            }

            if (axisisolate)
            {
                obj.SetActive(false);
                hiddenbyIsolate.Add(obj);
            }
        }
        private void  RemoveAllParts()
        {
            if (axishidden)
            {
                HideAxis();
            }
            var groupname = currentKinematic.GetGroupName();
            // get all game objects with group component where groupname  is same as currentKinematic groupname
            var grouplist = FindObjectsByType<Group>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(g => g.GroupName == groupname).Select(g => g.gameObject).ToList();
            foreach (var obj in grouplist)
            {
                CurrentComponents.Remove(obj);
                Group[] groups = obj.GetComponents<Group>();
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i].GroupName == AxisGO.name)
                    {
                        DestroyImmediate(groups[i]);
                    }
                }
                if (axisisolate)
                {
                    obj.SetActive(false);
                    hiddenbyIsolate.Add(obj);
                }
            }
        }
        private static void IsolateAxis()
        {
            if (!axisisolate)
            {
                if (axishidden)
                    HideAxis();
                axisisolate = true;
                isolategroup = currentKinematic.GetGroupName();
                // get all game objects  which have a meshfilter and are not part of the current axis 
                var objectsWithMeshFilter = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(mf => mf.gameObject);
                // filter out objects which have a group component with a different groupname as the current axis and don't have a group component with the same groupname as the current axis
                var filteredObjects = objectsWithMeshFilter.Where(go =>
                {
                    var groupComponent = go.GetComponents<Group>();
                    foreach (var group in groupComponent)
                    {
                        var groupname = group.GetGroupName();
                        if (groupname == isolategroup)
                            return false;
                    }
                    return true;
                });
                // set the gameobjects to inactive and add them to the hiddenbyisolate list 
                foreach (var go in filteredObjects)
                {
                    go.SetActive(false);
                    hiddenbyIsolate.Add(go);
                }
            }
            else
            {
                axisisolate = false;
                isolategroup = "";
                foreach (var go in hiddenbyIsolate)
                {
                    go.SetActive(true);
                }
                hiddenbyIsolate.Clear();
            }
        }
        private static void HideAxis()
        {
            var  hidedGrpoup = currentKinematic.GetGroupName();
            
                if (!axishidden)
                {
                    if(axisisolate)
                        IsolateAxis();
                    
                    var objectsWithMeshFilter = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(mf => mf.gameObject);
                    var filteredObjects = objectsWithMeshFilter.Where(go =>
                    {
                        var groupComponent = go.GetComponents<Group>();
                        foreach (var group in groupComponent)
                        {
                            var groupname = group.GetGroupName();
                            if (groupname == hidedGrpoup)
                                return true;
                        }
                        return false;
                    });
                    foreach (var go in filteredObjects)
                    {
                        go.SetActive(false);
                        hiddenbyHide.Add(go);
                    }
                    axishidden = true;
                }
                else
                {
                    foreach (var go in hiddenbyHide)
                    {
                        go.SetActive(true);
                    }
                    hiddenbyHide.Clear();
                    axishidden = false;
                }
        }

        private static List<Kinematic> GetAllGroupsConnectedtoKinematic()
        {
            var kinematicComponents = FindObjectsByType<Kinematic>(FindObjectsSortMode.None).Where(k => k.IntegrateGroupEnable).ToList();         
            return kinematicComponents;
        }
        #endregion
        private static void UpdateWindowContent()
        {
            if (Selection.activeGameObject == null || !Selection.activeGameObject.GetComponent<Axis>())
                return;
            if (AxisGO != null)
            {
                if (axishidden)
                    HideAxis();
                
                if(axisisolate)
                    IsolateAxis();
                
                currentAxis.activeInKinTool = false;
                connectedAxis = null;
                driveAdded = false;
                points.Clear();
            }

            AxisGO = Selection.activeGameObject;
            Axisname = AxisGO.name;
            currentAxis = Selection.activeGameObject.GetComponent<Axis>();
            refObj = currentAxis.AxisReferenceGameObject;
            if (refObj != null)
                AxisGoBaseRot = refObj.transform.rotation;

            for (int j = 0; j < RotationOptions.Length; j++)
            {
                if (RotationOptions[j] == currentAxis.RotationOffset)
                    selectedRotation = j;
            }

            axisEulerRotation = RotationOptions[selectedRotation];
            currentKinematic = Selection.activeGameObject.GetComponent<Kinematic>();

            if (currentKinematic != null)
            {
                GroupPrefixGO = currentKinematic.GroupNamePrefix;
            }
            IsSecondaryAxis = currentAxis.IsSecondaryAxis;
            IsBalljoint = currentAxis.Balljoint;
            currentAxis.activeInKinTool = true;
            AxisGOcreated = true;
            switch (currentAxis.PositionMode)
            {
                case Axis.AxisPositionTypes.Pivot:
                    currentAxisPosType = "Pivot";
                    break;
                case Axis.AxisPositionTypes.BoundingBoxCenter:
                    currentAxisPosType = "Box";
                    break;
                case Axis.AxisPositionTypes.RadiusCenter:
                    currentAxisPosType = "Center";
                    break;
            }

            if (AxisGO.GetComponent<Drive>() != null)
            {
                currDrive = AxisGO.GetComponent<Drive>();
                driveAdded = true;
            }

            if (currentAxis.ConnectedAxis != null)
                connectedAxis = currentAxis.ConnectedAxis.GetComponent<Axis>();

            if (currentAxis.Balljoint)
            {
                selectedDirection = directionOptions.Length - 1;
            }
            else
            {
                if (driveAdded)
                    currentAxis.Direction = currDrive.Direction;

                currentDirection = currentAxis.Direction;
                for (var i = 0; i < directionOptions.Length; i++)
                {
                    if (directionOptions[i] == currentDirection.ToString())
                        selectedDirection = i;
                }
            }

            if (!points.Contains(currentAxis.axisReferencePosition))
                points.Add(currentAxis.axisReferencePosition);
            gizmoDistance = currentAxis.GizmoDistance;


            if (currentKinematic != null)
            {
                string name = Axisname;
                if (currentKinematic.GroupNamePrefix != null)
                {
                    name = currentKinematic.GroupNamePrefix.name + Axisname;
                    if (CurrentComponents.Count > 0)
                    {
                        foreach (var obj in CurrentComponents)
                        {
                            if (obj.GetComponent<Group>() != null)
                                obj.GetComponent<Group>().GroupNamePrefix = currentKinematic.GroupNamePrefix;
                        }
                    }
                }
                CurrentComponents = currentKinematic.GetAllMeshesWithGroup(Axisname);
                EditorUtility.SetDirty(AxisGO);
            }

            if (CheckIfNameIsUsed(Axisname, AxisGO))
            {
                if (refObj != null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "The Axisname is already used! \nName is set to kinematic group name.", "OK");
                    AxisGO.name = currentKinematic.GroupName;
                    Axisname = currentKinematic.GroupName;
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "The Axisname is already used!", "OK");
                }
            }

            currentSelection = "";
            select = false;
        }

        #region MeshVisuals
        //Mesh visuals
        private static void ResetHoveredMesh()
        {
            if (hoveredMeshGizmo != null)
            {
                currentMeshes.Remove(hoveredMeshGizmo.mainGO);
                if (Global.g4acontrollernotnull)
                    Global.realvirtualcontroller.RemoveMeshGizmo(hoveredMeshGizmo);
                hoveredMeshGizmo = null;
            }
        }
        private static void ResetHoveredMeshes()
        {
            foreach (var hoveredMeshGizmo in hoveredMeshGizmos)
            {
                currentMeshes.Remove(hoveredMeshGizmo.mainGO);
                if (Global.g4acontrollernotnull)
                    Global.realvirtualcontroller.RemoveMeshGizmo(hoveredMeshGizmo);
            }
            hoveredMeshGizmos = new List<meshGizmo>();
        }
        private static void showHoveredMesh(GameObject foundobj)
        {
            if (EditorGizmoOptions == null)
                return;

            Tools.current = Tool.View;
            if (hoveredMeshGizmo != null)
            {
                if (foundobj != hoveredMeshGizmo.mainGO)
                {
                    currentMeshes.Remove(hoveredMeshGizmo.mainGO);
                    Global.realvirtualcontroller.RemoveMeshGizmo(hoveredMeshGizmo);
                    hoveredMeshGizmo = null;
                }
                else
                {
                    hoveredMeshGizmo.MeshColor = EditorGizmoOptions.KT_HoverMeshColor;
                }
            }

            if (!currentMeshes.Contains(foundobj))
            {
                hoveredMeshGizmo = Global.realvirtualcontroller.signalGizmoMesh(
                    foundobj,
                    Global.CalculateHandleSize(foundobj.transform.position, handleScaleFactor) * 10,
                    Global.realvirtualcontroller.EditorGizmoSettings.KT_HoverMeshColor, true, true);
                if (hoveredMeshGizmo.meshFilterList != null)
                    currentMeshes.Add(foundobj);
            }
        }


        private static void CreateMeshGizmo(GameObject obj)
        {
            hoveredMeshGizmo = Global.realvirtualcontroller.signalGizmoMesh(
                obj,
                Global.CalculateHandleSize(obj.transform.position, handleScaleFactor) * 10,
                Global.realvirtualcontroller.EditorGizmoSettings.KT_HoverMeshColor,
                false,
                false
            );

            if (hoveredMeshGizmo.meshFilterList != null)
                currentMeshes.Add(obj);

            hoveredMeshGizmos.Add(hoveredMeshGizmo);
        }
        #endregion

        #region DrawMethods

        private static void CalculateGizmoDistance(float width)
        {
            if (mesh == null)
            {
                gizmoDistance = EditorGUILayout.Slider("", gizmoDistance, -1.0f, 1.0f,
                    GUILayout.Width((width / 3) * 2 - 10));
            }
            else
            {
                switch (currentDirection)
                {
                    case DIRECTION.LinearX:
                    case DIRECTION.RotationX:
                    {
                        gizmoDistance = EditorGUILayout.Slider("", gizmoDistance,
                            mesh.sharedMesh.bounds.min.x - 1, mesh.sharedMesh.bounds.max.x + 1,
                            GUILayout.Width((width / 3) * 2 - 10));
                        break;
                    }
                    case DIRECTION.LinearY:
                    case DIRECTION.RotationY:
                    {
                        gizmoDistance = EditorGUILayout.Slider("", gizmoDistance,
                            mesh.sharedMesh.bounds.min.y - 1, mesh.sharedMesh.bounds.max.y + 1,
                            GUILayout.Width((width / 3) * 2 - 10));
                        break;
                    }
                    case DIRECTION.LinearZ:
                    case DIRECTION.RotationZ:
                    {
                        gizmoDistance = EditorGUILayout.Slider("", gizmoDistance,
                            mesh.sharedMesh.bounds.min.z - 1, mesh.sharedMesh.bounds.max.z + 1,
                            GUILayout.Width((width / 3) * 2 - 10));
                        break;
                    }
                }
            }
        }
        private static void drawPoints()
        {
            if (points.Count == 0 || Application.isPlaying)
                return;

            Event current = Event.current;
            float handleSize = 0.05f;
            for (int i = 0; i < points.Count; i++)
            {
                var point = Global.RoundVector(points[i], 3);
                string label = "";
                if (point == Point1)
                {
                    Handles.color = Color.yellow;
                    label = "Point1";
                }
                else if (point == Point2)
                {
                    Handles.color = Color.gray;
                    label = "Point2";
                }
                else if (point == centerpoint)
                {
                    Handles.color = Color.red;
                    label = "CenterPoint";
                }
                else
                {
                    EditorGizmoOptions = Global.realvirtualcontroller.GetGizmoOptions();
                    Handles.color = EditorGizmoOptions.AxisColor;
                }

                handleSize = Global.CalculateHandleSize(points[i], handleScaleFactor);
                Handles.SphereHandleCap(0, points[i], Quaternion.identity, handleSize, EventType.Repaint);
                Handles.Label(points[i], label);
                if (current.type == EventType.Layout)
                {
                    HandleUtility.Repaint();
                }
            }

            if (Point1Selected && Point2Selected)
            {
                Handles.color = Color.green;
                Handles.DrawLine(Point1, Point1);
            }

            if (Point1Selected && Point2Selected && Point3Selected)
            {
                Handles.color = Color.blue;
                Handles.DrawLine(Point1, centerpoint);
                Handles.DrawLine(Point3, centerpoint);
                Handles.DrawLine(Point2, centerpoint);
                Handles.DrawWireDisc(centerpoint, normal, Vector3.Distance(Point1, centerpoint));
            }

            if (current.type == EventType.Layout)
            {
                HandleUtility.Repaint();
            }
        }

        private static void drawPivotCoords()
        {
            if (AxisGO == null || currentAxis.AxisReferenceGameObject == null)
                return;

            var basePoint = currentAxis.axisReferencePosition;
            Quaternion rotation = AxisGO.transform.rotation;
            float handleSize = Global.CalculateHandleSize(basePoint, handleScaleFactor) * 6;
            Handles.color = Color.blue;
            Handles.DrawLine(basePoint, basePoint + rotation * Vector3.forward * handleSize);
            Handles.ConeHandleCap(0, basePoint + rotation * Vector3.forward * handleSize, rotation, handleSize * 0.2f,
                EventType.Repaint);
            Handles.color = Color.green;
            Handles.DrawLine(basePoint, basePoint + rotation * Vector3.up * handleSize);
            Handles.ConeHandleCap(0, basePoint + rotation * Vector3.up * handleSize,
                rotation * Quaternion.Euler(-90, 0, 0), handleSize * 0.2f, EventType.Repaint);
            Handles.color = Color.red;
            Handles.DrawLine(basePoint, basePoint + rotation * Vector3.right * handleSize);
            Handles.ConeHandleCap(0, basePoint + rotation * Vector3.right * handleSize,
                rotation * Quaternion.Euler(0, 90, 0), handleSize * 0.2f, EventType.Repaint);
        }

        private static void ResetCenterPoints()
        {
            if (points.Contains(Point1))
                points.Remove(Point1);
            if (points.Contains(Point2))
                points.Remove(Point2);
            if (points.Contains(Point3))
                points.Remove(Point3);
            if (points.Contains(centerpoint))
                points.Remove(centerpoint);
            Point1Selected = false;
            Point2Selected = false;
            Point3Selected = false;
        }
        #endregion
        private static bool CheckIfNameIsUsed(string name, GameObject selectedObj)
        {
            bool result = false;
            GameObject[] allGameObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Kinematic kin = null;
            if (selectedObj != null)
            {
                kin = selectedObj.GetComponent<Kinematic>();
            }
            foreach (GameObject go in allGameObjects)
            {
                var kinGO = go.GetComponent<Kinematic>();
                if (go.name == name && (selectedObj == null || selectedObj != go) && go.GetComponent<Axis>())
                {
                    if (kinGO != null && kin != null)
                    {
                        if (kin.GroupNamePrefix == kinGO.GroupNamePrefix)
                        {
                            result = true;
                            break;
                        }
                    }
                    else
                    {
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }
    }
}