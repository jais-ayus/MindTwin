// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

#region

using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace realvirtual
{
    public class MarchingCubes
    {
        private static ComputeBuffer m_cubeEdgeFlags;
        private static ComputeBuffer m_triangleConnectionTable;
        private static ComputeShader compute;

        private ComputeBuffer meshBuffer;
        private ComputeBuffer argBuffer;

        private int[] args;
        private int vertexCount = -1;

        private readonly Vector3 voxelSize;


        public MarchingCubes(int maxTriangleCount, Vector3 voxelSize)
        {
            this.voxelSize = voxelSize;
            CheckLoad();
            CreateBuffers(maxTriangleCount);
            var mb = maxTriangleCount * 3 * 3 * sizeof(float) / 1000000.0f;
            Debug.Log("[Marching Cubes] Initialied with " + mb + "MB");
        }


        public void MarchOffsetRange(Texture3D densities, float target, Vector3Int offset, Vector3Int range,
            int reduction, Vector3 meshOffset, bool clear = true, bool copyCount = true)
        {
            CheckLoad();

            if (clear) ClearMeshBuffer();


            var reductionFactor = (int)Mathf.Pow(2, reduction);


            var res = new Vector3Int(densities.width, densities.height, densities.depth);
            //Debug.Log("[MarchingCubes] Data Resolution " + res);


            compute.SetTexture(0, "_Densities", densities);
            compute.SetBuffer(0, "_Buffer", meshBuffer);
            compute.SetBuffer(0, "_CubeEdgeFlags", m_cubeEdgeFlags);
            compute.SetBuffer(0, "_TriangleConnectionTable", m_triangleConnectionTable);
            compute.SetFloat("_Target", target);
            compute.SetInts("_Resolution", res.x, res.y, res.z);
            compute.SetInts("_Offset", offset.x, offset.y, offset.z);
            compute.SetInts("_Range", range.x, range.y, range.z);
            compute.SetVector("_VoxelSize", voxelSize);
            compute.SetVector("_MeshOffset", meshOffset);
            compute.SetInt("_ReductionFactor", reductionFactor);

            compute.Dispatch(0, range.x / 8 / reductionFactor + 1, range.y / 8 / reductionFactor + 1,
                range.z / 8 / reductionFactor + 1);

            if (copyCount) CopyCountToArgs();

            vertexCount = -1;
            args = null;
        }


        public void March(Texture3D densities, float target, int border, int reduction, Vector3 meshOffset,
            bool clear = true, bool copyCount = true)
        {
            var res = new Vector3Int(densities.width, densities.height, densities.depth);

            var offset = Vector3Int.one * border;
            var range = res - Vector3Int.one * (2 * border + 1);

            MarchOffsetRange(densities, target, offset, range, reduction, meshOffset, clear, copyCount);
        }


        public Mesh ToMesh(bool invert = false)
        {
            var n = GetVertexCount();

            var mesh = new Mesh();
            mesh.name = "MarchedMesh";

            if (n >= 65535) mesh.indexFormat = IndexFormat.UInt32;

            var vertices = new Vector3[n];
            meshBuffer.GetData(vertices);

            var triangles = new int[n];
            if (invert)
                for (var i = 0; i < n; i++)
                    triangles[i] = n - i - 1;
            else
                for (var i = 0; i < n; i++)
                    triangles[i] = i;


            mesh.vertices = vertices;
            mesh.triangles = triangles;

            return mesh;
        }


        public ComputeBuffer GetMeshBuffer()
        {
            return meshBuffer;
        }

        public ComputeBuffer GetArgBuffer()
        {
            return argBuffer;
        }

        public int GetVertexCount()
        {
            if (vertexCount == -1)
            {
                var args = GetArgs();
                vertexCount = args[0];
            }

            return vertexCount;
        }


        private void Clear()
        {
            ClearMeshBuffer();
        }


        public void LogArgs()
        {
            var args = GetArgs();
            argBuffer.GetData(args);
            Debug.Log(args[0] + " " + args[1] + " " + args[2] + " " + args[3] + " " + args[4]);
        }


        public int[] GetArgs()
        {
            if (args == null)
            {
                args = new int[5];
                Debug.Log("[MarchingCubes] Downloading ArgBuffer");
                argBuffer.GetData(args);
            }

            return args;
        }


        public void ClearMeshBuffer()
        {
            meshBuffer.SetCounterValue(0);
            CopyCountToArgs();
        }


        private void CreateBuffers(int maxTriangleCount)
        {
            if (meshBuffer != null)
            {
                meshBuffer.Release();
                meshBuffer.Dispose();

                argBuffer.Release();
                argBuffer.Dispose();
            }


            meshBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);

            argBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            var args = new uint[5] { 0, 1, 0, 0, 0 };
            argBuffer.SetData(args);

            Debug.Log("[MarchingCubes] Created buffer " + maxTriangleCount * 4 * 3 * 3 + " bytes, " +
                      maxTriangleCount + " triangles");
        }

        public void CopyTriangleCountToBuffer(ComputeBuffer destination, int offset)
        {
            ComputeBuffer.CopyCount(meshBuffer, destination, offset * sizeof(int));
        }

        public void CopyCountToArgs()
        {
            ComputeBuffer.CopyCount(meshBuffer, argBuffer, 4 * sizeof(int));
            compute.SetBuffer(1, "_ArgBuffer", argBuffer);
            compute.Dispatch(1, 1, 1, 1); // count is multiplied by 3 (triangle -> vertices)
        }

        private void CheckLoad()
        {
            if (m_cubeEdgeFlags == null)
            {
                m_cubeEdgeFlags = new ComputeBuffer(256, sizeof(int));
                m_cubeEdgeFlags.SetData(MarchingCubesTables.CubeEdgeFlags);
                m_triangleConnectionTable = new ComputeBuffer(256 * 16, sizeof(int));
                m_triangleConnectionTable.SetData(MarchingCubesTables.TriangleConnectionTable);
            }

            if (compute == null) compute = (ComputeShader)UnityEngine.Resources.Load("Compute/MarchingCubesAppend");
        }

        public void Release()
        {
            if (meshBuffer != null)
            {
                meshBuffer.Release();
                meshBuffer.Dispose();
                meshBuffer = null;
            }

            if (argBuffer != null)
            {
                argBuffer.Release();
                argBuffer.Dispose();
                argBuffer = null;
            }
        }

        public void ReleaseAll()
        {
            if (meshBuffer != null)
            {
                meshBuffer.Release();
                meshBuffer.Dispose();
                meshBuffer = null;
            }

            if (argBuffer != null)
            {
                argBuffer.Release();
                argBuffer.Dispose();
                argBuffer = null;
            }

            if (m_cubeEdgeFlags != null)
            {
                m_cubeEdgeFlags.Release();
                m_cubeEdgeFlags.Dispose();
                m_triangleConnectionTable.Release();
                m_triangleConnectionTable.Dispose();
            }
        }
    }
}