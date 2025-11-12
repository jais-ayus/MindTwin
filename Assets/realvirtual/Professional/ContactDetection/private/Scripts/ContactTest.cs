// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace realvirtual
{
    public class ContactTest : MonoBehaviour
{
    public MeshFilter meshFilterA;
    public MeshFilter meshFilterB;

    public List<Vector3> points = new List<Vector3>();

    void OnDrawGizmos(){

        if(points.Count>0){

            Gizmos.color = Color.red;
            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[0], points[2]);
            Gizmos.DrawLine(points[2], points[1]);


            Gizmos.color = Color.green;
            Gizmos.DrawLine(points[3], points[4]);
            Gizmos.DrawLine(points[3], points[5]);
            Gizmos.DrawLine(points[5], points[4]);
        }
    }



    [Button("Test")]
    void Test(){

        points = new List<Vector3>();

        Transform transformA = meshFilterA.transform;
        Transform transformB = meshFilterB.transform;

        Vector3[] verticesA = meshFilterA.sharedMesh.vertices;
        Vector3[] verticesB = meshFilterB.sharedMesh.vertices;

        int[] trinaglesA = meshFilterA.sharedMesh.triangles;
        int[] trinaglesB = meshFilterB.sharedMesh.triangles;

        int ta = trinaglesA.Length/3;
        int tb = trinaglesB.Length/3;

        for (int i = 0; i < ta; i++){

            Vector3 p1 = transformA.TransformPoint(verticesA[trinaglesA[i*3+0]]);
            Vector3 p2 = transformA.TransformPoint(verticesA[trinaglesA[i*3+1]]);
            Vector3 p3 = transformA.TransformPoint(verticesA[trinaglesA[i*3+2]]);

            for (int j = 0; j < tb; j++){

                Vector3 p4 = transformB.TransformPoint(verticesB[trinaglesB[j*3+0]]);
                Vector3 p5 = transformB.TransformPoint(verticesB[trinaglesB[j*3+1]]);
                Vector3 p6 = transformB.TransformPoint(verticesB[trinaglesB[j*3+2]]);


                if (!ComputeIntersectionAABB(p1, p2, p3, p4, p5, p6)){
                    continue;
                }

                if (ComputeIntersection(p1, p2, p3, p4, p5, p6) && ComputeIntersectionWithPlanes(p1, p2, p3, p4, p5, p6)){
                    points.Add(p1);	
                    points.Add(p2);
                    points.Add(p3);
                    points.Add(p4);
                    points.Add(p5);
                    points.Add(p6);
                    Debug.Log("intersecting");
                    return;
                }
                
            }
        }
    }



    public static bool ComputeIntersectionAABB(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Vector3 p6)
    {
        // Compute AABBs for both triangles
        Vector3 minA, maxA, minB, maxB;
        ComputeAABB(p1, p2, p3, out minA, out maxA);
        ComputeAABB(p4, p5, p6, out minB, out maxB);

        // Check for AABB intersection
        return !(maxA.x < minB.x || minA.x > maxB.x ||
                maxA.y < minB.y || minA.y > maxB.y ||
                maxA.z < minB.z || minA.z > maxB.z);
    }

    static void ComputeAABB(Vector3 p1, Vector3 p2, Vector3 p3, out Vector3 min, out Vector3 max)
    {
        min = Vector3.Min(Vector3.Min(p1, p2), p3);
        max = Vector3.Max(Vector3.Max(p1, p2), p3);
    }

    public static bool ComputeIntersectionWithPlanes(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Vector3 p6)
    {
        // Define planes for the triangles
        Plane pa = new Plane(p1, p2, p3);
        Plane pb = new Plane(p4, p5, p6);

        // Check if points of triangle tb are not all on the same side of plane pa
        if (pb.GetSide(p1) == pb.GetSide(p2) && pb.GetSide(p1) == pb.GetSide(p3))
            return false;

        // Check if points of triangle ta are not all on the same side of plane pb
        if (pa.GetSide(p4) == pa.GetSide(p5) && pa.GetSide(p4) == pa.GetSide(p6))
            return false;

        // If both conditions are met, triangles potentially intersect
        return true;
    }
    
    public static bool ComputeIntersection(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Vector3 p6)
    {
        // Define separating axes (triangle edges and normals)
        Vector3[] edges1 = { p2 - p1, p3 - p2, p1 - p3 };
        Vector3[] edges2 = { p5 - p4, p6 - p5, p4 - p6 };
        Vector3 normal1 = Vector3.Cross(p2 - p1, p3 - p1).normalized;
        Vector3 normal2 = Vector3.Cross(p5 - p4, p6 - p4).normalized;

        // Additional separating axes: cross products of edges from both triangles
        Vector3[] additionalAxes = {
            Vector3.Cross(edges1[0], edges2[0]),
            Vector3.Cross(edges1[0], edges2[1]),
            Vector3.Cross(edges1[0], edges2[2]),
            Vector3.Cross(edges1[1], edges2[0]),
            Vector3.Cross(edges1[1], edges2[1]),
            Vector3.Cross(edges1[1], edges2[2]),
            Vector3.Cross(edges1[2], edges2[0]),
            Vector3.Cross(edges1[2], edges2[1]),
            Vector3.Cross(edges1[2], edges2[2])
        };

        // Test separating axes for potential intersection
        foreach (Vector3 axis in edges1)
        {
            if (!IsAxisSeparating(axis, p1, p2, p3, p4, p5, p6))
                return false;
        }

        foreach (Vector3 axis in edges2)
        {
            if (!IsAxisSeparating(axis, p4, p5, p6, p1, p2, p3))
                return false;
        }

        foreach (Vector3 axis in additionalAxes)
        {
            if (!IsAxisSeparating(axis, p1, p2, p3, p4, p5, p6))
                return false;
        }

        // No separating axis found overlap, triangles potentially intersect
        return true;
    }

    static bool IsAxisSeparating(Vector3 axis, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Vector3 p6)
    {
        float overlap = AxisProj(axis, p1, p2, p3, p4, p5, p6);
        return overlap > 0;
    }



    // Helper function to project triangles onto an axis and check for overlap
    static float AxisProj(Vector3 axis, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Vector3 p6)
    {
        float min1 = Vector3.Dot(axis, p1);
        float max1 = min1;
        float min2 = Vector3.Dot(axis, p4);
        float max2 = min2;

        // Project all points of triangle 1 onto the axis
        max1 = Mathf.Max(max1, Vector3.Dot(axis, p2));
        max1 = Mathf.Max(max1, Vector3.Dot(axis, p3));
        min1 = Mathf.Min(min1, Vector3.Dot(axis, p2));
        min1 = Mathf.Min(min1, Vector3.Dot(axis, p3));

        // Project all points of triangle 2 onto the axis
        max2 = Mathf.Max(max2, Vector3.Dot(axis, p5));
        max2 = Mathf.Max(max2, Vector3.Dot(axis, p6));
        min2 = Mathf.Min(min2, Vector3.Dot(axis, p5));
        min2 = Mathf.Min(min2, Vector3.Dot(axis, p6));

        // Calculate overlap
        float overlap = Mathf.Min(max1, max2) - Mathf.Max(min1, min2);

        return overlap;
    }
    
}

}
