// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace realvirtual
{
    //! Controls the display of tooltips when hovering over objects with RuntimeTooltip components
    public class TooltipController : MonoBehaviour
    {
        [Tooltip("The tooltip UI GameObject to show/hide")]
        public GameObject tooltip;
        
        private RuntimeTooltip currentTooltipHit;

        private void Update()
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                currentTooltipHit = null;
                tooltip.SetActive(false);
                return;
            }
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("rvSelection")))
            {
                RuntimeTooltip tooltipHit = hit.collider.GetComponentInParent<RuntimeTooltip>();

                if (tooltipHit != null)
                { 
                    if (currentTooltipHit != tooltipHit)
                    {
                        tooltip.SetActive(true); 
                        tooltip.transform.position = Input.mousePosition; 
                        tooltip.GetComponentInChildren<TMPro.TMP_Text>(true).SetText(tooltipHit.text); 
                        tooltip.GetComponentInChildren<ContentSizeFitter>(true).SetLayoutHorizontal(); 
                        tooltip.GetComponentInChildren<ContentSizeFitter>(true).SetLayoutVertical();
                        currentTooltipHit = tooltipHit;
                    }
                }
                else
                {
                    currentTooltipHit = null;
                    tooltip.SetActive(false);
                }
            }
            else
            {
                currentTooltipHit = null;
                tooltip.SetActive(false);
            }
        }

        void OnDisable()
        {
            tooltip.SetActive(false);
            currentTooltipHit = null;
        }
    }
}
