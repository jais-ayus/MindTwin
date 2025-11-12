// // realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// // (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
#if UNITY_EDITOR
    [CustomEditor(typeof(StatStates))]
    public class StatisticStatesEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            StatStates tracker = (StatStates)target;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Utilization (%)", EditorStyles.boldLabel);

            // Reserve space for the utilization bar
            Rect utilizationBarRect = GUILayoutUtility.GetRect(200, EditorGUIUtility.singleLineHeight);
            float utilizationPercentage = tracker.GetUtilizationPercent();
            float utilizationWidth = utilizationBarRect.width * Mathf.Clamp01(utilizationPercentage / 100f);

            // Background (light gray)
            EditorGUI.DrawRect(utilizationBarRect, new Color(0.8f, 0.8f, 0.8f, 0f));

            // Fill (blue)
            Rect utilizationFillRect = new Rect(utilizationBarRect.x, utilizationBarRect.y, utilizationWidth, utilizationBarRect.height);
            EditorGUI.DrawRect(utilizationFillRect, new Color(0.0f, 1.0f, 0.0f, 1f));

            // Percentage text over the bar
            EditorGUI.LabelField(utilizationBarRect, $"{utilizationPercentage:F1}%", EditorStyles.whiteLabel);

            // Table Header
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Duration (s)", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("Percentage (%)", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Table Rows
            for (int i = 0; i < tracker.TrackedStates.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(tracker.TrackedStates[i], GUILayout.Width(120));
                EditorGUILayout.LabelField(tracker.TrackedDurations[i].ToString("F2"), GUILayout.Width(100));

                // Reserve space for the bar
                Rect barRect = GUILayoutUtility.GetRect(200, EditorGUIUtility.singleLineHeight);
                float percentage = tracker.TrackedPercentages[i];
                float width = barRect.width * Mathf.Clamp01(percentage / 100f);

                // Background (light gray)
                EditorGUI.DrawRect(barRect, new Color(0.8f, 0.8f, 0.8f, 0f));

                // Fill (blue)
                Rect fillRect = new Rect(barRect.x, barRect.y, width, barRect.height);
                EditorGUI.DrawRect(fillRect, new Color(0.2f, 0.5f, 1f, 1f));

                // Percentage text over the bar
                EditorGUI.LabelField(barRect, $"{percentage:F1}%", EditorStyles.whiteLabel);

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(20);

            DrawDefaultInspector();
        }
    }
#endif
}