// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

namespace realvirtual
{

    using UnityEngine;

    public class MeshBufferMaterials : MonoBehaviour
    {
        
        public static Shader GetShader(string id)
        {
            if (id == "occlusion")
            {
                return (Shader)UnityEngine.Resources.Load("Shader/MeshBufferAmbientOcclusion");
            }



            return (Shader)UnityEngine.Resources.Load("Shader/MeshBufferDefault");
        }
    }
}
