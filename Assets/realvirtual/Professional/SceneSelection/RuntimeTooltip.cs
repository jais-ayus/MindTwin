// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    //! Provides runtime tooltip functionality for 3D objects with automatic collider generation
    public class RuntimeTooltip : MonoBehaviour
    {
        [HideInInspector] 
        [Tooltip("The tooltip text to display when hovering over this object")]
        public string text;

        [HideInInspector] public Collider[] colliders;

        private void OnEnable()
        {
            CreateColliders();
        }
        
        private void OnDisable()
        {
            DeleteColliders();
        }

        //! Sets the tooltip content from an array of text lines
        public void SetContent(string[] lines)
        {
            string newText = "";
            
            for (int i = 0; i < lines.Length; i++)
            {
                
                if (i == lines.Length - 1)
                {
                    newText += lines[i];
                }
                else
                {
                    newText += lines[i] + "\n";
                }
            }
            SetText(newText);
        }

        private void CreateColliders()
        {
            MeshRenderer[] renderers = gameObject.GetComponentsInChildren<MeshRenderer>();
            colliders = new Collider[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                GameObject colliderObj = new GameObject("Collider");
                colliderObj.layer = LayerMask.NameToLayer("rvSelection");
                colliderObj.transform.SetParent(renderers[i].transform);
                colliderObj.transform.localPosition = Vector3.zero;
                colliderObj.transform.localRotation = Quaternion.identity;
                colliderObj.transform.localScale = Vector3.one;
                MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = renderers[i].GetComponent<MeshFilter>().sharedMesh;
                
                Rigidbody rigidbody = colliderObj.GetComponentInParent<Rigidbody>();
                if (rigidbody != null)
                {
                    meshCollider.convex = true;
                    meshCollider.isTrigger = true;
                }
                else
                {
                    meshCollider.isTrigger = false;
                    meshCollider.convex = false;
                }
                
                
                colliders[i] = meshCollider;
                
            }
            
        }

        private void DeleteColliders()
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    Destroy(colliders[i].gameObject);
            }
            
        }
    
        //! Sets the tooltip text
        public void SetText(string newText)
        {
            text = newText;
        }

        //! Adds a new line to the existing tooltip text
        public void AddLine(string line)
        {
            text = text + "\n" + line;
            
        }
    }

#if UNITY_EDITOR

    [CustomEditor(typeof(RuntimeTooltip))]
    public class RuntimeTooltipEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            RuntimeTooltip tooltip = (RuntimeTooltip)target;
                
            // vertical layout
            EditorGUILayout.BeginVertical("box");
            //label
            EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
            tooltip.text = EditorGUILayout.TextArea(tooltip.text, GUI.skin.textArea);
            EditorGUILayout.Space(5);
            // end vertical
            EditorGUILayout.EndVertical();
                
        }
    }

#endif
}
