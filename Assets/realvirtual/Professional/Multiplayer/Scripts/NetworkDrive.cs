// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class NetworkDrive : NetworkBehaviour
    {
        public string scenePath;
        public Drive drive;
        private float lastDrivePos;
        private float lastDriveSpeed;
        
        public NetworkVariable<FixedString512Bytes> netScenePath = new NetworkVariable<FixedString512Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> netPosition = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<float> netSpeed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            if (IsHost)
            {
                this.NetworkObject.TrySetParent(NetworkInitializer.Container);
                InitDriveByScenePath(scenePath);
                netScenePath.Value = scenePath;
            }
            else
            {
                scenePath = netScenePath.Value.ToString();
                InitDriveByScenePath(scenePath);
            }
            
        }
        
        public void InitDriveByScenePath(string path)
        {
            scenePath = path;
            
            GameObject driveObject = RelayConnectionManager.FindGameObjectByPath(scenePath);
            if (driveObject == null)
            {
                Debug.LogError("Drive not found: " + scenePath);
                return;
            }
            drive = driveObject.GetComponent<Drive>();
            

            if (IsOwner)
            {
                // The owner controls the drive directly.
                //drive.PositionOverwrite = false;
                //drive.SpeedOverwrite = false;
            }
            else
            {
                    drive.PositionOverwrite = true;
                    drive.SpeedOverwrite = true;
            }
        }
        
        
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                if (drive.CurrentPosition != lastDrivePos || drive.CurrentSpeed != lastDriveSpeed)
                {
                    netPosition.Value = drive.CurrentPosition;
                    netSpeed.Value = drive.CurrentSpeed;
                    lastDrivePos = drive.CurrentPosition;
                    lastDriveSpeed = drive.CurrentSpeed;
                }
            }
            else
            {
                if (drive.PositionOverwrite)
                {
                    drive.SetPositionAndSpeed(netPosition.Value,netSpeed.Value);
                }
            }
        }
    }
}
