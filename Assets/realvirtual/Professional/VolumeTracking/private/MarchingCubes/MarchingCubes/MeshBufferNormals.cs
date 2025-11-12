// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

#region

using UnityEngine;

#endregion

namespace realvirtual
{
    public class MeshBufferNormals
    {
        private static ComputeShader compute;

        private ComputeBuffer normalBuffer;
        private ComputeBuffer argBuffer;

        private int[] args;


        public MeshBufferNormals(int maxTriangleCount)
        {
            CheckLoad();
            CreateBuffers(maxTriangleCount);
            var mb = maxTriangleCount * 3 * 3 * sizeof(float) / 1000000.0f;
            Debug.Log("[MeshBufferNormals] Initialied with " + mb + "MB");
        }


        public void ComputeNormals(Texture3D densities, ComputeBuffer meshBuffer, ComputeBuffer meshArgs, float bias,
            int border)
        {
            CheckLoad();

            ClearBuffer();

            UpdateArgBuffer(meshArgs);


            var res = new Vector3Int(densities.width, densities.height, densities.depth);
            var offset = Vector3Int.one * border;
            var range = res - Vector3Int.one * (2 * border + 1);

            compute.SetTexture(0, "_Densities", densities);
            compute.SetBuffer(0, "_NormalBuffer", normalBuffer);
            compute.SetBuffer(0, "_MeshBuffer", meshBuffer);
            compute.SetBuffer(0, "_MeshArgs", meshArgs);
            compute.SetInts("_Resolution", res.x, res.y, res.z);
            compute.SetInts("_Offset", offset.x, offset.y, offset.z);
            compute.SetInts("_Range", range.x, range.y, range.z);
            compute.SetVector("_VoxelSize", Vector3.one);
            compute.SetFloat("_NormalBias", bias);

            compute.DispatchIndirect(0, argBuffer, 0);
        }

        public ComputeBuffer GetBuffer()
        {
            return normalBuffer;
        }


        private void UpdateArgBuffer(ComputeBuffer meshArgs)
        {
            compute.SetBuffer(1, "_MeshArgs", meshArgs);
            compute.SetBuffer(1, "_Args", argBuffer);
            compute.Dispatch(1, 1, 1, 1);
        }


        private void Clear()
        {
            ClearBuffer();
        }


        private void ClearBuffer()
        {
            normalBuffer.SetCounterValue(0);
        }


        private void CreateBuffers(int maxTriangleCount)
        {
            if (normalBuffer != null)
            {
                normalBuffer.Release();
                normalBuffer.Dispose();

                argBuffer.Release();
                argBuffer.Dispose();
            }


            normalBuffer = new ComputeBuffer(maxTriangleCount * 3, sizeof(float) * 3); //, ComputeBufferType.Append);
            argBuffer = new ComputeBuffer(3, sizeof(int));


            Debug.Log("[MeshBufferNormals] Created buffer " + maxTriangleCount * 3 * 4 * 3 + " bytes, " +
                      maxTriangleCount * 3 + " vertices");
        }


        private void CheckLoad()
        {
            if (compute == null) compute = UnityEngine.Resources.Load<ComputeShader>("Compute/MeshBufferNormals");
        }

        public void Release()
        {
            if (normalBuffer != null)
            {
                normalBuffer.Release();
                normalBuffer.Dispose();
                normalBuffer = null;
            }

            if (argBuffer != null)
            {
                argBuffer.Release();
                argBuffer.Dispose();
                argBuffer = null;
            }
        }
    }
}