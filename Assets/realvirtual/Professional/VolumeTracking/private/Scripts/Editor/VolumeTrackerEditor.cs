// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEditor;
using UnityEngine;

namespace realvirtual.VolumeTracking
{
#if UNITY_EDITOR
    [CustomEditor(typeof(VolumeTracker))]
    public class VolumeTrackerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VolumeTracker tracker = (VolumeTracker)target;

            serializedObject.Update();

            EditorGUILayout.BeginVertical("box");

            // tracker
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Tracking Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                tracker.settings.size = new Vector3(tracker.settings.size.x, tracker.settings.size.y,
                    tracker.settings.size.x);
                tracker.transform.localScale = tracker.settings.size;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Tracking Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Targets", tracker.targets.Length.ToString());
            EditorGUILayout.LabelField("Tracked Cells", tracker.trackedCellCount.ToString());
            EditorGUILayout.LabelField("Visualizers", tracker.visualizers.Count.ToString());

            if (Application.isPlaying)
            {
                if (tracker.IsTracking())
                {
                    if (GUILayout.Button("Stop Tracking"))
                    {
                        tracker.StopTracking();
                    }
                }
                else
                {
                    if (GUILayout.Button("Start Tracking"))
                    {
                        tracker.StartTracking();
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Init Targets"))
                {
                    tracker.visualizerMode = DistanceVisualizer.Mode.Original;
                    tracker.ApplyVisualizerMode();
                    tracker.RefreshTracking();
                    serializedObject.ApplyModifiedProperties();

                    EditorUtility.SetDirty(tracker);
                }
            }


            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");


            // sdf
            EditorGUILayout.LabelField("Distance Map", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("minOccupationTime"));
            if (GUILayout.Button("Recompute Distance Map"))
            {
                tracker.RecomputeDistanceMap();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("visualizerMode"));
            if (tracker.visualizerMode == DistanceVisualizer.Mode.Segments ||
                tracker.visualizerMode == DistanceVisualizer.Mode.Cutout)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("segment1"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("segment2"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("segment3"));
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                tracker.ApplyVisualizerMode();
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("targetGizmos"));

            if (GUILayout.Button("Refresh Visuals"))
            {
                tracker.visualizerMode = DistanceVisualizer.Mode.Original;
                tracker.ApplyVisualizerMode();
                tracker.RefreshTrackingVisualizers();
            }

            EditorGUILayout.EndVertical();


            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceLevel"));

            if (GUILayout.Button("Create Surface"))
            {
                serializedObject.ApplyModifiedProperties();
                IsoSurface.CreateMesh(tracker, tracker.surfaceLevel);
            }


            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
    #endif
}