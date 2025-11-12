using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using System.Text.RegularExpressions;

namespace realvirtual
{
#pragma warning disable 0414
    [InitializeOnLoad]
    //! Class to handle the creation of the realvirtual menu
    public class MaterialWindow : EditorWindow 
    {
        private static MaterialPalet materialPalet; 
        private static List<Material> currentmaterials=new List<Material>();
        private Vector2 scrollPos;
        private static Material selectedMaterial;
        private static int selectedMaterialIndexOld; 
        private int selectedGroupIndex = 0;
        private static int selectedGroupIndexOld;
        static List<Object> selectedobjs = new List<Object>();
        private static List<string> Groups;
        private string activegroup;
        private static object[] activegroupsel = new object[0];
        private bool layoutwithGroup;
        private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>();
        
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            InitSettings();
        }
        private static void InitSettings()
        {
            GetMaterialPalet();
            UpdateMaterialPalette();
        }
        
        static void GetMaterialPalet()
        {
            string[] palet;
            var paletname=EditorPrefs.GetString("materialPalet");
            if (paletname != "")
            {
                palet = AssetDatabase.FindAssets(paletname);
            }
            else
            {
                palet = AssetDatabase.FindAssets("MaterialpaletDefault");
            }
            if (palet.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(palet[0]);
                materialPalet = AssetDatabase.LoadAssetAtPath<MaterialPalet>(path);
            }
        }
        private static void UpdateMaterialPalette()
        {
            if (materialPalet != null)
            {
                currentmaterials.Clear();
                foreach (var material in materialPalet.materiallist)
                {
                    currentmaterials.Add(material);
                }
                EditorPrefs.SetString("materialPalet", materialPalet.name);
            }
        }
       
        static void GetGroups()
        {
            object[] groups = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Group));
            Groups = new List<string>();
            foreach (Group group in groups)
            {
                if (EditorUtility.IsPersistent(group.transform.root.gameObject))
                    continue;
                if (!Groups.Contains(group.GroupName))
                    Groups.Add(group.GroupName);
            }

            Groups.Sort();
        }
        
        [MenuItem("realvirtual/Material window (Pro)", false, 400)]
        static void Init()
        {
            MaterialWindow window =
                (MaterialWindow) EditorWindow.GetWindow(typeof(MaterialWindow));
            window.Show();
        }
        static void AddObjectsToSelection()
        {
            foreach (var myobj in Selection.objects)
            {
                if (myobj is GameObject && selectedobjs.IndexOf(myobj) == -1)
                {
                    selectedobjs.Add(myobj);
                }
            }
            Init();
        }

        void OnGUI()
        {
            if (materialPalet == null)
            {
                GetMaterialPalet();
            }
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
            float width = position.width;
           // Main vertical layout
            EditorGUILayout.BeginVertical();

            // Material Palette Header
            EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Current material list:", GUILayout.Width(width / 4 - 15));
                materialPalet = (MaterialPalet)EditorGUILayout.ObjectField(materialPalet, typeof(MaterialPalet), false, GUILayout.Width(width / 3 - 15));
                UpdateMaterialPalette();
                GUILayout.Space(15);
                if (GUILayout.Button("New material set", GUILayout.Width(width / 3 )))
                {
                    var newMaterialPalet = ScriptableObject.CreateInstance<MaterialPalet>();
                    AssetDatabase.CreateAsset(newMaterialPalet, "Assets/MaterialPalet.asset");
                    Selection.activeObject = newMaterialPalet; // Open in inspector
                }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            // Material List
            if (currentmaterials != null && currentmaterials.Count > 0)
            {
                foreach (var material in currentmaterials)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Material Name
                    GUILayout.Label(material.name, GUILayout.Width(width / 4 - 10));

                    // Material Preview
                    var preview = AssetPreview.GetAssetPreview(material);
                    if (preview)
                    {
                        GUILayout.Label(preview, GUILayout.Width(50), GUILayout.Height(50));
                    }
                    // Buttons for material actions
                    if (GUILayout.Button("Select", GUILayout.Width(width / 5), GUILayout.Height(45)))
                    {
                        SelectMaterial(material);
                    }
                    if (GUILayout.Button("Set part", GUILayout.Width(width / 5), GUILayout.Height(45)))
                    {
                        AssignMaterial(material, false);
                    }
                    if (GUILayout.Button("Set part & sub parts", GUILayout.Width(width / 5), GUILayout.Height(45)))
                    {
                        AssignMaterial(material, true);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("No materials available.", GUILayout.Width(width));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        void OnSceneGUI()
        {
            Event e = Event.current;

            // Check for "Ctrl + Z" or "Command + Z" (for macOS)
            if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.Z)
            {
                UndoLastMaterialAssignment();
                e.Use(); // Consume the event to prevent further processing
            }
        }

        private List<GameObject> GetAllSelectedObjectsIncludingSub(bool includeSubs)
        {
            List<GameObject> list = new List<GameObject>();
            AddObjectsToSelection();
            foreach (Object myobj in selectedobjs)
            {
                if(includeSubs)
                {
                    var objs = GatherObjects((GameObject)myobj);
                    foreach (var obj in objs)
                    {
                        list.Add(obj);
                    }
                }
                else
                {
                    list.Add((GameObject)myobj);
                }
            }

            selectedobjs.Clear();
            RecordUndo(ref list);
            return list;
        }
        public GameObject[] GatherObjects(GameObject root)
        {
            List<GameObject> objects = new List<GameObject>();
            Stack<GameObject> recurseStack = new Stack<GameObject>(new GameObject[] {root});

            while (recurseStack.Count > 0)
            {
                GameObject obj = recurseStack.Pop();
                objects.Add(obj);

                foreach (Transform childT in obj.transform)
                    recurseStack.Push(childT.gameObject);
            }

            return objects.ToArray();
        }
        private void RecordUndo(ref List<GameObject> list)
        {
            foreach (var go in list)
            {
                Undo.RecordObject(go, "Selection Window Changes");
            }
        }
        
        private void SelectMaterial(Material material)
        {
            if (material == null)
            {
                EditorUtility.DisplayDialog("No material selected", "Please select a material", "OK");
                return;
            }

            List<GameObject> list = new List<GameObject>();
            var groupcomps = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(MeshRenderer));

            foreach (var comp in groupcomps)
            {
                var gr = (MeshRenderer) comp;
                if (EditorUtility.IsPersistent(gr.transform.root.gameObject))
                    continue;
                if (gr.sharedMaterial.name.Contains(material.name))
                    list.Add(gr.gameObject);
            }

            Selection.objects = list.ToArray();
        }
        
        private void AssignMaterial(Material material,bool includeSubs)
        {
            if (material == null)
            {
                EditorUtility.DisplayDialog("No new material for asignment selected", "Please select a new material",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayCancelableProgressBar("Collecting objects", "Please wait",
                    0))
            {
                EditorUtility.ClearProgressBar();
                return;
            }
            var a = 0;
            List<GameObject> list = GetAllSelectedObjectsIncludingSub(includeSubs);
            foreach (var myobj in list)
            {
                a++;
                float progress = (float) a / (float) list.Count;
                var renderer = myobj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Progressing objects",
                            $"Material update on object {a} of {list.Count}",
                            progress))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                    if (!originalMaterials.ContainsKey(myobj))
                    {
                        Undo.RecordObject(renderer, "Material assignment");
                        originalMaterials[myobj] = renderer.sharedMaterials;
                    }
                    
                    Material[] sharedMaterialsCopy = renderer.sharedMaterials;

                    for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    {
                        sharedMaterialsCopy[i] = material;
                    }

                    renderer.sharedMaterials = sharedMaterialsCopy;
                }
            }

            EditorUtility.ClearProgressBar();
        }
        private void UndoLastMaterialAssignment()
        {
            if (originalMaterials.Count == 0)
            {
                Debug.LogWarning("No material changes to undo.");
                return;
            }

            foreach (var entry in originalMaterials)
            {
                var renderer = entry.Key.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterials = entry.Value;
                }
            }

            Debug.Log("Material changes undone.");
            originalMaterials.Clear();
        }
        
        private void GroupToIUnitySelection(string group)
        {
            var objs = GetAllWithGroup(group);
            Selection.objects = new Object[0];
            Selection.objects = objs.ToArray();
            activegroupsel = Array.ConvertAll(Selection.objects, item => (Object) item);
            activegroup = group;
        }
        public List<GameObject> GetAllWithGroup(string group)
        {
            List<GameObject> list = new List<GameObject>();
            var groupcomps = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Group));

            foreach (var comp in groupcomps)
            {
                var gr = (Group) comp;
                if (EditorUtility.IsPersistent(gr.transform.root.gameObject))
                    continue;
                if (gr.GroupName == group)
                {
                   
                    list.Add(gr.gameObject);
                }
            }

            return list;
        }
        private void AssignMaterialtoGroup(Material material)
        {
            var group = Groups[selectedGroupIndex];
            GroupToIUnitySelection(group);
            AssignMaterial(material,false);
            
        }
    }
}
