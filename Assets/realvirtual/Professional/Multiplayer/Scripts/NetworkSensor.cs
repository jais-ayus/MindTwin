// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class NetworkSensor : NetworkBehaviour
    {
        public string scenePath;
        public Sensor sensor;
        
        public NetworkVariable<FixedString512Bytes> netScenePath = new NetworkVariable<FixedString512Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public NetworkVariable<bool> netSensorValue = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            if (IsHost)
            {
                this.NetworkObject.TrySetParent(NetworkInitializer.Container);
                InitSensorByScenePath(scenePath);
                netScenePath.Value = scenePath;
            }
            else
            {
                scenePath = netScenePath.Value.ToString();
                InitSensorByScenePath(scenePath);
            }

            
        }
        
        private void InitSensorByScenePath(string path)
        {
            scenePath = path;
            
            GameObject sensorObject = RelayConnectionManager.FindGameObjectByPath(scenePath);
            if (sensorObject == null)
            {
                Debug.LogError("Signal not found: " + scenePath);
                return;
            }
            sensor = sensorObject.GetComponent<Sensor>();

            if (!IsOwner)
            {
                sensor.SetNetworkControlled();
            }
            
            
            
            
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {
                // Only update the network variable if the value has changed.
                bool currentValue = sensor.Occupied;
                netSensorValue.Value = currentValue;
            }
            else
            {

                if (sensor.Occupied != netSensorValue.Value)
                {
                    sensor.SetOccupied(netSensorValue.Value);
                }
                    
                
            }
        }
    }
}
