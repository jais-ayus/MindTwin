// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class NetworkSource : NetworkBehaviour
    {
        public string scenePath;
        public GameObject networkMUPrefab;

        public NetworkVariable<FixedString512Bytes> netScenePath = new NetworkVariable<FixedString512Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        // Reference to the Source component (your custom component)
        public Source source;
        
        public override void OnNetworkSpawn()
        {
            if (IsHost)
            {
                this.NetworkObject.TrySetParent(NetworkInitializer.Container);
                InitSourceByScenePath(scenePath);
                netScenePath.Value = scenePath;
            }
            else
            {
                scenePath = netScenePath.Value.ToString();
                InitSourceByScenePath(scenePath);
            }
        }
        
        public void InitSourceByScenePath(string path)
        {
            scenePath = path;
            
            GameObject sourceObject = RelayConnectionManager.FindGameObjectByPath(scenePath);
            if (sourceObject == null)
            {
                Debug.LogError("Source not found: " + scenePath);
                return;
            }
            
            source = sourceObject.GetComponent<Source>();

            if (IsHost)
            {
                source.PositionOverwrite = false;
                source.EventMUCreated.AddListener(SpawnNetworkMU);
            }
            else
            {
                //source.DeleteAllImmediate();
                source.PositionOverwrite = true;
                source.LimitNumber = false;
                source.Enabled = true;
            }
        }

        

        public void SpawnNetworkMU(MU mu)
        {
            GameObject go = mu.gameObject;
            GameObject instance = Instantiate(networkMUPrefab, go.transform.position, go.transform.rotation);
            NetworkObject networkObject = instance.GetComponent<NetworkObject>();
            NetworkMU networkMU = instance.GetComponent<NetworkMU>();
            networkMU.scenePath = RelayConnectionManager.GetFullPath(go);
            networkMU.sourceScenePath = scenePath; 
            networkObject.Spawn();
        }
    }
    
    
}
