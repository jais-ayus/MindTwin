// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using Unity.Netcode;

namespace realvirtual
{
    //! Initializes and manages networked components for collaborative multi-user simulation environments.
    //! This professional networking component automatically spawns network-synchronized versions of automation
    //! components, enabling multiple users to interact with the same virtual commissioning scenario simultaneously.
    //! Essential for remote collaboration, training scenarios, and design reviews where multiple stakeholders
    //! need real-time synchronized views of automation systems, component states, and material flow.
    public class NetworkInitializer : MonoBehaviour
    {
        public static NetworkObject Container;
        
        
        public GameObject networkContainerPrefab;
        public GameObject networkDrivePrefab;
        public GameObject networkSourcePrefab;
        public GameObject networkMUPrefab;
        public GameObject networkSignalPrefab;
        public GameObject networkSensorPrefab;
        
        
        
        public void InitNetworkComponents()
        {
            
            GameObject containerInstance = Instantiate(networkContainerPrefab, Vector3.zero, Quaternion.identity);
            NetworkObject containerNetworkObject = containerInstance.GetComponent<NetworkObject>();
            containerNetworkObject.Spawn();
            Container = containerNetworkObject;
            
            
            // Process Drives
            Drive[] drives = GameObject.FindObjectsByType<Drive>(FindObjectsSortMode.InstanceID);
            foreach (Drive drive in drives)
            {
                GameObject go = drive.gameObject;
                GameObject instance = Instantiate(networkDrivePrefab, go.transform.position, go.transform.rotation);
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                NetworkDrive networkDrive = instance.GetComponent<NetworkDrive>();
                
                networkDrive.scenePath = RelayConnectionManager.GetFullPath(go);
                
                networkObject.Spawn();
            }
            
            // Process Signals
            Signal[] signals = GameObject.FindObjectsByType<Signal>(FindObjectsSortMode.InstanceID);
            foreach (Signal signal in signals)
            {
                GameObject go = signal.gameObject;
                GameObject instance = Instantiate(networkSignalPrefab, go.transform.position, go.transform.rotation);
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                NetworkSignal networkSignal = instance.GetComponent<NetworkSignal>();
                
                networkSignal.scenePath = RelayConnectionManager.GetFullPath(go);
                
                networkObject.Spawn();
            }
                
            
            
            
            // Process Sources
            Source[] sources = GameObject.FindObjectsByType<Source>(FindObjectsSortMode.InstanceID);
            foreach (Source source in sources)
            {
                GameObject go = source.gameObject;
                GameObject instance = Instantiate(networkSourcePrefab, go.transform.position, go.transform.rotation);
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                NetworkSource networkSource = instance.GetComponent<NetworkSource>();
                
                networkSource.scenePath = RelayConnectionManager.GetFullPath(go);
                networkSource.networkMUPrefab = networkMUPrefab;
                
                networkObject.Spawn();
            }
            
            
            // Process MUs
            MU[] mus = GameObject.FindObjectsByType<MU>(FindObjectsSortMode.InstanceID);
            foreach (MU mu in mus)
            {
                Source source = mu.GetComponent<Source>();
                if (source != null)
                {
                    continue;
                }
                GameObject go = mu.gameObject;
                GameObject instance = Instantiate(networkMUPrefab, go.transform.position, go.transform.rotation);
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                NetworkMU networkMU = instance.GetComponent<NetworkMU>();
                networkMU.scenePath = RelayConnectionManager.GetFullPath(go);
                
                if(mu.CreatedBy != null)
                {
                    networkMU.sourceScenePath = RelayConnectionManager.GetFullPath(mu.CreatedBy.gameObject);
                }
                networkObject.Spawn();
            }
            
            // Process Sensors
            Sensor[] sensors = GameObject.FindObjectsByType<Sensor>(FindObjectsSortMode.InstanceID);
            foreach (Sensor sensor in sensors)
            {
                GameObject go = sensor.gameObject;
                GameObject instance = Instantiate(networkSensorPrefab, go.transform.position, go.transform.rotation);
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                NetworkSensor networkSensor = instance.GetComponent<NetworkSensor>();

                networkSensor.scenePath = RelayConnectionManager.GetFullPath(go);

                networkObject.Spawn();

            }


        }
        
        
    }
}