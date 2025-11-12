// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

namespace realvirtual
{
    using UnityEngine;

    public class MeshGPU
    {

        public static Material defaultMaterial;

        public ComputeBuffer meshBuffer;
        public ComputeBuffer argBuffer;
        public RenderTexture densities;

       
        ComputeShader compute;


        /*void CheckLoadCompute(){
            if(compute == null){
                compute = (ComputeShader)Resources.Load("Compute/MeshGPU");
            }
        }

        public MeshGPU(ComputeBuffer meshBuffer, ComputeBuffer argBuffer, RenderTexture densities){
            this.meshBuffer = meshBuffer;
            this.argBuffer = argBuffer;
            this.densities = densities;
        }

        public MeshGPU(RenderTexture densities, int reduction = 0){
            this.densities = densities;
            CreateBuffers(reduction = reduction);
        }

        public void Release(){
            meshBuffer.Release();
            argBuffer.Release();
        }

        public void SetDirty(){
            vertexCount = -1;
        }

        public int GetVertexCount(){
            if(meshBuffer == null){
                vertexCount = -1;
                return vertexCount;
            }
            if(vertexCount == -1){
                vertexCount = GetArgs()[0];
            }
            return vertexCount;
        }

        Vector3Int GetResolution(){
            return new Vector3Int(densities.width, densities.height, densities.volumeDepth);
        }


        void CheckCreateBuffers(int reduction){
            if(meshBuffer == null){
                CreateBuffers(reduction);
            }
        }




        void CreateBuffers(int reduction){

            if(meshBuffer != null){
                meshBuffer.Release();
                meshBuffer.Dispose();

                argBuffer.Release();
                argBuffer.Dispose();
            }

            Vector3Int res = Vector3Int.FloorToInt((Vector3)GetResolution() / Mathf.Pow(2, reduction));

            int SIZE = res.x * res.y * res.z * 5;

            int bound = 4000000;

            if(SIZE > bound){
                Debug.Log("[MeshGPU] Vertex Limit of 4M triangles Applied (" + res.x  + "," + res.y + "," + res.z+")");
                SIZE = bound;
            }

            meshBuffer = new ComputeBuffer(SIZE, (sizeof(float) * 3) * 3, ComputeBufferType.Append);

            argBuffer = new ComputeBuffer(5, sizeof(int));
            uint[] args = new uint[5]{0,1,0,0,0};
            argBuffer.SetData(args);

            Debug.Log("[MeshGPU] Created buffer " + (SIZE*3*3) + " bytes");



        }

        public int[] GetArgs(){
            int[] args = new int[argBuffer.count];
            argBuffer.GetData(args);
            return args;
        }


        public RenderMesh ToRenderMesh(){
            int n = GetVertexCount();

            RenderMesh renderMesh = new RenderMesh(n, n);

            ComputeBuffer vertexBuffer = renderMesh.GetVertexBuffer();
            ComputeBuffer indexBuffer = renderMesh.GetIndexBuffer();


            CheckLoadCompute();

            compute.SetBuffer(0, "_Vertices", vertexBuffer);
            compute.SetBuffer(0, "_Indices", indexBuffer);

            compute.SetBuffer(0, "_MeshBuffer", meshBuffer);

            compute.SetInt("_N", n);


            compute.Dispatch(0, n/1024+1, 1, 1);



            return renderMesh;


        }


        /*public Mesh Download(){
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = DownloadVertices();
            mesh.triangles = CreateTriangles(mesh.vertices.Length);
            mesh.RecalculateNormals();
            return mesh;
        }

        int[] CreateTriangles(int n){
            int[] t = new int[n];
            for (int i = 0; i < n; i++)
            {
                t[i] = i;
            }

            return t;
        }

        Vector3[] DownloadVertices(){
            int[] args = GetArgs();

            Vector3[] tmp = new Vector3[args[0]];
            meshBuffer.GetData(tmp);
            int n = args[0];
            Debug.Log("[MeshGPU] Downloading " + n + " vertices");
            Vector3[] vertices = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                vertices[i] = tmp[i];
            }
            return vertices;
        }*/

        /*public void Render(Material material, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 voxelSize, int reduciton = 0){
            if(meshBuffer != null){

                Vector3Int res = GetResolution();

                material.SetBuffer("_Buffer", meshBuffer);
                material.SetMatrix("_ObjectToWorldMatrix", Matrix4x4.TRS(position, rotation, scale));
                material.SetTexture("_Densities", densities);
                material.SetVector("_Resolution", (Vector3)GetResolution() / Mathf.Pow(2, reduciton));
                //material.SetVector("_ReductionVector", Vector3.one * Mathf.Pow(2f, reduciton));
                //material.SetVector("_LocalOffset", ((Vector3)res)*0.5f);

                material.SetVector("_VoxelSize", voxelSize);


                material.SetPass(0);
                Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, argBuffer, 0);





            }
        }

        public void Render(Vector3 position, Quaternion rotation, Vector3 scale, Vector3 voxelSize, int reduction = 0){
            Render(CheckGetDefaultMaterial(), position, rotation, scale, voxelSize, reduction = reduction);
        }






        Material CheckGetDefaultMaterial(){
            if(defaultMaterial == null){
                defaultMaterial = (Material)Resources.Load("Materials/MeshBufferDefault");
            }
            return defaultMaterial;
        }*/


    }
}
