// // realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// // (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    public class StatDisplay : MonoBehaviour
    {
        public List<MonoBehaviour> StatDisplays;
        public TextMeshPro TextMeshPro;
        public GameObject Background;

        private Camera MainCamera;

        [Tooltip("Extra size around text bounds in world units")]
        public Vector2 Padding = new Vector2(0.05f, 0.03f);  // Adjust as needed

        private void Start()
        {
            MainCamera = Camera.main;
        }

        private void Update()
        {
            if (TextMeshPro != null && MainCamera != null)
            {
                Vector3 directionToCamera = MainCamera.transform.position - TextMeshPro.transform.position;
                directionToCamera.y = 0; // Optional: Keep the text upright by ignoring vertical rotation
                TextMeshPro.transform.rotation = Quaternion.LookRotation(-directionToCamera.normalized, Vector3.up);
            }

            UpdateStatDisplay();
            UpdateBackgroundSize();
        }

        private void UpdateStatDisplay()
        {
            if (TextMeshPro == null) return;

            StringBuilder textBuilder = new StringBuilder();
            foreach (var statDisplay in StatDisplays)
            {
                if (statDisplay == null) continue;
                
                // get the IStatDisplay interface
                var display = statDisplay as IStatDisplay;
                if (display == null) 
                {
                    Debug.LogWarning($"Component {statDisplay.GetType().Name} on {statDisplay.gameObject.name} does not implement IStatDisplay interface and will be ignored.", statDisplay);
                    continue;
                }
                
                // get the display text
                string displayText = display.GetDisplay();
                if (!string.IsNullOrEmpty(displayText))
                {
                    textBuilder.AppendLine(displayText);
                }
            }

            TextMeshPro.text = textBuilder.ToString();
        }

        private void UpdateBackgroundSize()
        {
            if (TextMeshPro == null || Background == null) return;

            // Get the bounds of the rendered text
            var bounds = TextMeshPro.textBounds;

            // Adjust scale of the background (assuming a unit Quad with scale (1,1,1) fits 1x1 world units)
            Background.transform.localScale = new Vector3(
                bounds.size.x + Padding.x,
                bounds.size.y + Padding.y,
                1f
            );

            // Re-center background behind the text (assumes Quad's pivot is centered)
            Background.transform.position = TextMeshPro.transform.position;
            Background.transform.localPosition -= new Vector3(0, 0, -0.001f); // Slight offset to render behind text
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (StatDisplays == null) return;
            
            for (int i = StatDisplays.Count - 1; i >= 0; i--)
            {
                var component = StatDisplays[i];
                if (component != null && !(component is IStatDisplay))
                {
                    Debug.LogWarning($"Component {component.GetType().Name} does not implement IStatDisplay interface and was removed from StatDisplays list.", this);
                    StatDisplays.RemoveAt(i);
                }
            }
        }
#endif
    }
}