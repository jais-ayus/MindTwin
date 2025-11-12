// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  


using System.Threading.Tasks;
using NaughtyAttributes;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Events;


namespace realvirtual
{

    public class RelayConnectionManager : MonoBehaviour
    {
        public string joinCode;
        public UnityEvent OnConnect;
        
        
        [Button]
        public async Task<string> StartHost()
        {
            return await StartHostWithRelay();
        }

        [Button]
        public async Task<bool> StartClient()
        {
            return await StartClientWithRelay();
        }


        public async Task<string> StartHostWithRelay(int maxConnections = 5)
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            Debug.Log("Host created join code: " + joinCode);
            
            //TODO: select server
            
            string result = NetworkManager.Singleton.StartHost() ? joinCode : null;

            if (result != null)
            {
                OnConnect.Invoke();
            }
            
            
            return result;
            
        }

        public async Task<bool> StartClientWithRelay()
        {
            PrepareSources();
            PrepareSinks();
            
            Debug.Log("Joining with code: " + joinCode + "_");
            
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            
            Debug.Log("Client signed in");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);
            
            Debug.Log("Allocation id: " + joinAllocation.AllocationId);
            
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            
            Debug.Log("Relay server data: " + relayServerData);
            
            NetworkManager.Singleton.GetComponent<UnityTransport>()
                .SetRelayServerData(relayServerData);
            
            Debug.Log("Client joined allocation: " + joinAllocation);
            
            bool result = !string.IsNullOrEmpty(joinCode) && NetworkManager.Singleton.StartClient();
            

            return result;

        }

        public static void PrepareSinks()
        {
            Sink[] sinks = FindObjectsByType<Sink>(FindObjectsSortMode.InstanceID);
            foreach (Sink sink in sinks)
            {
                Collider[] colliders = sink.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    collider.enabled = false;
                }
            }
        }

        public static void PrepareSources()
        {
            Source[] sources = FindObjectsByType<Source>(FindObjectsSortMode.InstanceID);
            foreach (Source source in sources)
            {
                PrepareSource(source);
            }
            
        }
        
        public static void PrepareSource(Source source)
        {
            source.DeleteAllImmediate();
            source.PositionOverwrite = true;
            source.LimitNumber = false;
            source.Enabled = true;
        }

        
        public static GameObject FindGameObjectByPath(string path)
        {
            string[] parts = path.Split('/');
            if (parts.Length == 0) return null;

            GameObject root = GameObject.Find(parts[0]); // Find the root object
            if (root == null) return null;

            Transform current = root.transform;

            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Find(parts[i]); // Find child by name
                if (current == null) return null;
            }

            return current.gameObject;
        }
        
        public static string GetFullPath(GameObject gameObject)
        {
            string path = gameObject.name;
            Transform current = gameObject.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        
    }

}
