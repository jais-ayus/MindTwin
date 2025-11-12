
using NaughtyAttributes;
using UnityEngine;


namespace realvirtual
{
    //! Optimizes 3D mesh complexity for improved performance while maintaining visual quality in industrial models.
    //! This professional tool reduces polygon counts in CAD-imported models using advanced simplification algorithms
    //! with configurable quality levels. Essential for optimizing complex industrial equipment models, enabling
    //! real-time visualization of large assemblies, and improving frame rates in VR/AR applications. Supports
    //! lossless mode and intelligent edge preservation for maintaining critical geometric features.
    public class MeshOptimizer : MonoBehaviour
    {

        public bool LosLess = false;

        [SerializeField, Range(0f, 1f), Tooltip("The desired quality of the simplified mesh.")]
        public float Quality = 0.5f;

        public bool EnableSmartLink = true;
        public bool PreserverBorderEdges = false;
        public bool PreserveUVSeamEdges = false;
        public bool PreserveUVFoldoverEdges = false;
        public double Agressivness = 2.0;

        [SerializeField] private Mesh oldMesh;
        public float OldMeshVertices = 0;
        public float OldMeshTriangles = 0;
        public float NewNeshVertices = 0;
        public float NewNeshTriangles = 0;

        private void Reset()
        {
            UndoSimplifyMeshFilter();
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                return;
            OldMeshVertices = meshFilter.sharedMesh.vertexCount;

            NewNeshVertices = meshFilter.sharedMesh.vertexCount;
        }

        [Button("Simplify Mesh")]
        private void SimplifyMeshFilter()
        {
            if (oldMesh != null)
                UndoSimplifyMeshFilter();
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            Mesh sourceMesh = meshFilter.sharedMesh;
            if (sourceMesh == null) // verify that the mesh filter actually has a mesh
                return;

            if (oldMesh == null)
                oldMesh = sourceMesh;

            // Create our mesh simplifier and setup our entire mesh in it
            var meshSimplifier = new MeshSimplifier();


            meshSimplifier.Initialize(sourceMesh);
            meshSimplifier.EnableSmartLink = EnableSmartLink;
            meshSimplifier.PreserveBorderEdges = PreserverBorderEdges;
            meshSimplifier.PreserveUVSeamEdges = PreserveUVSeamEdges;
            meshSimplifier.PreserveUVFoldoverEdges = PreserveUVFoldoverEdges;
            meshSimplifier.Agressiveness = Agressivness;
            // This is where the magic happens, lets simplify!
            if (LosLess)
                meshSimplifier.SimplifyMeshLossless();
            else
                meshSimplifier.SimplifyMesh(Quality);

            // Create our final mesh and apply it back to our mesh filter
            meshFilter.sharedMesh = meshSimplifier.ToMesh();

            OldMeshVertices = oldMesh.vertexCount;
            OldMeshTriangles = oldMesh.triangles.Length / 3;
            NewNeshVertices = meshFilter.sharedMesh.vertexCount;
            NewNeshTriangles = meshFilter.sharedMesh.triangles.Length / 3;
        }

        [Button("Undo Simplify")]
        private void UndoSimplifyMeshFilter()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (oldMesh == null || meshFilter == null)
                return;
            meshFilter.sharedMesh = oldMesh;
            oldMesh = null;
            OldMeshVertices = meshFilter.sharedMesh.vertexCount;
            OldMeshTriangles = meshFilter.sharedMesh.triangles.Length / 3;
            NewNeshVertices = meshFilter.sharedMesh.vertexCount;
            NewNeshTriangles = meshFilter.sharedMesh.triangles.Length / 3;
        }

    }
}