using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;


namespace realvirtual.VolumeTracking
{

    public class VolumetricGrid : MonoBehaviour
    {
        public float resolution;
        public Mesh mesh;
        public Vector3[] cellPositions;

        private void OnDrawGizmosSelected()
        {
            DrawGizmos(Color.green);
        }

        public void DrawGizmos(Color color)
        {

            Gizmos.color = Color.black;
            Gizmos.matrix = transform.localToWorldMatrix;

            Bounds bounds = GetBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            if (cellPositions == null)
            {
                return;
            }


            Color c = new Color(color.r, color.g, color.b, 0.1f);


            Gizmos.color = c;



            foreach (Vector3 cellPosition in cellPositions)
            {
                Gizmos.DrawWireCube(cellPosition, Vector3.one * resolution);

            }
        }

        [Button]
        public void Init()
        {
            Mesh inverseMesh = InverseMeshFaces(mesh);

            MeshCollider insideCollider = gameObject.AddComponent<MeshCollider>();
            insideCollider.sharedMesh = inverseMesh;

            MeshCollider outsideCollider = gameObject.AddComponent<MeshCollider>();
            outsideCollider.sharedMesh = mesh;



            // iterate over each cell in the bounds in local space


            Vector3[] candidateCellPositions = GetCandidateCellPositions();
            // filter out the cells that are inside the mesh

            List<Vector3> validCellPositions = new List<Vector3>();
            foreach (Vector3 candidateCellPosition in candidateCellPositions)
            {

                Vector3[] positions = new Vector3[9];

                positions[0] = candidateCellPosition;

                // add the corners
                positions[1] = candidateCellPosition + new Vector3(-1, -1, -1) * resolution * 0.5f;
                positions[2] = candidateCellPosition + new Vector3(-1, -1, 1) * resolution * 0.5f;
                positions[3] = candidateCellPosition + new Vector3(-1, 1, -1) * resolution * 0.5f;
                positions[4] = candidateCellPosition + new Vector3(-1, 1, 1) * resolution * 0.5f;
                positions[5] = candidateCellPosition + new Vector3(1, -1, -1) * resolution * 0.5f;
                positions[6] = candidateCellPosition + new Vector3(1, -1, 1) * resolution * 0.5f;
                positions[7] = candidateCellPosition + new Vector3(1, 1, -1) * resolution * 0.5f;
                positions[8] = candidateCellPosition + new Vector3(1, 1, 1) * resolution * 0.5f;

                for (int i = 0; i < 9; i++)
                {
                    if (ValidateCellPosition(positions[i], insideCollider, outsideCollider))
                    {
                        validCellPositions.Add(candidateCellPosition);
                        break;
                    }

                }

            }



            // convert the list to an array

            cellPositions = validCellPositions.ToArray();

            DestroyImmediate(insideCollider);
            DestroyImmediate(outsideCollider);
            DestroyImmediate(inverseMesh);

        }

        public static Mesh InverseMeshFaces(Mesh originalMesh)
        {
            Mesh newMesh = Instantiate(originalMesh);

            // Reverse the triangle indices
            int[] triangles = newMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 1];
                triangles[i + 1] = temp;
            }

            newMesh.triangles = triangles;

            // Optionally, recalculate normals and tangents
            newMesh.RecalculateNormals();
            newMesh.RecalculateTangents();

            return newMesh;
        }




        bool ValidateCellPosition(Vector3 cellPosition, MeshCollider insideCollider, MeshCollider outsideCollider)
        {
            Vector3 worldPosition = transform.TransformPoint(cellPosition);

            // try to find a point inside the mesh by raycasting from the cell position
            Vector3 rayDirection = Vector3.forward;
            Ray ray = new Ray(worldPosition, rayDirection);
            RaycastHit insideHit;
            if (insideCollider.Raycast(ray, out insideHit, 100))
            {

                RaycastHit outsideHit;
                if (outsideCollider.Raycast(ray, out outsideHit, 100))
                {
                    float insideDistance = Vector3.Distance(insideHit.point, worldPosition);

                    float outsideDistance = Vector3.Distance(outsideHit.point, worldPosition);

                    if (insideDistance < outsideDistance)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
                else
                {
                    return true;
                }

            }

            return false;

        }

        Vector3[] GetCandidateCellPositions()
        {

            Bounds bounds = GetBounds();

            Vector3Int cellCount = new Vector3Int(
                Mathf.CeilToInt(bounds.size.x / resolution),
                Mathf.CeilToInt(bounds.size.y / resolution),
                Mathf.CeilToInt(bounds.size.z / resolution)
            );


            Vector3[] candidateCellPositions = new Vector3[cellCount.x * cellCount.y * cellCount.z];

            Vector3 centerOffset = Vector3.one * resolution * 0.5f;
            int index = 0;
            for (int x = 0; x < cellCount.x; x++)
            {
                for (int y = 0; y < cellCount.y; y++)
                {
                    for (int z = 0; z < cellCount.z; z++)
                    {
                        Vector3 cellPosition = new Vector3(
                            bounds.min.x + x * resolution,
                            bounds.min.y + y * resolution,
                            bounds.min.z + z * resolution
                        );
                        candidateCellPositions[index] = cellPosition + centerOffset;
                        index++;
                    }
                }
            }

            return candidateCellPositions;
        }

        Bounds GetBounds()
        {
            // bounds containing all the vertices of the mesh in the transforms local space
            Bounds bounds = mesh.bounds;
            //bounds.center = transform.InverseTransformPoint(bounds.center);
            return bounds;
        }
    }

}