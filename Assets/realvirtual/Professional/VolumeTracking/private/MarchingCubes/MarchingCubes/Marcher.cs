// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

#region

using UnityEngine;

#endregion

namespace realvirtual
{
    public class Marcher : MonoBehaviour
    {
        private MarchingCubes marching;
        private MeshBufferNormals normals;
        public int count = 100000;
        public bool allocateNormals;


        public bool allocate;
        public bool release;


        public void MarchAppend(Texture3D densities, float target, int border, int reduction)
        {
            marching.March(densities, target, border, reduction, Vector3.zero, false);
        }

        public void March(Texture3D densities, float target, int border, int reduction)
        {
            marching.March(densities, target, border, reduction, Vector3.zero);
        }

        public void RecomputeNormals(Texture3D densities, float bias, int border)
        {
            if (normals == null) normals = new MeshBufferNormals(count);

            normals.ComputeNormals(densities, marching.GetMeshBuffer(), marching.GetArgBuffer(), bias, border);
        }

        public void MarchAndComputeNormals(Texture3D densities, float target, float bias, int border, int reduction)
        {
            March(densities, target, border, reduction);
            RecomputeNormals(densities, bias, border);
        }


        public void CopyTriangleCountToBuffer(ComputeBuffer destination, int index)
        {
            marching.CopyTriangleCountToBuffer(destination, index);
        }


        private void Update()
        {
            if (allocate)
            {
                allocate = false;
                Allocate();
            }

            if (release)
            {
                release = false;
                Release();
            }
        }

        public bool CheckAllocate()
        {
            if (marching == null)
            {
                Allocate();
                return true;
            }

            return false;
        }

        private void Allocate()
        {
            Release();
            marching = new MarchingCubes(count, Vector3.one);
            if (allocateNormals) normals = new MeshBufferNormals(count);

            Debug.Log("[Marcher] Allocated");
        }

        public void Release()
        {
            if (marching != null)
            {
                marching.Release();
                marching = null;
            }

            if (normals != null)
            {
                normals.Release();
                normals = null;
            }

            Debug.Log("[Marcher] Released");
        }
    }
}