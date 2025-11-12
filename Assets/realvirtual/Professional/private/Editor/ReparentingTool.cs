// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace realvirtual
{
    public class ReparentingTool : EditorWindow
    {
                
        [MenuItem("realvirtual/Reparanting (Pro) (Alt+R) &r", false, 400)]
        static void ShowWindow()
        {
            ReparentingTool window = GetWindow<ReparentingTool>("Reparenting");
            window.minSize = new Vector2(400, 320);
            window.maxSize = new Vector2(400, 320);
            window.Show();
        }

        private GameObject selectedParent;
        private List<GameObject> objectsToMove = new List<GameObject>();
        private bool showObjectsList = false;
        private Vector2 scrollPosition;
        private bool showUndoButton = false;
        private string lastOperationDescription = "";

        private void OnEnable()
        {
            // If something is already selected when opening, use it as parent
            if (Selection.activeGameObject != null)
            {
                selectedParent = Selection.activeGameObject;
            }
            Repaint();
        }

        private void OnGUI()
        {
            // Calculate required window height based on content
            float baseHeight = 320f; // Base height for basic elements
            float extraHeight = 0f;
            
            // Add height for objects list if visible
            if (objectsToMove.Count > 0 && showObjectsList)
            {
                float listHeight = Mathf.Min(200f, objectsToMove.Count * 22f + 40f);
                extraHeight += listHeight + 40f; // Add some padding
            }
            
            // Add height for undo button if visible
            if (showUndoButton)
            {
                extraHeight += 50f;
            }
            
            float targetHeight = baseHeight + extraHeight;
            Vector2 newSize = new Vector2(400f, targetHeight);
            
            // Only resize if the difference is significant to avoid constant resizing
            if (Mathf.Abs(position.height - targetHeight) > 5f)
            {
                minSize = newSize;
                maxSize = newSize;
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Move objects to a selected parent (select in any order)", EditorStyles.helpBox);
            
            EditorGUILayout.Space(20);
            
            // Parent Selection
            EditorGUILayout.LabelField("Target Parent", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            selectedParent = EditorGUILayout.ObjectField("Target Parent:", selectedParent, typeof(GameObject), true) as GameObject;
            
            if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
            {
                if (Selection.activeGameObject != null)
                {
                    selectedParent = Selection.activeGameObject;
                }
                else
                {
                    EditorUtility.DisplayDialog("No Selection", "Please select a GameObject in the hierarchy first.", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Objects to Move Selection
            EditorGUILayout.LabelField("Objects to Move", EditorStyles.boldLabel);
            
            // Button to add currently selected objects
            EditorGUILayout.BeginHorizontal();
            GameObject[] currentSelection = Selection.gameObjects.Where(obj => obj != selectedParent && obj != null).ToArray();
            GUI.enabled = currentSelection.Length > 0;
            if (GUILayout.Button($"Add Selected ({currentSelection.Length})", GUILayout.Width(150)))
            {
                AddSelectedObjectsToMoveList();
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("Clear List", GUILayout.Width(80)))
            {
                objectsToMove.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            // Clean up the objects to move list (remove null references and duplicates)
            objectsToMove.RemoveAll(obj => obj == null || obj == selectedParent);
            objectsToMove = objectsToMove.Distinct().ToList();
            
            var canMove = selectedParent != null ? objectsToMove.Where(obj => obj.transform.parent != selectedParent.transform).ToList() : new List<GameObject>();
            
            // Show objects to move list status
            if (objectsToMove.Count > 0)
            {
                EditorGUILayout.HelpBox($"Objects to move: {objectsToMove.Count}. {canMove.Count} can be moved to selected parent.", MessageType.Info);
                
                // Foldout to show/hide objects list
                showObjectsList = EditorGUILayout.Foldout(showObjectsList, "Show Objects List", true);
                
                if (showObjectsList)
                {
                    // Scroll view for objects list with max height
                    float maxHeight = Mathf.Min(200f, objectsToMove.Count * 20f + 20f);
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(maxHeight));
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    int indexToRemove = -1;
                    for (int i = 0; i < objectsToMove.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(objectsToMove[i], typeof(GameObject), true);
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            indexToRemove = i;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                    
                    // Remove after GUI layout is complete
                    if (indexToRemove >= 0)
                    {
                        objectsToMove.RemoveAt(indexToRemove);
                    }
                    
                    EditorGUILayout.EndScrollView();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Use 'Add Selected' button to add objects to the move list.", MessageType.Info);
            }
            
            EditorGUILayout.Space(10);
            
            // Show parent status and move button
            if (selectedParent == null)
            {
                EditorGUILayout.HelpBox("Select a target parent above to enable moving.", MessageType.Warning);
                GUI.enabled = false;
            }
            else
            {
                GUI.enabled = canMove.Count > 0;
            }
            
            string buttonText = selectedParent != null && canMove.Count > 0 ? $"MOVE TO PARENT ({canMove.Count})" : "MOVE TO PARENT";
            
            // Make button green when everything is ready
            if (selectedParent != null && canMove.Count > 0)
            {
                GUI.backgroundColor = Color.green;
            }
            
            if (GUILayout.Button(buttonText, GUILayout.Height(60)))
            {
                MoveSelectedObjectsToParent();
            }
            
            GUI.backgroundColor = Color.white; // Reset color
            EditorGUILayout.Space(10);
            GUI.enabled = true;
            
            // Show undo button if last operation was successful
            if (showUndoButton)
            {
                EditorGUILayout.Space(10);
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"UNDO: {lastOperationDescription}", GUILayout.Height(30)))
                {
                    Undo.PerformUndo();
                    showUndoButton = false;
                    lastOperationDescription = "";
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void AddSelectedObjectsToMoveList()
        {
            GameObject[] currentSelection = Selection.gameObjects.Where(obj => obj != selectedParent && obj != null).ToArray();
            
            foreach (GameObject obj in currentSelection)
            {
                if (!objectsToMove.Contains(obj))
                {
                    objectsToMove.Add(obj);
                }
            }
            
            Debug.Log($"Added {currentSelection.Length} object(s) to move list.");
        }
        
        private void MoveSelectedObjectsToParent()
        {
            if (selectedParent == null)
            {
                EditorUtility.DisplayDialog("Invalid Operation", "No parent selected.", "OK");
                return;
            }
            
            // Check if target parent is a prefab or part of a prefab
            if (PrefabUtility.IsPartOfPrefabAsset(selectedParent))
            {
                EditorUtility.DisplayDialog("Invalid Operation", 
                    "Cannot move objects into a prefab asset. Please instantiate the prefab in the scene first.", "OK");
                return;
            }
            
            if (PrefabUtility.IsPartOfPrefabInstance(selectedParent) && 
                PrefabUtility.GetPrefabInstanceStatus(selectedParent) == PrefabInstanceStatus.Connected)
            {
                EditorUtility.DisplayDialog("Invalid Operation", 
                    "Cannot move objects into a connected prefab instance. Please unpack the prefab instance first or select a regular GameObject.", "OK");
                return;
            }
            
            var validObjects = objectsToMove.Where(obj => obj != null && obj != selectedParent && obj.transform.parent != selectedParent.transform).ToList();
            
            if (validObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("Invalid Operation", "No valid objects selected to move.", "OK");
                return;
            }
            
            // Create undo group for the entire operation
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Move {validObjects.Count} objects to '{selectedParent.name}'");
            
            // Register undo for all objects
            Undo.RegisterCompleteObjectUndo(validObjects.ToArray(), "Move Objects to Parent");
            
            int successCount = 0;
            List<string> errors = new List<string>();
            
            foreach (GameObject obj in validObjects)
            {
                try
                {
                    // Check for circular dependency (object cannot be parent of its ancestor)
                    if (IsCircularDependency(obj, selectedParent))
                    {
                        errors.Add($"Skipped '{obj.name}': Would create circular dependency");
                        continue;
                    }
                    
                    // Store world position and rotation
                    Vector3 worldPosition = obj.transform.position;
                    Quaternion worldRotation = obj.transform.rotation;
                    
                    // Perform reparenting with undo support
                    Undo.SetTransformParent(obj.transform, selectedParent.transform, "Move Object to Parent");
                    
                    // Restore world position and rotation
                    obj.transform.position = worldPosition;
                    obj.transform.rotation = worldRotation;
                    
                    successCount++;
                }
                catch (System.Exception e)
                {
                    errors.Add($"Error moving '{obj.name}': {e.Message}");
                }
            }
            
            // Show results
            if (errors.Count > 0)
            {
                string message = $"Operation completed!\n\nSuccessful: {successCount}\nErrors: {errors.Count}\n\nDetails:\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Move to Parent Results", message, "OK");
            }
            else
            {
                Debug.Log($"Successfully moved {successCount} object(s) to '{selectedParent.name}'.");
            }
            
            // Clear objects to move list and mark scene dirty after successful operation
            if (successCount > 0)
            {
                objectsToMove.Clear();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                
                // Collapse undo group and show undo button
                Undo.CollapseUndoOperations(undoGroup);
                showUndoButton = true;
                lastOperationDescription = $"Moved {successCount} objects to '{selectedParent.name}'";
            }
        }
        
        private bool IsCircularDependency(GameObject child, GameObject potentialParent)
        {
            if (child == potentialParent)
                return true;
                
            Transform current = potentialParent.transform;
            while (current != null)
            {
                if (current.gameObject == child)
                    return true;
                current = current.parent;
            }
            
            return false;
        }
        
        private void OnSelectionChange()
        {
            // Repaint the window when selection changes to update the UI
            Repaint();
        }
    }
}