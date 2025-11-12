// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace realvirtual
{
    public class SceneSelectable : MonoBehaviour
    {
        public string title;
        public string description;
        [HideInInspector] public string tooltip;
        
        public List<MeshRenderer> renderers;
        public List<Signal> signals;

        private SignalState initialState;
        
        private void Start()
        {
            initialState = new SignalState(signals);
        }
        
        AbstractSelectionManager GetActiveManager()
        {
            SceneSelectionManager manager = GetComponentInParent<SceneSelectionManager>();
            SignalState currentState = new SignalState(signals);
            bool isChanged = !initialState.IsEqual(currentState);
            if(isChanged)
            {
                return manager.changed;
            }
            else
            {
                return manager.active;
            }
        }
        
        public bool HasTooltip()
        {
            return !string.IsNullOrEmpty(tooltip);
        }
        
        public void Activate()
        {
            SceneSelectionManager manager = GetComponentInParent<SceneSelectionManager>();
            
            GameObject go = UnityEngine.Resources.Load<GameObject>("SelectablePart");
            
            AbstractSelectionManager active = GetActiveManager();

            for (int i = 0; i < renderers.Count; i++)
            {
                GameObject part = Instantiate(go, renderers[i].transform);
                part.transform.localPosition = Vector3.zero;
                part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = Vector3.one;
                
                SelectablePart selectablePart = part.GetComponent<SelectablePart>();
                selectablePart.selectable = this;
                selectablePart.Init(renderers[i].GetComponent<MeshFilter>().sharedMesh);
                
                active.Select(renderers[i]);
                
            }
            
            
        }
        
        public void Deactivate()
        {
            SceneSelectionManager manager = GetComponentInParent<SceneSelectionManager>();
            
            for (int i = 0; i < renderers.Count; i++)
            {
                manager.active.Deselect(renderers[i]);
                manager.changed.Deselect(renderers[i]);
                manager.hover.Deselect(renderers[i]);
                manager.selected.Deselect(renderers[i]);
                
                SelectablePart selectablePart = renderers[i].GetComponentInChildren<SelectablePart>();
                DestroyImmediate(selectablePart.gameObject);
            }
        }

        public void Hover()
        {
            SceneSelectionManager manager = GetComponentInParent<SceneSelectionManager>();

            for(int i = 0; i < renderers.Count; i++)
            {
                manager.hover.Select(renderers[i]);
            }

            if (HasTooltip() && !manager.window.activeSelf)
            {
                OpenTooltip(manager);
            }
            
            
        }

        public void UnHover()
        {
            SceneSelectionManager manager = GetComponentInParent<SceneSelectionManager>();

            for(int i = 0; i < renderers.Count; i++)
            {
                manager.hover.Deselect(renderers[i]);
            }
            
            CloseTooltip(manager);
        }

        public void Click()
        {
            SceneSelectionManager manager = GetComponentInParent<SceneSelectionManager>();
            
            manager.selected.DeselectAll();
            
            for(int i = 0; i < renderers.Count; i++)
            {
                
                manager.selected.Select(renderers[i]);
            }

            OpenSignalWindow(manager);
        }
        
        public void RefreshActive(SceneSelectionManager manager)
        {
            AbstractSelectionManager active = GetActiveManager();

            for(int i = 0; i < renderers.Count; i++)
            {
                manager.active.Deselect(renderers[i]);
                manager.changed.Deselect(renderers[i]);
                active.Select(renderers[i]);
              
            }
        }
        
        void OpenSignalWindow(SceneSelectionManager manager)
        {
            // open the signal window
            
            CloseTooltip(manager);

            GameObject window = manager.window;
            
            
            
            // delete old rows
            rvUIToggle[] toggles = window.GetComponentsInChildren<rvUIToggle>();
            for (int i = 0; i < toggles.Length; i++)
            {
                DestroyImmediate(toggles[i].gameObject.transform.parent.gameObject);
            }
            
            window.SetActive(true);
            
            // add new rows
            
            GameObject row = window.GetComponentInChildren<rvUIToggle>(true).gameObject.transform.parent.gameObject;
            for (int i = 0; i < signals.Count; i++)
            {
                if (signals[i] == null)
                {
                    continue;
                }
                

                GameObject newRow = Instantiate(row, row.transform.parent);
                newRow.SetActive(true);
                rvUIToggle toggle = newRow.GetComponentInChildren<rvUIToggle>(true);
                toggle.ToggleTo((bool)signals[i].GetValue());
                toggle.ChangeLabelText(signals[i].name);
                
                Signal signal = signals[i];
                
                toggle.OnToggle.AddListener((bool state) =>
                {
                    signal.SetValue(state);
                    manager.RefreshActives();
                });
            }
            
            manager.title.text = title;

            // check if descrition string is blank
            if (string.IsNullOrEmpty(description))
            {
                manager.description.gameObject.SetActive(false);
            }
            else
            {
                manager.description.gameObject.SetActive(true);
                manager.description.GetComponentInChildren<TMPro.TMP_Text>(true).text = description;
                manager.description.gameObject.SetActive(false);
                StartCoroutine(ActivateDescriptionText(manager));
            }
            
        }

        
        void OpenTooltip(SceneSelectionManager manager)
        {
            manager.tooltip.SetActive(true);
            manager.tooltip.GetComponentInChildren<TMPro.TMP_Text>(true).SetText(tooltip);
            manager.tooltip.GetComponentInChildren<ContentSizeFitter>(true).SetLayoutHorizontal();
            manager.tooltip.GetComponentInChildren<ContentSizeFitter>(true).SetLayoutVertical();
            manager.tooltip.transform.position = Input.mousePosition;
            rvUICopySize copySize = manager.tooltip.GetComponent<rvUICopySize>();
            copySize.CopySize();
            StartCoroutine(OpenTooltipDelayed(manager));
        }

        IEnumerator OpenTooltipDelayed(SceneSelectionManager manager)
        {
            yield return null;
            manager.tooltip.SetActive(true);
            rvUICopySize copySize = manager.tooltip.GetComponent<rvUICopySize>();
            copySize.CopySize();
        }
        
        void CloseTooltip(SceneSelectionManager manager)
        {
            manager.tooltip.SetActive(false);
        }
        
        
        IEnumerator ActivateDescriptionText(SceneSelectionManager manager)                                                   
        {
            yield return new WaitForSeconds(0.1f);
            manager.description.gameObject.SetActive(true);
        }
    }


#if UNITY_EDITOR

    [CustomEditor(typeof(SceneSelectable))]
    public class SceneSelectableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            SceneSelectable selectable = (SceneSelectable)target;
            
            // vertical layout
            EditorGUILayout.BeginVertical("box");
            //label
            EditorGUILayout.LabelField("Tooltip", EditorStyles.boldLabel);
            selectable.tooltip = EditorGUILayout.TextArea(selectable.tooltip, GUI.skin.textArea);
            // end vertical
            EditorGUILayout.EndVertical();
            
        }
    }

#endif

}
