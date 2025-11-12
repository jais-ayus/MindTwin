// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class NetworkSignal : NetworkBehaviour
    {
        
        public string scenePath;
        public Signal signal;
        
        public NetworkVariable<FixedString512Bytes> netScenePath = new NetworkVariable<FixedString512Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public NetworkVariable<FixedString32Bytes> netSignalValue = new NetworkVariable<FixedString32Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public override void OnNetworkSpawn()
        {
            if (IsHost)
            {
                this.NetworkObject.TrySetParent(NetworkInitializer.Container);
                InitSignalByScenePath(scenePath);
                netScenePath.Value = scenePath;
            }
            else
            {
                scenePath = netScenePath.Value.ToString();
                InitSignalByScenePath(scenePath);
            }

            
        }
        
        private void InitSignalByScenePath(string path)
        {
            scenePath = path;
            
            GameObject signalObject = RelayConnectionManager.FindGameObjectByPath(scenePath);
            if (signalObject == null)
            {
                Debug.LogError("Signal not found: " + scenePath);
                return;
            }
            signal = signalObject.GetComponent<Signal>();
            
            
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {
                // Only update the network variable if the value has changed.
                string currentValue = signal.GetValue().ToString();
                netSignalValue.Value = currentValue;
            }
            else
            {
                
                signal.SetValue(netSignalValue.Value.ToString());
                
            }
        }
    }
}