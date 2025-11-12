// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class FollowMainCamera : NetworkBehaviour
    {
        
        public Camera mainCamera;

        void Start()
        {
            if (IsOwner)
            {
                mainCamera = Camera.main;
            }
            
        }
        

        // Update is called once per frame
        void FixedUpdate()
        {

            if (IsOwner)
            {
                mainCamera = Camera.main;
                // sets position and rotation of this object to be the same as the main camera
                transform.position = mainCamera.transform.position;
                transform.rotation = mainCamera.transform.rotation;

            }

        }

        private void OnRenderObject()
        {
            if (IsOwner)
            {
                mainCamera = Camera.main;
                // sets position and rotation of this object to be the same as the main camera
                transform.position = mainCamera.transform.position;
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}
