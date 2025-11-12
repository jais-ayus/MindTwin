// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;

namespace realvirtual
{
    public class MultiuserUIManager : MonoBehaviour
    {
        public GameObject button;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            button.SetActive(true);
            Transform toolbarRoot = GameObject.Find("realvirtual").transform.Find("UI").Find("MainView").Find("Toolbar")
                .Find("Left");
            button.transform.SetParent(toolbarRoot);
            button.GetComponent<RectTransform>().localScale = Vector3.one;
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
