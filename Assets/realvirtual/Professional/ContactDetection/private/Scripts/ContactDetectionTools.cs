#if REALVIRTUAL_BURST
// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

namespace realvirtual
{

    using System;
    using UnityEngine;
    using Unity.Mathematics;
    using Unity.Jobs;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;


    public static class ContactDetectionTools
    {

        [BurstCompile]
        public static bool IsIntersecting(float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6,
            float tolerance)
        {

            // Add AABB check for performance

            if (!ComputeIntersectionAABB(p1, p2, p3, p4, p5, p6))
            {
                return false;
            }



            float3 edges10 = p2 - p1;
            float3 edges11 = p3 - p2;
            float3 edges12 = p1 - p3;

            float3 edges20 = p5 - p4;
            float3 edges21 = p6 - p5;
            float3 edges22 = p4 - p6;

            // edges1

            if (!IsAxisSeparating(edges10, p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(edges11, p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(edges12, p1, p2, p3, p4, p5, p6, tolerance))
                return false;


            // edges2

            if (!IsAxisSeparating(edges20, p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(edges21, p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(edges22, p1, p2, p3, p4, p5, p6, tolerance))
                return false;


            // additional axes

            if (!IsAxisSeparating(math.cross(edges10, edges20), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges10, edges21), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges10, edges22), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges11, edges20), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges11, edges21), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges11, edges22), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges12, edges20), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges12, edges21), p1, p2, p3, p4, p5, p6, tolerance))
                return false;

            if (!IsAxisSeparating(math.cross(edges12, edges22), p1, p2, p3, p4, p5, p6, tolerance))
                return false;




            //float3 na = math.normalize(math.cross(p2 - p1, p3 - p1));
            //float3 nb = math.normalize(math.cross(p5 - p4, p6 - p4));






            /*if(!ComputeIntersectionWithPlanes(p1, p2, p3, p4, p5, p6, na, nb)){
                return false;
            }*/

            // No separating axis found, triangles intersect
            return true;
        }


        [BurstCompile]
        static bool IsAxisSeparating(float3 axis, float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6,
            float tolerance)
        {
            float min1 = math.dot(axis, p1);
            float max1 = min1;
            float min2 = math.dot(axis, p4);
            float max2 = min2;

            // Project all points of triangle 1 onto the axis
            max1 = math.max(max1, math.dot(axis, p2));
            max1 = math.max(max1, math.dot(axis, p3));
            min1 = math.min(min1, math.dot(axis, p2));
            min1 = math.min(min1, math.dot(axis, p3));

            // Project all points of triangle 2 onto the axis
            max2 = math.max(max2, math.dot(axis, p5));
            max2 = math.max(max2, math.dot(axis, p6));
            min2 = math.min(min2, math.dot(axis, p5));
            min2 = math.min(min2, math.dot(axis, p6));

            // Calculate overlap
            float overlap = math.min(max1, max2) - math.max(min1, min2);

            return overlap > tolerance;
        }

        [BurstCompile]
        public static bool ComputeIntersectionAABB(float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6)
        {
            // Compute AABBs for both triangles
            float3 minA, maxA, minB, maxB;
            ComputeAABB(p1, p2, p3, out minA, out maxA);
            ComputeAABB(p4, p5, p6, out minB, out maxB);

            // Check for AABB intersection
            return !(maxA.x < minB.x || minA.x > maxB.x ||
                     maxA.y < minB.y || minA.y > maxB.y ||
                     maxA.z < minB.z || minA.z > maxB.z);
        }

        [BurstCompile]
        static void ComputeAABB(float3 p1, float3 p2, float3 p3, out float3 min, out float3 max)
        {
            min = math.min(math.min(p1, p2), p3);
            max = math.max(math.max(p1, p2), p3);
        }

        [BurstCompile]
        public static bool ComputeIntersectionWithPlanes(float3 p1, float3 p2, float3 p3, float3 p4, float3 p5,
            float3 p6, float3 na, float3 nb)
        {



            float3 cb = (p4 + p5 + p6) / 3;

            float d1 = math.dot(p1 - cb, nb);
            float d2 = math.dot(p2 - cb, nb);
            float d3 = math.dot(p3 - cb, nb);

            if (d1 * d2 > 0 && d2 * d3 > 0)
            {
                return false;
            }


            float3 ca = (p1 + p2 + p3) / 3;

            float d4 = math.dot(p4 - ca, na);
            float d5 = math.dot(p5 - ca, na);
            float d6 = math.dot(p6 - ca, na);

            if (d4 * d5 > 0 && d5 * d6 > 0)
            {
                return false;
            }


            // If both conditions are met, triangles potentially intersect
            return true;
        }



        public static JobHandle HasContact(NativeMeshData meshDataA, NativeMeshData meshDataB,
            NativeArray<bool> results, int resultIndex, float tolerance)
        {
            return HasContact(meshDataA.vertices, meshDataA.indices, meshDataB.vertices, meshDataB.indices, results,
                resultIndex, tolerance);
        }

        private static JobHandle HasContact(NativeArray<float3> verticesA, NativeArray<int> indicesA,
            NativeArray<float3> verticesB, NativeArray<int> indicesB, NativeArray<bool> results, int resultIndex,
            float tolerance)
        {
            int numTrianglesA = indicesA.Length / 3;
            var job = new TriangleIntersectionJob
            {
                VerticesA = verticesA,
                IndicesA = indicesA,
                VerticesB = verticesB,
                IndicesB = indicesB,
                Results = results,
                ResultIndex = resultIndex,
                Tolerance = tolerance
            };

            JobHandle handle = job.Schedule();



            return handle;
        }
    }

    public class NativeMeshData : IDisposable
    {
        public NativeArray<float3> vertices;
        public NativeArray<int> indices;

        public NativeMeshData(Mesh mesh, Transform transform)
        {
            if (mesh == null)
                throw new ArgumentNullException("mesh");

            // Allocate native arrays
            vertices = new NativeArray<float3>(mesh.vertexCount, Allocator.TempJob);
            indices = new NativeArray<int>(mesh.triangles.Length, Allocator.TempJob);

            // Transform vertices to world space
            Vector3[] meshVertices = mesh.vertices;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            for (int i = 0; i < meshVertices.Length; i++)
            {
                //Vector3 worldVertex = matrix.MultiplyPoint3x4(meshVertices[i]);
                Vector3 worldVertex = transform.TransformPoint(meshVertices[i]);
                vertices[i] = new float3(worldVertex.x, worldVertex.y, worldVertex.z);
            }

            // Copy indices directly
            indices.CopyFrom(mesh.triangles);
        }

        public void Dispose()
        {
            if (vertices.IsCreated)
                vertices.Dispose();
            if (indices.IsCreated)
                indices.Dispose();
        }
    }

    [BurstCompile]
    public struct TriangleIntersectionJob : IJob
    {
        [ReadOnly] public NativeArray<float3> VerticesA;
        [ReadOnly] public NativeArray<int> IndicesA;
        [ReadOnly] public NativeArray<float3> VerticesB;
        [ReadOnly] public NativeArray<int> IndicesB;


        [NativeDisableContainerSafetyRestriction]
        public NativeArray<bool> Results;

        public int ResultIndex;

        public float Tolerance;

        public void Execute()
        {

            for (int j = 0; j < IndicesA.Length / 3; j++)
            {

                int triangleIndexA = j * 3;
                float3 p1 = VerticesA[IndicesA[triangleIndexA]];
                float3 p2 = VerticesA[IndicesA[triangleIndexA + 1]];
                float3 p3 = VerticesA[IndicesA[triangleIndexA + 2]];

                for (int i = 0; i < IndicesB.Length / 3; i++)
                {
                    int triangleIndexB = i * 3;
                    float3 p4 = VerticesB[IndicesB[triangleIndexB]];
                    float3 p5 = VerticesB[IndicesB[triangleIndexB + 1]];
                    float3 p6 = VerticesB[IndicesB[triangleIndexB + 2]];

                    if (ContactDetectionTools.IsIntersecting(p1, p2, p3, p4, p5, p6, Tolerance))
                    {
                        Results[ResultIndex] = true;
                        return; // Stop checking as soon as we find one intersection
                    }
                }

            }


        }
    }
}


#endif