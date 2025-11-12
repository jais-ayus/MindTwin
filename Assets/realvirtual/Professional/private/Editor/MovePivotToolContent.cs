// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace realvirtual
{
    //! Shared content for Move Pivot Tool - used by both overlay and window modes
    public class MovePivotToolContent
    {
        // UI State
        private GameObject sourceObject;
        private GameObject targetObject;
        private PivotMoveMethod currentMethod = PivotMoveMethod.ToAnotherObject;
        private List<Vector3> selectedPoints = new List<Vector3>();
        private Vector3 previewPosition;
        private bool matchRotation;
        private bool useMeshCenter;
        
        // Visual Elements
        private ObjectField sourceObjectField;
        private DropdownField methodDropdown;
        private VisualElement targetSettingsContainer;
        private Button applyButton;
        private Button undoButton;
        private Button resetButton;
        private Button selectSourceButton;
        private Button selectTargetButton;
        private Toggle matchRotationToggle;
        private Toggle useMeshCenterToggle;
        
        // Undo Support
        private Stack<TransformState> undoStack = new Stack<TransformState>();
        
        // Scene Interaction
        private bool isSelectingVertex;
        
        // Gizmo Settings
        private static readonly Color CurrentPivotColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);
        private static readonly Color PreviewPivotColor = new Color(0.8f, 0.8f, 0.2f, 0.8f);
        private static readonly Color VertexHighlightColor = new Color(0.8f, 0.3f, 0.3f, 1f);
        
        // Point colors for visualization
        private static readonly Color[] PointColors = new Color[]
        {
            new Color(0.2f, 0.4f, 0.8f), // Dark blue - Point 1
            new Color(0.3f, 0.5f, 0.9f), // Medium blue - Point 2
            new Color(0.4f, 0.6f, 1.0f)  // Light blue - Point 3
        };
        
        // Handle sizes
        private const float VERTEX_HANDLE_SIZE = 0.05f;
        private const float PIVOT_HANDLE_SIZE = 0.1f;
        
        public enum PivotMoveMethod
        {
            ToAnotherObject,
            ToVertex,
            BetweenTwoPoints,
            ToTriangleCenter,
            ToMeshCenter
        }
        
        private struct TransformState
        {
            public GameObject gameObject;
            public Vector3 position;
            public Quaternion rotation;
            
            public TransformState(GameObject obj)
            {
                gameObject = obj;
                position = obj.transform.position;
                rotation = obj.transform.rotation;
            }
        }
        
        public void CreateGUI(VisualElement root)
        {
            if (root == null) return;
            
            // Add padding to root
            root.style.paddingTop = 3;
            root.style.paddingBottom = 3;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            
            // Source Object Section with toggle button
            var sourceSection = CreateSection("Source Object");
            
            // Create header row with Source Object label and toggle button
            #if UNITY_2021_2_OR_NEWER && REALVIRTUAL_PROFESSIONAL
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 5;
            
            var label = new Label("Source Object");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12;
            label.style.color = new Color(0.85f, 0.85f, 0.85f);
            label.style.marginLeft = 0;
            headerRow.Add(label);
            
            var toggleButton = new Button(() => {
                MovePivotTool.ToggleMovePivotMode();
            });
            toggleButton.text = IsWindowMode() ? "Overlay" : "Window";
            toggleButton.tooltip = IsWindowMode() ? "Switch to overlay mode in Scene view" : "Switch to dockable window mode";
            toggleButton.style.fontSize = 9;
            toggleButton.style.height = 18;
            toggleButton.style.paddingTop = 1;
            toggleButton.style.paddingBottom = 1;
            toggleButton.style.paddingLeft = 6;
            toggleButton.style.paddingRight = 6;
            headerRow.Add(toggleButton);
            
            sourceSection.Clear(); // Clear the default header
            sourceSection.Add(headerRow);
            #endif
            
            var sourceRow = new VisualElement();
            sourceRow.style.flexDirection = FlexDirection.Row;
            sourceRow.style.alignItems = Align.Center;
            
            sourceObjectField = new ObjectField()
            {
                objectType = typeof(GameObject),
                value = sourceObject
            };
            
            // If source object was already set (from ShowWindow), update everything
            if (sourceObject != null)
            {
                UpdatePreview();
                UpdateButtonStates();
            }
            sourceObjectField.style.flexGrow = 1;
            sourceObjectField.RegisterValueChangedCallback(evt => 
            {
                if (evt != null)
                {
                    sourceObject = evt.newValue as GameObject;
                    UpdatePreview();
                    UpdateButtonStates();
                }
            });
            
            selectSourceButton = new Button(() => {
                if (Selection.activeGameObject != null)
                {
                    sourceObject = Selection.activeGameObject;
                    sourceObjectField.value = sourceObject;
                    UpdatePreview();
                    UpdateButtonStates();
                }
            }) { text = "Select" };
            selectSourceButton.style.marginLeft = 5;
            selectSourceButton.style.minWidth = 60;
            selectSourceButton.style.width = 60;
            
            sourceRow.Add(sourceObjectField);
            sourceRow.Add(selectSourceButton);
            sourceSection.Add(sourceRow);
            root.Add(sourceSection);
            
            // Method Selection - just dropdown with descriptive text
            var methodContainer = new VisualElement();
            methodContainer.style.paddingTop = 2;
            methodContainer.style.paddingBottom = 2;
            methodContainer.style.paddingLeft = 0;
            methodContainer.style.paddingRight = 0;
            
            methodDropdown = new DropdownField(
                GetMethodNames(), 
                (int)currentMethod);
            methodDropdown.RegisterValueChangedCallback(evt =>
            {
                currentMethod = (PivotMoveMethod)methodDropdown.index;
                UpdateTargetSettings();
                UpdatePreview();
                AdjustMinimumSize();
            });
            
            methodContainer.Add(methodDropdown);
            root.Add(methodContainer);
            
            // Dynamic Target Settings
            targetSettingsContainer = CreateSection("Target Settings");
            UpdateTargetSettings();
            root.Add(targetSettingsContainer);
            
            
            // Action Buttons
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.marginTop = 5;
#if UNITY_6000_0_OR_NEWER
            buttonContainer.style.justifyContent = Justify.SpaceEvenly;
#else
            buttonContainer.style.justifyContent = Justify.SpaceBetween; // Unity 2022 fallback
#endif
            
            applyButton = new Button(() => ApplyPivotMove()) { text = "Apply" };
            applyButton.style.flexGrow = 1;
            applyButton.style.height = 25;
            applyButton.style.marginRight = 4;
            applyButton.style.minWidth = 60;
            
            undoButton = new Button(() => UndoPivotMove()) { text = "Undo" };
            undoButton.style.flexGrow = 1;
            undoButton.style.height = 25;
            undoButton.style.marginRight = 4;
            undoButton.style.minWidth = 60;
            
            resetButton = new Button(() => ResetTool()) { text = "Reset" };
            resetButton.style.flexGrow = 1;
            resetButton.style.height = 25;
            resetButton.style.marginRight = 4;
            resetButton.style.minWidth = 60;
            
            var closeButton = new Button(() => CloseTool()) { text = "Close" };
            closeButton.style.flexGrow = 1;
            closeButton.style.height = 25;
            closeButton.style.minWidth = 60;
            
            buttonContainer.Add(applyButton);
            buttonContainer.Add(undoButton);
            buttonContainer.Add(resetButton);
            buttonContainer.Add(closeButton);
            
            root.Add(buttonContainer);
            
            UpdateButtonStates();
            
            // Setup callbacks
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            
            // Set initial source object if available
            if (sourceObject == null && Selection.activeGameObject != null)
            {
                sourceObject = Selection.activeGameObject;
                if (sourceObjectField != null)
                {
                    sourceObjectField.value = sourceObject;
                    UpdatePreview();
                    UpdateButtonStates();
                }
            }
            
            // Adjust minimum size based on current method
            AdjustMinimumSize();
        }
        
        public void Cleanup()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            isSelectingVertex = false;
        }
        
        private void OnSelectionChanged()
        {
            // Auto-set source object when selection changes and source is empty
            if (sourceObject == null && Selection.activeGameObject != null)
            {
                sourceObject = Selection.activeGameObject;
                if (sourceObjectField != null)
                {
                    sourceObjectField.value = sourceObject;
                    UpdatePreview();
                    UpdateButtonStates();
                }
            }
        }
        
        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.paddingTop = 2;
            section.style.paddingBottom = 2;
            section.style.paddingLeft = 0;
            section.style.paddingRight = 0;
            section.style.marginBottom = 2;
            
            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2;
            header.style.fontSize = 12;
            header.style.color = new Color(0.85f, 0.85f, 0.85f);
            header.style.marginLeft = 0;
            section.Add(header);
            
            return section;
        }
        
        private VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            separator.style.marginTop = 5;
            separator.style.marginBottom = 5;
            separator.style.marginLeft = 0;
            separator.style.marginRight = 0;
            return separator;
        }
        
        private List<string> GetMethodNames()
        {
            return new List<string>
            {
                "Move to Pivot of another Object",
                "Move to selected Vertex",
                "Move to Center between two Points",
                "Move to Center of Triangle",
                "Move to Target's Mesh Center"
            };
        }
        
        private void UpdateTargetSettings()
        {
            if (targetSettingsContainer == null) return;
            
            targetSettingsContainer.Clear();
            
            // Keep the section header
            var header = new Label("Target Settings");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2;
            targetSettingsContainer.Add(header);
            
            switch (currentMethod)
            {
                case PivotMoveMethod.ToAnotherObject:
                    CreateToAnotherObjectPanel();
                    break;
                case PivotMoveMethod.ToVertex:
                    CreateToVertexPanel();
                    break;
                case PivotMoveMethod.BetweenTwoPoints:
                    CreateBetweenPointsPanel();
                    break;
                case PivotMoveMethod.ToTriangleCenter:
                    CreateTriangleCenterPanel();
                    break;
                case PivotMoveMethod.ToMeshCenter:
                    CreateMeshCenterPanel();
                    break;
            }
            
            // Adjust window size after updating content
            AdjustMinimumSize();
        }
        
        private void CreateToAnotherObjectPanel()
        {
            // Target object row - no label
            var targetRow = new VisualElement();
            targetRow.style.flexDirection = FlexDirection.Row;
            targetRow.style.alignItems = Align.Center;
            targetRow.style.marginBottom = 5;
            
            var targetObjectField = new ObjectField()
            {
                objectType = typeof(GameObject),
                value = targetObject
            };
            targetObjectField.style.flexGrow = 1;
            targetObjectField.RegisterValueChangedCallback(evt =>
            {
                if (evt != null)
                {
                    targetObject = evt.newValue as GameObject;
                    UpdatePreview();
                    UpdateButtonStates();
                }
            });
            
            selectTargetButton = new Button(() => {
                if (Selection.activeGameObject != null && Selection.activeGameObject != sourceObject)
                {
                    targetObject = Selection.activeGameObject;
                    targetObjectField.value = targetObject;
                    UpdatePreview();
                    UpdateButtonStates();
                }
            }) { text = "Set" };
            selectTargetButton.style.marginLeft = 5;
            selectTargetButton.style.minWidth = 60;
            selectTargetButton.style.width = 60;
            
            targetRow.Add(targetObjectField);
            targetRow.Add(selectTargetButton);
            targetSettingsContainer.Add(targetRow);
            
            // Options in one row with proper alignment
            var optionsRow = new VisualElement();
            optionsRow.style.flexDirection = FlexDirection.Row;
            optionsRow.style.alignItems = Align.Center;
            optionsRow.style.marginTop = 5;
            optionsRow.style.justifyContent = Justify.FlexStart;
            
            matchRotationToggle = new Toggle() { value = matchRotation };
            matchRotationToggle.text = "Match Rotation";
            matchRotationToggle.style.marginRight = 20;
            matchRotationToggle.style.flexGrow = 0;
            matchRotationToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt != null)
                {
                    matchRotation = evt.newValue;
                    UpdatePreview();
                }
            });
            
            useMeshCenterToggle = new Toggle() { value = useMeshCenter };
            useMeshCenterToggle.text = "Use Target's Mesh Center";
            useMeshCenterToggle.style.flexGrow = 0;
            useMeshCenterToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt != null)
                {
                    useMeshCenter = evt.newValue;
                    UpdatePreview();
                }
            });
            
            optionsRow.Add(matchRotationToggle);
            optionsRow.Add(useMeshCenterToggle);
            targetSettingsContainer.Add(optionsRow);
            
            UpdateButtonStates();
        }
        
        private void CreateToVertexPanel()
        {
            var instructionLabel = new Label("Click a vertex in the Scene View");
            targetSettingsContainer.Add(instructionLabel);
            
            if (selectedPoints.Count == 0)
            {
                var statusLabel = new Label("Waiting for vertex selection...");
                statusLabel.style.marginTop = 10;
                statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                targetSettingsContainer.Add(statusLabel);
                
                var cancelButton = new Button(() => CancelVertexSelection()) { text = "Cancel Selection" };
                cancelButton.style.marginTop = 10;
                targetSettingsContainer.Add(cancelButton);
                
                // Start vertex selection mode
                StartVertexSelection();
            }
            else
            {
                var pointContainer = new VisualElement();
                pointContainer.style.marginTop = 10;
                
                var pointButton = new Button(() => ReselectPoint(0)) 
                { 
                    text = $"× Point: {FormatVector(selectedPoints[0])}" 
                };
                pointButton.style.unityTextAlign = TextAnchor.MiddleLeft;
                pointButton.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f); // Blue
                pointButton.style.color = Color.white;
                pointButton.style.paddingLeft = 5;
                pointButton.style.paddingRight = 5;
                pointButton.style.height = 22;
                pointButton.tooltip = "Click to reselect this point";
                
                pointContainer.Add(pointButton);
                targetSettingsContainer.Add(pointContainer);
                
                // Show that selection is still active
                if (isSelectingVertex)
                {
                    var activeLabel = new Label("Click any vertex to change selection");
                    activeLabel.style.marginTop = 5;
                    activeLabel.style.color = new Color(0.8f, 0.8f, 0.2f);
                    activeLabel.style.fontSize = 11;
                    targetSettingsContainer.Add(activeLabel);
                }
                
                var clearButton = new Button(() => ClearSelectedPoints()) { text = "Clear Selection" };
                clearButton.style.marginTop = 10;
                targetSettingsContainer.Add(clearButton);
                
                // Keep vertex selection active for continuous reselection
                if (!isSelectingVertex)
                    StartVertexSelection();
            }
        }
        
        private void CreateBetweenPointsPanel()
        {
            var instructionLabel = new Label("Select two vertices to place pivot at center");
            targetSettingsContainer.Add(instructionLabel);
            
            // Point 1
            var point1Container = new VisualElement();
            point1Container.style.marginTop = 10;
            
            if (selectedPoints.Count > 0)
            {
                var point1Button = new Button(() => ReselectPoint(0)) 
                { 
                    text = $"× Point 1: {FormatVector(selectedPoints[0])}" 
                };
                point1Button.style.unityTextAlign = TextAnchor.MiddleLeft;
                point1Button.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f); // Blue
                point1Button.style.color = Color.white;
                point1Button.style.paddingLeft = 5;
                point1Button.style.paddingRight = 5;
                point1Button.style.height = 22;
                point1Button.tooltip = "Click to reselect this point";
                point1Container.Add(point1Button);
            }
            else
            {
                var point1Label = new Label("Point 1: Click in Scene View...");
                point1Label.style.color = new Color(0.7f, 0.7f, 0.7f);
                point1Container.Add(point1Label);
            }
            targetSettingsContainer.Add(point1Container);
            
            // Point 2
            var point2Container = new VisualElement();
            point2Container.style.marginTop = 5;
            
            if (selectedPoints.Count > 1)
            {
                var point2Button = new Button(() => ReselectPoint(1)) 
                { 
                    text = $"× Point 2: {FormatVector(selectedPoints[1])}" 
                };
                point2Button.style.unityTextAlign = TextAnchor.MiddleLeft;
                point2Button.style.backgroundColor = new Color(0.3f, 0.5f, 0.9f); // Lighter blue
                point2Button.style.color = Color.white;
                point2Button.style.paddingLeft = 5;
                point2Button.style.paddingRight = 5;
                point2Button.style.height = 22;
                point2Button.tooltip = "Click to reselect this point";
                point2Container.Add(point2Button);
            }
            else
            {
                var point2Label = new Label("Point 2: Click in Scene View...");
                point2Label.style.color = new Color(0.7f, 0.7f, 0.7f);
                point2Container.Add(point2Label);
            }
            targetSettingsContainer.Add(point2Container);
            
            if (selectedPoints.Count >= 2)
            {
                var midpointLabel = new Label($"Midpoint: {FormatVector((selectedPoints[0] + selectedPoints[1]) / 2)}");
                midpointLabel.style.marginTop = 10;
                midpointLabel.style.color = new Color(0.5f, 1f, 0.5f);
                midpointLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                targetSettingsContainer.Add(midpointLabel);
            }
            
            var clearButton = new Button(() => ClearSelectedPoints()) { text = "Clear Points" };
            clearButton.style.marginTop = 10;
            targetSettingsContainer.Add(clearButton);
            
            if (selectedPoints.Count < 2)
                StartVertexSelection();
        }
        
        private void CreateTriangleCenterPanel()
        {
            var instructionLabel = new Label("Select three vertices to find triangle center");
            targetSettingsContainer.Add(instructionLabel);
            
            for (int i = 0; i < 3; i++)
            {
                var pointContainer = new VisualElement();
                if (i == 0) pointContainer.style.marginTop = 10;
                else pointContainer.style.marginTop = 5;
                
                if (selectedPoints.Count > i)
                {
                    var index = i; // Capture for closure
                    var pointButton = new Button(() => ReselectPoint(index)) 
                    { 
                        text = $"× Point {index + 1}: {FormatVector(selectedPoints[index])}" 
                    };
                    pointButton.style.unityTextAlign = TextAnchor.MiddleLeft;
                    pointButton.style.backgroundColor = PointColors[i];
                    pointButton.style.color = Color.white;
                    pointButton.style.paddingLeft = 5;
                    pointButton.style.paddingRight = 5;
                    pointButton.style.height = 22;
                    pointButton.tooltip = "Click to reselect this point";
                    pointContainer.Add(pointButton);
                }
                else
                {
                    var pointLabel = new Label($"Point {i + 1}: Click in Scene View...");
                    pointLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    pointContainer.Add(pointLabel);
                }
                targetSettingsContainer.Add(pointContainer);
            }
            
            if (selectedPoints.Count >= 3)
            {
                var center = (selectedPoints[0] + selectedPoints[1] + selectedPoints[2]) / 3f;
                var centerLabel = new Label($"Center: {FormatVector(center)}");
                centerLabel.style.marginTop = 10;
                centerLabel.style.color = new Color(0.5f, 1f, 0.5f);
                centerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                targetSettingsContainer.Add(centerLabel);
            }
            
            var clearButton = new Button(() => ClearSelectedPoints()) { text = "Clear Points" };
            clearButton.style.marginTop = 10;
            targetSettingsContainer.Add(clearButton);
            
            if (selectedPoints.Count < 3)
                StartVertexSelection();
        }
        
        private void CreateMeshCenterPanel()
        {
            var instructionLabel = new Label("Select target object to use its mesh center");
            targetSettingsContainer.Add(instructionLabel);
            
            // Target object row
            var targetRow = new VisualElement();
            targetRow.style.flexDirection = FlexDirection.Row;
            targetRow.style.alignItems = Align.Center;
            targetRow.style.marginTop = 5;
            
            var targetObjectField = new ObjectField()
            {
                objectType = typeof(GameObject),
                value = targetObject
            };
            targetObjectField.style.flexGrow = 1;
            targetObjectField.RegisterValueChangedCallback(evt =>
            {
                if (evt != null)
                {
                    targetObject = evt.newValue as GameObject;
                    UpdatePreview();
                    UpdateButtonStates();
                    UpdateTargetSettings(); // Refresh to show mesh info
                }
            });
            
            selectTargetButton = new Button(() => {
                if (Selection.activeGameObject != null && Selection.activeGameObject != sourceObject)
                {
                    targetObject = Selection.activeGameObject;
                    targetObjectField.value = targetObject;
                    UpdatePreview();
                    UpdateButtonStates();
                    UpdateTargetSettings(); // Refresh to show mesh info
                }
            }) { text = "Set" };
            selectTargetButton.style.marginLeft = 5;
            selectTargetButton.style.minWidth = 60;
            selectTargetButton.style.width = 60;
            
            targetRow.Add(targetObjectField);
            targetRow.Add(selectTargetButton);
            targetSettingsContainer.Add(targetRow);
            
            if (targetObject != null)
            {
                var meshFilter = targetObject.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    var bounds = meshFilter.sharedMesh.bounds;
                    var centerLabel = new Label($"Mesh Center: {bounds.center}");
                    centerLabel.style.marginTop = 10;
                    centerLabel.style.color = new Color(0.5f, 1f, 0.5f);
                    targetSettingsContainer.Add(centerLabel);
                    
                    var sizeLabel = new Label($"Bounds Size: {bounds.size}");
                    targetSettingsContainer.Add(sizeLabel);
                }
                else
                {
                    var errorLabel = new Label("No mesh found on target object");
                    errorLabel.style.marginTop = 10;
                    errorLabel.style.color = new Color(1f, 0.5f, 0.5f);
                    targetSettingsContainer.Add(errorLabel);
                }
            }
        }
        
        private void UpdatePreview()
        {
            if (sourceObject == null)
            {
                return;
            }
            
            Vector3? newPos = CalculateNewPosition();
            if (newPos.HasValue)
            {
                previewPosition = newPos.Value;
            }
            
            UpdateButtonStates();
            SceneView.RepaintAll();
        }
        
        private Vector3? CalculateNewPosition()
        {
            switch (currentMethod)
            {
                case PivotMoveMethod.ToAnotherObject:
                    if (targetObject != null)
                    {
                        if (useMeshCenter)
                        {
                            var meshFilter = targetObject.GetComponent<MeshFilter>();
                            if (meshFilter != null && meshFilter.sharedMesh != null)
                            {
                                return targetObject.transform.TransformPoint(meshFilter.sharedMesh.bounds.center);
                            }
                        }
                        return targetObject.transform.position;
                    }
                    break;
                    
                case PivotMoveMethod.ToVertex:
                    if (selectedPoints.Count > 0)
                        return selectedPoints[0];
                    break;
                    
                case PivotMoveMethod.BetweenTwoPoints:
                    if (selectedPoints.Count >= 2)
                        return (selectedPoints[0] + selectedPoints[1]) / 2f;
                    break;
                    
                case PivotMoveMethod.ToTriangleCenter:
                    if (selectedPoints.Count >= 3)
                        return (selectedPoints[0] + selectedPoints[1] + selectedPoints[2]) / 3f;
                    break;
                    
                case PivotMoveMethod.ToMeshCenter:
                    if (targetObject != null)
                    {
                        var meshFilter = targetObject.GetComponent<MeshFilter>();
                        if (meshFilter != null && meshFilter.sharedMesh != null)
                        {
                            return targetObject.transform.TransformPoint(meshFilter.sharedMesh.bounds.center);
                        }
                    }
                    break;
            }
            
            return null;
        }
        
        private void UpdateButtonStates()
        {
            bool canApply = sourceObject != null && CalculateNewPosition().HasValue;
            bool canUndo = undoStack.Count > 0;
            bool hasContent = sourceObject != null || targetObject != null || selectedPoints.Count > 0;
            
            applyButton?.SetEnabled(canApply);
            undoButton?.SetEnabled(canUndo);
            resetButton?.SetEnabled(hasContent);
            
            // Update button colors
            SetButtonColor(applyButton, canApply, new Color(0.3f, 0.6f, 0.3f)); // Green
            SetButtonColor(undoButton, canUndo, new Color(0.7f, 0.3f, 0.3f)); // Red
            SetButtonColor(resetButton, hasContent, new Color(0.7f, 0.6f, 0.2f)); // Yellow
            
            // Update button colors based on selection
            if (selectSourceButton != null)
            {
                bool needsSource = sourceObject == null;
                SetButtonColor(selectSourceButton, true, 
                    needsSource ? new Color(0.7f, 0.6f, 0.2f) : new Color(0.3f, 0.6f, 0.3f)); // Yellow if needed, Green if selected
            }
            
            if (selectTargetButton != null)
            {
                bool needsTarget = currentMethod == PivotMoveMethod.ToAnotherObject && sourceObject != null && targetObject == null;
                bool hasTarget = targetObject != null;
                
                if (hasTarget || needsTarget)
                {
                    SetButtonColor(selectTargetButton, true,
                        hasTarget ? new Color(0.3f, 0.6f, 0.3f) : new Color(0.7f, 0.6f, 0.2f)); // Green if selected, Yellow if needed
                }
                else
                {
                    SetButtonColor(selectTargetButton, false, Color.clear);
                }
            }
        }
        
        private void ApplyPivotMove()
        {
            if (sourceObject == null || sourceObject.transform == null || !CalculateNewPosition().HasValue)
                return;
                
            // Store current state for undo
            if (undoStack != null)
            {
                undoStack.Push(new TransformState(sourceObject));
            }
            
            // Apply the transformation
            Undo.RecordObject(sourceObject.transform, "Move Pivot");
            sourceObject.transform.position = previewPosition;
            
            if (matchRotation && targetObject != null)
            {
                sourceObject.transform.rotation = targetObject.transform.rotation;
            }
            
            EditorUtility.SetDirty(sourceObject);
            UpdatePreview();
            
            // Clear selection after applying
            if (currentMethod != PivotMoveMethod.ToAnotherObject)
            {
                ClearSelectedPoints();
                if (currentMethod != PivotMoveMethod.ToMeshCenter)
                {
                    UpdateTargetSettings();
                }
            }
        }
        
        private void UndoPivotMove()
        {
            if (undoStack.Count == 0)
                return;
                
            var lastState = undoStack.Pop();
            
            // Restore previous state
            Undo.RecordObject(lastState.gameObject.transform, "Undo Move Pivot");
            lastState.gameObject.transform.position = lastState.position;
            lastState.gameObject.transform.rotation = lastState.rotation;
            
            EditorUtility.SetDirty(lastState.gameObject);
            UpdatePreview();
        }
        
        private void ResetTool()
        {
            sourceObject = null;
            targetObject = null;
            selectedPoints.Clear();
            currentMethod = PivotMoveMethod.ToAnotherObject;
            matchRotation = false;
            useMeshCenter = false;
            isSelectingVertex = false;
            
            if (sourceObjectField != null)
                sourceObjectField.value = null;
            
            if (methodDropdown != null)
                methodDropdown.index = 0;
                
            UpdateTargetSettings();
            UpdatePreview();
            UpdateButtonStates();
            SceneView.RepaintAll();
        }
        
        private void CloseTool()
        {
            // Check if we're in overlay or window mode
            if (IsWindowMode())
            {
                // Close the window
                var window = UnityEngine.Resources.FindObjectsOfTypeAll<MovePivotWindow>().FirstOrDefault();
                if (window != null)
                {
                    window.Close();
                }
            }
            else
            {
                // Hide the overlay
                #if UNITY_2022_1_OR_NEWER
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    var overlayCanvas = sceneView.overlayCanvas;
                    if (overlayCanvas != null)
                    {
#if UNITY_6000_0_OR_NEWER
                        var movePivotOverlay = overlayCanvas.overlays.FirstOrDefault(o => o.GetType().Name == "MovePivotOverlay");
                        if (movePivotOverlay != null)
                        {
                            movePivotOverlay.displayed = false;
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
                            displayedProperty?.SetValue(movePivotOverlayObj, false);
                        }
#endif
                    }
                }
                #else
                // For older versions, try to find and hide the overlay
                #if REALVIRTUAL_PROFESSIONAL
                var overlays = UnityEngine.Resources.FindObjectsOfTypeAll<MovePivotOverlay>();
                if (overlays != null && overlays.Length > 0)
                {
                    overlays[0].displayed = false;
                }
                #endif
                #endif
                
                // Save the preference
                EditorPrefs.SetBool("realvirtual_MovePivotOverlayVisible", false);
            }
        }
        
        
        private void StartVertexSelection()
        {
            isSelectingVertex = true;
            SceneView.duringSceneGui += OnSceneGUI;
        }
        
        private void CancelVertexSelection()
        {
            isSelectingVertex = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            UpdateTargetSettings();
        }
        
        private void ClearSelectedPoints()
        {
            selectedPoints.Clear();
            UpdateTargetSettings();
            UpdatePreview();
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (sceneView == null) return;
            
            Event current = Event.current;
            if (current == null) return;
            
            // Handle escape key
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                isSelectingVertex = false;
                SceneView.duringSceneGui -= OnSceneGUI;
                current.Use();
                UpdateTargetSettings();
                return;
            }
            
            if (isSelectingVertex)
            {
                HandleVertexSelection(current);
                
                // Handle mouse movement and camera changes
                if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
                {
                    sceneView.Repaint();
                }
            }
            
            // Draw preview gizmos
            DrawPreviewGizmos();
        }
        
        
        private void HandleVertexSelection(Event current)
        {
            // Take control of mouse input when selecting vertices
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }
            
            GameObject foundObj = null;
            Vector3 vertex = Vector3.zero;
            bool found = false;
            
            if (current != null)
            {
                found = HandleUtility.FindNearestVertex(current.mousePosition, out vertex, out foundObj);
            }
            
            // Always repaint when in selection mode
            if (current.type == EventType.Layout)
            {
                HandleUtility.Repaint();
            }
            
            if (found)
            {
                // Draw vertex highlight
                Handles.color = VertexHighlightColor;
                float size = HandleUtility.GetHandleSize(vertex) * VERTEX_HANDLE_SIZE;
                Handles.SphereHandleCap(0, vertex, Quaternion.identity, size, EventType.Repaint);
                
                // Request continuous repaint while hovering
                SceneView.RepaintAll();
                
                if (current.type == EventType.MouseDown && current.button == 0)
                {
                    switch (currentMethod)
                    {
                        case PivotMoveMethod.ToVertex:
                            selectedPoints.Clear();
                            selectedPoints.Add(vertex);
                            // Don't stop selection mode - allow continuous reselection
                            break;
                            
                        case PivotMoveMethod.BetweenTwoPoints:
                            if (selectedPoints.Count < 2)
                            {
                                selectedPoints.Add(vertex);
                                if (selectedPoints.Count >= 2)
                                    isSelectingVertex = false;
                            }
                            break;
                            
                        case PivotMoveMethod.ToTriangleCenter:
                            if (selectedPoints.Count < 3)
                            {
                                selectedPoints.Add(vertex);
                                if (selectedPoints.Count >= 3)
                                    isSelectingVertex = false;
                            }
                            break;
                    }
                    
                    if (!isSelectingVertex)
                        SceneView.duringSceneGui -= OnSceneGUI;
                        
                    UpdateTargetSettings();
                    UpdatePreview();
                    current.Use();
                }
            }
        }
        
        private void DrawPreviewGizmos()
        {
            if (sourceObject != null && sourceObject.transform != null)
            {
                // Draw current pivot
                Handles.color = CurrentPivotColor;
                float size = HandleUtility.GetHandleSize(sourceObject.transform.position) * PIVOT_HANDLE_SIZE;
                Handles.SphereHandleCap(0, sourceObject.transform.position, Quaternion.identity, size, EventType.Repaint);
                Handles.Label(sourceObject.transform.position + Vector3.up * size * 2, "Current Pivot");
            }
            
            // Draw preview position
            Vector3? newPos = CalculateNewPosition();
            if (newPos.HasValue && sourceObject != null && sourceObject.transform != null)
            {
                Handles.color = PreviewPivotColor;
                float size = HandleUtility.GetHandleSize(newPos.Value) * PIVOT_HANDLE_SIZE;
                Handles.SphereHandleCap(0, newPos.Value, Quaternion.identity, size, EventType.Repaint);
                
                // Draw line between current and new position
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                Handles.DrawDottedLine(sourceObject.transform.position, newPos.Value, 5f);
            }
            
            // Draw selected vertices with matching colors
            if (selectedPoints != null)
            {
                for (int i = 0; i < selectedPoints.Count; i++)
                {
                    Handles.color = i < PointColors.Length ? PointColors[i] : Color.yellow;
                    float size = HandleUtility.GetHandleSize(selectedPoints[i]) * VERTEX_HANDLE_SIZE;
                    Handles.SphereHandleCap(0, selectedPoints[i], Quaternion.identity, size, EventType.Repaint);
                    Handles.Label(selectedPoints[i] + Vector3.up * size * 2, $"Point {i + 1}");
                }
            }
            
            // Draw connections between points
            if (selectedPoints.Count >= 2)
            {
                Handles.color = new Color(0.5f, 0.7f, 1f, 0.5f); // Light blue for connections
                for (int i = 0; i < selectedPoints.Count - 1; i++)
                {
                    Handles.DrawLine(selectedPoints[i], selectedPoints[i + 1]);
                }
                
                if (currentMethod == PivotMoveMethod.ToTriangleCenter && selectedPoints.Count >= 3)
                {
                    Handles.DrawLine(selectedPoints[2], selectedPoints[0]);
                }
            }
        }
        
        private void ReselectPoint(int index)
        {
            if (index >= 0 && index < selectedPoints.Count)
            {
                selectedPoints.RemoveAt(index);
                isSelectingVertex = true;
                UpdateTargetSettings();
                UpdatePreview();
            }
        }
        
        private string FormatVector(Vector3 v)
        {
            return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        }
        
        private void AdjustMinimumSize()
        {
            // Base height for header, dropdown, and buttons
            int baseHeight = 250;
            
            // Add extra height based on method
            int extraHeight = 0;
            switch (currentMethod)
            {
                case PivotMoveMethod.ToAnotherObject:
                    extraHeight = 50; // For target object and options
                    break;
                case PivotMoveMethod.ToVertex:
                    extraHeight = selectedPoints.Count > 0 ? 80 : 60;
                    break;
                case PivotMoveMethod.BetweenTwoPoints:
                    extraHeight = 120; // Space for 2 points
                    break;
                case PivotMoveMethod.ToTriangleCenter:
                    extraHeight = 180; // Space for 3 points
                    break;
                case PivotMoveMethod.ToMeshCenter:
                    extraHeight = 80;
                    break;
            }
            
            // Try to get the window and set its minSize
            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in windows)
            {
                if (window is MovePivotWindow)
                {
                    window.minSize = new Vector2(400, baseHeight + extraHeight);
                    break;
                }
            }
        }
        
        private void SetButtonColor(Button button, bool enabled, Color enabledColor)
        {
            if (button == null || button.style == null) return;
            
            if (enabled)
            {
                button.style.backgroundColor = enabledColor;
                button.style.color = Color.white;
            }
            else
            {
                button.style.backgroundColor = StyleKeyword.Null;
                button.style.color = StyleKeyword.Null;
            }
        }
        
        // Helper to check if running in window mode
        private static bool IsWindowMode()
        {
            // Check if any MovePivotWindow is open
            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<MovePivotWindow>();
            return windows != null && windows.Length > 0;
        }
    }
}