// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace realvirtual
{
    public class NetworkMU : NetworkBehaviour
    {
        public string scenePath;
        public string sourceScenePath;
        
        public NetworkVariable<FixedString512Bytes> netScenePath = new NetworkVariable<FixedString512Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        public NetworkVariable<FixedString512Bytes> netSourceScenePath = new NetworkVariable<FixedString512Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        public NetworkVariable<int> netLocalID = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        public NetworkVariable<int> netGlobalId = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        public NetworkVariable<FixedString64Bytes> netName = new NetworkVariable<FixedString64Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        private NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        private NetworkVariable<Quaternion> netRotation = new NetworkVariable<Quaternion>(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        
        public MU mu;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsHost)
            {
                this.NetworkObject.TrySetParent(NetworkInitializer.Container);
                InitMUByScenePath(scenePath);
                netScenePath.Value = scenePath;
                netSourceScenePath.Value = sourceScenePath;
            }
            else
            {
                scenePath = netScenePath.Value.ToString();
                sourceScenePath = netSourceScenePath.Value.ToString();
                InitMUByScenePath(scenePath);
            }
        }
        
        public void InitMUByScenePath(string path)
        {
            scenePath = path;
            
            GameObject muObject = RelayConnectionManager.FindGameObjectByPath(scenePath);
            
            if (muObject == null)
            {
                
                // create the missing mu via source
                
                GameObject sourceObject = RelayConnectionManager.FindGameObjectByPath(sourceScenePath);
                if (sourceObject == null)
                {
                    Debug.LogError("Source not found: " + sourceScenePath);
                }
                
                Source source = sourceObject.GetComponent<Source>();
                muObject = source.Generate().gameObject;
                
                string[] pathParts = scenePath.Split('/');
                
                muObject.name = pathParts[pathParts.Length - 1];
            }
            
            mu = muObject.GetComponent<MU>();

            if (IsOwner)
            {
                //mu.Rigidbody.isKinematic = false;
                netLocalID.Value = mu.ID;
                netGlobalId.Value = mu.GlobalID;
                netName.Value = mu.Name;
                netPosition.Value = mu.transform.position;
                netRotation.Value = mu.transform.rotation;
                
                mu.EventMUDeleted.AddListener((MU deletedMU) =>
                {
                    this.NetworkObject.Despawn(true);
                });
                
                
            }else
            {
                mu.Rigidbody.isKinematic = true;
                mu.ID = netLocalID.Value;
                mu.GlobalID = netGlobalId.Value;
                mu.Name = netName.Value.ToString();
                mu.transform.position = netPosition.Value;
                mu.transform.rotation = netRotation.Value;

                DeactivateColliders();

            }
        }
        
        private void DeactivateColliders()
        {
            Collider[] colliders = mu.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            if(!IsOwner)
            {
                Destroy(mu.gameObject);
            }
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {
                netLocalID.Value = mu.ID;
                netGlobalId.Value = mu.GlobalID;
                netName.Value = mu.Name;
                netPosition.Value = mu.transform.position;
                netRotation.Value = mu.transform.rotation;
            }
            else
            {
                mu.ID = netLocalID.Value;
                mu.GlobalID = netGlobalId.Value;
                mu.Name = netName.Value.ToString();
                mu.transform.position = netPosition.Value;
                mu.transform.rotation = netRotation.Value;
                
            }
            
        }
    }
}