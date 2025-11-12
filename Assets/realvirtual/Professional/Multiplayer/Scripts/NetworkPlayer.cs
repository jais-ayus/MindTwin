// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class NetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<FixedString32Bytes> netName = new NetworkVariable<FixedString32Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        public TMPro.TextMeshProUGUI nameText;
        
        // rightController
        private GameObject rightController;
        private bool rightControllerDetected = false;
        public NetworkVariable<bool> netRightControllerActive = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Vector3> netRightControllerPosition = new NetworkVariable<Vector3>(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Quaternion> netRightControllerRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        // leftController
        private GameObject leftController;
        private bool leftControllerDetected = false;
        public NetworkVariable<bool> netLeftControllerActive = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Vector3> netLeftControllerPosition = new NetworkVariable<Vector3>(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<Quaternion> netLeftControllerRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        
        
        public GameObject rightControllerMirror;
        public GameObject leftControllerMirror;

        private MultiuserWindow multiUserWindow;

        public override void OnNetworkSpawn()
        {
            multiUserWindow = FindFirstObjectByType<MultiuserWindow>(FindObjectsInactive.Include);
            
            if (IsOwner)
            {
                netName.Value = FindPlayerName();
                DeactivateAvatar();
                FindControllers();
            }
           
        }

        protected override void OnNetworkPostSpawn()
        {
            if (!IsOwner)
            {
                SetPlayerName(netName.Value.ToString());
            }
            
        }
        
        void FindControllers()
        {

            Group[] groups = FindObjectsByType<Group>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Group group in groups)
            {
                if (group.GroupName == "RightController")
                {
                    rightController = group.gameObject;
                    rightControllerDetected = true;
                    Debug.Log("Found right controller");
                }
                else if (group.GroupName == "LeftController")
                {
                    leftController = group.gameObject;
                    leftControllerDetected = true;
                    Debug.Log("Found left controller");
                }
            }
            
        }

        void DeactivateAvatar()
        {
            GameObject avatar = transform.Find("Avatar").gameObject;
            if (avatar == null)
            {
                Debug.LogError("Avatar not found.");
                return;
            }
            avatar.SetActive(false);
        }
        
        string FindPlayerName()
        {
            if (multiUserWindow == null)
            {
                Debug.LogError("MultiuserWindow not found.");
                return "";
            }
            
            return multiUserWindow.GetPlayerName();
        }
        
        void SetPlayerName(string name)
        {
            if (multiUserWindow == null)
            {
                Debug.LogError("MultiuserWindow not found.");
                return;
            }

            
            nameText.text = name;

        }
        
        private void FixedUpdate()
        {
            SyncPlayerName();
            
            if (IsOwner)
            {
                SyncControllersOwner();
            }
            else
            {
                SyncControllersClient();
                
            }
            
        }

        
        private void SyncPlayerName()
        {
            if (IsOwner)
            {
                netName.Value = FindPlayerName();
            }
            else
            {
                nameText.text = netName.Value.ToString();
            }
        }
        
        private void SyncControllersClient()
        {
            float lerpFactor = 0.25f;
            
            rightControllerMirror.SetActive(netRightControllerActive.Value);
            if (rightControllerMirror.activeSelf)
            {
                rightControllerMirror.transform.position = LerpPosition(rightControllerMirror.transform.position, netRightControllerPosition.Value, lerpFactor);
                rightControllerMirror.transform.rotation = LerpRotation(rightControllerMirror.transform.rotation, netRightControllerRotation.Value, lerpFactor);
            }
            leftControllerMirror.SetActive(netLeftControllerActive.Value);
            if (leftControllerMirror.activeSelf)
            {
                leftControllerMirror.transform.position = LerpPosition(leftControllerMirror.transform.position, netLeftControllerPosition.Value, lerpFactor);
                leftControllerMirror.transform.rotation = LerpRotation(leftControllerMirror.transform.rotation, netLeftControllerRotation.Value, lerpFactor);
            }
        }
        
        

        private void SyncControllersOwner()
        {
            if(rightControllerDetected)
            {
                netRightControllerActive.Value = rightController.activeInHierarchy;
                netRightControllerPosition.Value = rightController.transform.position;
                netRightControllerRotation.Value = rightController.transform.rotation;
            }
            if(leftControllerDetected)
            {
                netLeftControllerActive.Value = leftController.activeInHierarchy;
                netLeftControllerPosition.Value = leftController.transform.position;
                netLeftControllerRotation.Value = leftController.transform.rotation;
            }
        }
        
        Vector3 LerpPosition(Vector3 start, Vector3 end, float t)
        {
            return Vector3.Lerp(start, end, t);
        }
        
        Quaternion LerpRotation(Quaternion start, Quaternion end, float t)
        {
            return Quaternion.Lerp(start, end, t);
        }
    }
}
