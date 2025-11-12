#if REALVIRTUAL_BURST
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  


using UnityEngine;
using NaughtyAttributes;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    public class ContactDetector : MonoBehaviour
{

    public GameObject root;
    public GameObject target;

    private MeshFilter[] meshFilters;
    
   

    public bool boundsOnly;


   


    [Button("Detect")]
    public void Detect(){

        ComputeContacts();
        
    }

  

    [Button("Clear")]
    public void ClearAll(){
        DeleteAllContacts();
    }


    


    

    Bounds GetAABB(MeshFilter mf){
        Bounds bounds = mf.sharedMesh.bounds;
        Vector3 boundMin = mf.transform.TransformPoint(bounds.min);
        Vector3 boundMax = mf.transform.TransformPoint(bounds.max);
        Bounds worldBounds = new Bounds();
        worldBounds.SetMinMax(boundMin, boundMax);
        return worldBounds;
    }


    private void ComputeContacts(){

        target = gameObject;

        meshFilters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

        MeshFilter targetFilter = target.GetComponent<MeshFilter>();
        ObjectContacts contacts = target.GetComponent<ObjectContacts>();

        if(contacts != null){
            DestroyImmediate(contacts);
        }

        ObjectContacts newContacts = target.AddComponent<ObjectContacts>();
        int count = 0;

        NativeMeshData targetMeshData = new NativeMeshData(targetFilter.sharedMesh, targetFilter.transform);

        Bounds boundsI = targetFilter.GetComponent<MeshRenderer>().bounds;

        for (int j = 0; j < meshFilters.Length; j++)
        {
            if( meshFilters[j] != targetFilter){
                Bounds boundsJ = meshFilters[j].GetComponent<MeshRenderer>().bounds;

                if(boundsI.Intersects(boundsJ) || boundsJ.Intersects(boundsI)){

                            
                    if(!boundsOnly){

                        NativeMeshData otherMeshData = new NativeMeshData(meshFilters[j].sharedMesh, meshFilters[j].transform);

                        //if(ContactDetectionTools.HasContact(targetMeshData, otherMeshData) == false && ContactDetectionTools.HasContact(otherMeshData, targetMeshData) == false){
                        //    otherMeshData.Dispose();
                        //    continue;
                        //}

                        otherMeshData.Dispose();

                        
                                
                    }

                    newContacts.contacts.Add(meshFilters[j]);
                    count += 1;


                }


            }

        }

        targetMeshData.Dispose();

        if(count == 0){
            DestroyImmediate(newContacts);
        }
            
        Debug.Log("[ContactDetector] Detected " + count + " contacts");

        

    }

    void DeleteAllContacts(){
        ObjectContacts contacts = GetComponent<ObjectContacts>();
        if(contacts != null){
            DestroyImmediate(contacts);
        }
       
    }
    
    bool IsContained(Bounds i, Bounds j){
        return i.Contains(j.min) && i.Contains(j.max);
    }

}
}


#endif