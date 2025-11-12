// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace realvirtual
{
    //! Manages interactive object selection and highlighting in 3D scenes with multi-layer visual feedback systems.
    //! This professional component orchestrates runtime object selection with hover effects, active highlights,
    //! and change indicators. Provides intuitive interaction for operators and engineers to inspect components,
    //! view properties, and interact with simulation elements during virtual commissioning and training scenarios.
    //! Supports multiple selection states with customizable visual feedback for enhanced user experience.
    public class SceneSelectionManager : MonoBehaviour
    {
        public AbstractSelectionManager active;
        public AbstractSelectionManager hover;
        public AbstractSelectionManager selected;
        public AbstractSelectionManager changed;
        public Sprite icon;
        
        private SceneSelectable selectable;
        public GameObject window;
        public TMPro.TextMeshProUGUI title;
        public GameObject description;
        public GameObject tooltip;

        private void OnValidate()
        {
            realvirtualController controller = FindFirstObjectByType<realvirtualController>();

            if (controller == null)
            {
                return;
            }
            
            
            if(active == null)
            {
                active = controller.gameObject.transform.Find("Highlighter").Find("Highlight 1").GetComponentInChildren<AbstractSelectionManager>();
            }
            
            if(hover == null)
            {
                hover = controller.gameObject.transform.Find("Highlighter").Find("Highlight 2").GetComponentInChildren<AbstractSelectionManager>();
            }
            
            if(selected == null)
            {
                //selected = controller.gameObject.transform.Find("Highlighter").Find("Outline").GetComponentInChildren<AbstractSelectionManager>();
                selected = controller.gameObject.transform.Find("Highlighter").Find("Highlight 4").GetComponentInChildren<AbstractSelectionManager>();
            }
            
            if(changed == null)
            {
                changed = controller.gameObject.transform.Find("Highlighter").Find("Highlight 3").GetComponentInChildren<AbstractSelectionManager>();
            }
            
            rvUIToolbarButton button = GetComponentInChildren<rvUIToolbarButton>(true);
            if (button != null)
            {
                button.transform.Find("ImageOff").GetComponent<Image>().sprite = icon;
                button.transform.Find("ImageOn").GetComponent<Image>().sprite = icon;
                
                
            }
        }


        public void RefreshActives()
        {
            foreach (var sceneSelectable in GetComponentsInChildren<SceneSelectable>())
            {
                sceneSelectable.RefreshActive(this);
            }
        }
        
        public void CloseWindow()
        {
            window.SetActive(false);
            selected.DeselectAll();
        }

        private void Update()
        {
            
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }
            
            // Raycast for hover
            
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            // Use RaycastAll to get all the objects hit by the ray.
            RaycastHit[] hits = Physics.RaycastAll(ray);

            // Flag to track if we found a valid SelectablePart.
            bool foundSelectable = false;
        
            foreach (var hit in hits)
            {
                // Attempt to get the component.
                SelectablePart selectable = hit.collider.GetComponentInChildren<SelectablePart>();
            
                // If a SelectablePart was found, set it and break out if we only care about the first hit.
                if (selectable != null)
                {
                    SetSelectable(selectable);
                    foundSelectable = true;
                    break;
                }
            }

            // If none of the hits had a SelectablePart, pass null.
            if (!foundSelectable)
            {
                if (selectable != null)
                {
                    selectable.UnHover();
                    selectable = null;
                }
            }
           
            
            // Click for select
            
            if (Input.GetMouseButtonDown(0))
            {
                if (selectable != null)
                {
                    selectable.Click();
                }
                else
                {
                    //selected.DeselectAll();
                    //CloseWindow();
                }
            }
            
            
        }
        
        void SetSelectable(SelectablePart part)
        {
            if (selectable == null)
            {
                selectable = part.selectable;
                selectable.Hover();
            }
            else
            {
                if (part.selectable != selectable)
                {
                    selectable.UnHover();
                    selectable = part.selectable;
                    selectable.Hover();
                }
            }
            

        }


        [NaughtyAttributes.Button]
        public void AddSelectable()
        {
            GameObject selectable = new GameObject("Selectable");
            selectable.transform.SetParent(transform);
            selectable.AddComponent<SceneSelectable>();
            
        }

        // uts every cild selectable on active, adds colliders and scrits etc
        public void Activate()
        {
            SceneSelectable[] selectables = GetComponentsInChildren<SceneSelectable>();
            for (int i = 0; i < selectables.Length; i++)
            {
                selectables[i].Activate();
            }
            
        }
        
        public void Deactivate()
        {
            SceneSelectable[] selectables = GetComponentsInChildren<SceneSelectable>();
            for (int i = 0; i < selectables.Length; i++)
            {
                selectables[i].Deactivate();
            }
            
            CloseWindow();
            
        }
    }
}
