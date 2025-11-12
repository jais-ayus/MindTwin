// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    public class MultiuserWindow : MonoBehaviour
    {
        public rvUIInputField inputField;
        public rvUIInputField statusField;
        public rvUIInputField nameField;
        public rvUIToolbarButton toolbarButton;
        public Color colorSuccess;
        public Color colorFailed;
        public Color colorPending;
        
        
        private RelayConnectionManager _relayConnectionManager;
        
        void Awake()
        {
            string playerName = PlayerPrefs.GetString("NetworkPlayerName", "");
            nameField.valueInputField.text = playerName;
            
            
            _relayConnectionManager = GameObject.FindFirstObjectByType<RelayConnectionManager>();
        }
        
        
        public async void StartHost()
        {
            SetImageColor(colorPending);
            
            PlayerPrefs.SetString("NetworkPlayerName", GetPlayerName());
            PlayerPrefs.Save();
            
            string joinCode = await _relayConnectionManager.StartHost();
            bool success = joinCode != null;
            statusField.ChangeValueText(success ? "Host connected" : "Host failed");
            Debug.Log("Multiuser Window: Host started with join code: " + joinCode);
            inputField.ChangeValueText(joinCode);
            
            SetImageColor(success ? colorSuccess : colorFailed);
            
        }
        
        public async void StartClient()
        {
            
            
            SetImageColor(colorPending);
            
            PlayerPrefs.SetString("NetworkPlayerName", GetPlayerName()); 
            PlayerPrefs.Save();
            
            string joinCode = inputField.valueInputField.text;
            joinCode = joinCode.Replace(" ", "").Replace("\n", "");
            Debug.Log("Multiuser Window: Joining with code: " + joinCode + "_");
            _relayConnectionManager.joinCode = joinCode;
            bool success = await _relayConnectionManager.StartClient();
            statusField.ChangeValueText(success ? "Client connected" : "Client failed");
            
            SetImageColor(success ? colorSuccess : colorFailed);
            
        }

        public string GetPlayerName()
        {
            return nameField.valueInputField.text;
        }

        void SetImageColor(Color color)
        {
            Image image = toolbarButton.transform.Find("Image").GetComponent<Image>();
            image.color = color;
        }
    }
}
