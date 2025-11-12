#if REALVIRTUAL_BURST
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;


namespace realvirtual
{
    public static class ContactDetection
    {
        public static List<GameObject> FindContacts(GameObject target, List<GameObject> ignoredObjects, float tolerance,
            bool overrideGroups, int verbose = 0)
        {
            var targetRenderer = target.GetComponent<MeshRenderer>();
            var targetFilter = target.GetComponent<MeshFilter>();

#if UNITY_EDITOR
            if (!targetFilter.sharedMesh.isReadable)
            {
                EditorUtility.DisplayDialog("Warning", "The mesh " + targetFilter.sharedMesh.name + " is not readable.",
                    "Ok");
                return new List<GameObject>();
            }
#endif

            var otherRenderers = Object.FindObjectsOfType<MeshRenderer>();

            if (ignoredObjects == null) ignoredObjects = new List<GameObject>();

            if (verbose > 0) Debug.Log("Comparing bounds for " + (otherRenderers.Length - 1) + " renderers.");

            // intersecting bounds

            var intersectingRenderers = new List<MeshRenderer>();
            for (var i = 0; i < otherRenderers.Length; i++)
            {
                var mr = otherRenderers[i];
                if (mr != targetRenderer)
                    if (!ignoredObjects.Contains(mr.gameObject))
                        if (targetRenderer.bounds.Intersects(mr.bounds))
                        {
                            Debug.Log(overrideGroups);
                            if (overrideGroups)
                            {
                                intersectingRenderers.Add(mr);
                            }
                            else
                            {
                                if (mr.GetComponent<Group>() == null) intersectingRenderers.Add(mr);
                            }
                        }
            }

            if (verbose > 0) Debug.Log("Intersecting bounds for " + intersectingRenderers.Count + " renderers.");


            // Mesh Contacts

            var contacts = new List<GameObject>();

            if (intersectingRenderers.Count == 0) return contacts;

            var results = new NativeArray<bool>(intersectingRenderers.Count, Allocator.TempJob);
            var targetData = new NativeMeshData(targetFilter.sharedMesh, target.transform);

            var allData = new List<NativeMeshData>();
            var handles = new List<JobHandle>();

            var hasUnreadable = false;

            for (var i = 0; i < intersectingRenderers.Count; i++)
            {
                var mr = intersectingRenderers[i];
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf.sharedMesh.isReadable)
                {
                    hasUnreadable = true;
                }
                else
                {
                    var otherData = new NativeMeshData(mf.sharedMesh, mr.transform);
                    allData.Add(otherData);
                    handles.Add(ContactDetectionTools.HasContact(targetData, otherData, results, i, tolerance));
                }
            }

            for (var i = 0; i < handles.Count; i++)
            {
                handles[i].Complete();
                allData[i].Dispose();


                if (results[i]) contacts.Add(intersectingRenderers[i].gameObject);
            }

            targetData.Dispose();
            results.Dispose();


            if (hasUnreadable)
            {
#if UNITY_EDITOR
                EditorUtility.DisplayDialog("Warning",
                    "Not all meshes are readable. You can change this by setting read/write to true in the mesh import settings.",
                    "Ok");
#endif
            }

            if (verbose > 0) Debug.Log("Detected " + contacts.Count + " contacts.");

            return contacts;
        }


        public static List<GameObject> FindAllConnected(GameObject target)
        {
            var remaining = new List<GameObject>();
            var total = new List<GameObject>();

            remaining.Add(target);
            total.Add(target);

            var iteration = 0;

            while (remaining.Count > 0)
            {
                iteration++;

                var newObs = new List<GameObject>();

                for (var i = 0; i < remaining.Count; i++)
                {
                    var contacts = FindContacts(remaining[i], total, 0, false);

                    for (var j = 0; j < contacts.Count; j++)
                        if (!total.Contains(contacts[j]))
                        {
                            total.Add(contacts[j]);
                            newObs.Add(contacts[j]);
                        }
                }

                remaining = newObs;
            }

            return total;
        }
    }
}
#endif