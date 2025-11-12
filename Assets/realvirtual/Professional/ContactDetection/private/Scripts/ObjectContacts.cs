// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  


using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;

namespace realvirtual
{
    public class ObjectContacts : MonoBehaviour
    {
        public bool show = true;
        public List<MeshFilter> contacts = new List<MeshFilter>();

        void OnDrawGizmosSelected(){
            if(show){
                Gizmos.color = Color.red;
            
                for (int i = 0; i < contacts.Count; i++)
                {

                    //contacts[i].GetComponent<MeshRenderer>().enabled = false;

                    Mesh mesh = contacts[i].sharedMesh;
                    Transform t = contacts[i].gameObject.transform;

                    Gizmos.DrawWireMesh(mesh, t.position, t.rotation, t.lossyScale);
                
                }

                Gizmos.color = Color.green;    
                Gizmos.DrawWireMesh(GetComponent<MeshFilter>().sharedMesh, transform.position, transform.rotation, transform.lossyScale);
            }
                
        }
    }
}

