using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace realvirtual
{
    
    public class MeshCombinerEditor
    {
        
        public static bool DeleteEmptyGroupObjects = true;

        private static bool deleteOldMeshes = true;
        private static List<GameObject> NewMeshes = new List<GameObject>();
        private static List<MeshRenderer> deactivatedRenderer = new List<MeshRenderer>();
#if UNITY_EDITOR
        public static void OptimizeSelectObject(GameObject go, ref List<GameObject>optimizedMeshes, ref List<MeshRenderer>deactivatedObjects,bool deleteDeactivated)
        {
            deleteOldMeshes = deleteDeactivated;
            CombineRootObject(go);
            optimizedMeshes = NewMeshes;
            deactivatedObjects = deactivatedRenderer;
        }
        public static void FinalizeHierarchy(GameObject go)
        {
            Selection.activeGameObject = go;
            CleanUpSelectedHierarchy();
        }
        

        [MenuItem("GameObject/realvirtual/Combine Meshes (Pro)", false, 10)]
        private static void CombineSelectedMeshes()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            foreach (var selectedObject in selectedObjects)
            {
                CombineRootObject(selectedObject);
            }
            
            CleanUpSelectedHierarchy();
        }
        
        [MenuItem("GameObject/realvirtual/Clean Up Hierarchy (Pro)", false, 9)]
        private static void CleanUpSelectedHierarchy()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            foreach (var selectedObject in selectedObjects)
            {
                CleanUpHierarchy(selectedObject);
            }
        }

        // has bugs needs different approach [MenuItem("GameObject/realvirtual/Fix Negative Scales", false, 0)]
        private static void FixNegativeScalesSelection()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            foreach (var selectedObject in selectedObjects)
            {
                FixNegativeScales(selectedObject.transform);
            }
        }
        
        #endif
        
        private static void FlipTriangles(Mesh mesh)
        {
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 1];
                triangles[i + 1] = temp;
            }
            mesh.triangles = triangles;
        }

        private static void FixMesh(Vector3 factor, Transform root, MeshFilter target)
        {
            
            Mesh mesh = target.sharedMesh;
            Vector3[] globalVertices = mesh.vertices;
            Vector3[] globalNormals = mesh.normals;
            
            for (int i = 0; i < globalVertices.Length; i++)
            {
                globalVertices[i] = target.transform.TransformPoint(globalVertices[i]);
                globalNormals[i] = target.transform.TransformDirection(globalNormals[i]);
            }
            
            root.localScale = new Vector3(root.localScale.x*factor.x, root.localScale.y*factor.y, root.localScale.z*factor.z);
            
            Vector3[] newVertices = new Vector3[globalVertices.Length];
            Vector3[] newNormals = new Vector3[globalNormals.Length];
            for (int i = 0; i < globalVertices.Length; i++)
            {
                newVertices[i] = target.transform.InverseTransformPoint(globalVertices[i]);
                newNormals[i] = target.transform.InverseTransformDirection(globalNormals[i]).normalized;
            }
            
            root.localScale = new Vector3(root.localScale.x/factor.x, root.localScale.y/factor.y, root.localScale.z/factor.z);
            
            Mesh newMesh = GameObject.Instantiate(mesh);
            newMesh.name = mesh.name;
            newMesh.vertices = newVertices;
            newMesh.normals = newNormals;
            
            FlipTriangles(newMesh);
            
            newMesh.RecalculateBounds();
            
            target.sharedMesh = newMesh;
            
            
            
            
        }

        private static void FixNegativeScales(Transform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

            if (root.localScale.x < 0)
            {
                Vector3 factor = new Vector3(-1, 1, 1);
                MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    FixMesh(factor, root, meshFilter);
                }
                root.localScale = new Vector3(root.localScale.x*factor.x, root.localScale.y*factor.y, root.localScale.z*factor.z);

                
            }
            
            if (root.localScale.y < 0)
            {
                Vector3 factor = new Vector3(1, -1, 1);
                MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    FixMesh(factor, root, meshFilter);
                }
                root.localScale = new Vector3(root.localScale.x*factor.x, root.localScale.y*factor.y, root.localScale.z*factor.z);

            }
            
            if (root.localScale.z < 0)
            {
                Vector3 factor = new Vector3(1, 1, -1);
                MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>();
                foreach (var meshFilter in meshFilters)
                {
                    FixMesh(factor, root, meshFilter);
                }
                root.localScale = new Vector3(root.localScale.x*factor.x, root.localScale.y*factor.y, root.localScale.z*factor.z);

            }



            for(int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != root)
                {
                    FixNegativeScales(transform);
                }
            }
        }
        
        private static void CleanUpHierarchy(GameObject root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Dictionary<Transform, bool> transformInfo = new Dictionary<Transform, bool>();

            foreach (var transform in transforms)
            {
                transformInfo.Add(transform, IsValidTransform(transform));
            }

            List<Transform> toDelete = new List<Transform>();

            foreach (var transform in transformInfo.Keys)
            {
                bool hasValidChild = false;
                foreach (var child in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (transformInfo[child])
                    {
                        hasValidChild = true;
                        break;
                    }
                }

                if (!hasValidChild && !transformInfo[transform])
                {
                    toDelete.Add(transform);
                }
            }

            for (int i = toDelete.Count - 1; i >= 0; i--)
            {
                if (toDelete[i] != root.transform)
                {
                    GameObject.DestroyImmediate(toDelete[i].gameObject);
                }
            }
        }

        private static bool IsValidTransform(Transform transform)
        {

            if (DeleteEmptyGroupObjects)
            {
                bool hasSpecialComponent = false;
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component.GetType() != typeof(Transform) && component.GetType() != typeof(Group))
                    {
                        hasSpecialComponent = true;
                        break;
                    }
                }
                return hasSpecialComponent;
            }
            
            
            // it is valid id it has any additional component
            return transform.GetComponents<Component>().Length > 1;
        }

        private static void CombineRootObject(GameObject root)
        {
            Dictionary<Transform, List<MeshRenderer>> renderermap = new Dictionary<Transform, List<MeshRenderer>>();
            renderermap.Add(root.transform, new List<MeshRenderer>());

            // assign renderers to drives
            Drive[] drives = root.GetComponentsInChildren<Drive>();
            
            foreach (var drive in drives)
            {
                if (!renderermap.ContainsKey(drive.transform))
                {
                    renderermap.Add(drive.transform, new List<MeshRenderer>());
                }
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                Transform parent = root.transform;
                Drive drive = renderer.GetComponentInParent<Drive>();
                if (drive != null)
                {
                    parent = drive.transform;
                }
                renderermap[parent].Add(renderer);
            }

            int count = 0;

            foreach (var transform in renderermap.Keys)
            {
                count += CombineByGroups(renderermap[transform], transform.gameObject);
            }
            
            Debug.Log("Reduced from " + renderers.Length + " to " + count + " meshes in " + root.name);

        }

        private static int CombineByGroups(List<MeshRenderer> renderers, GameObject parent)
        {
            Dictionary<string, List<MeshRenderer>> groupmap = new Dictionary<string, List<MeshRenderer>>();
            foreach (var renderer in renderers)
            {
                Group[] groups = renderer.GetComponents<Group>();
                List<string> groupNames = new List<string>();
                foreach (Group group in groups)
                {
                    string name;
                    if(group.GroupNamePrefix!=null)
                        name= group.GroupNamePrefix.name+"~~~" + group.GroupName;
                    else
                        name = group.GroupName;
                    
                    groupNames.Add(name);
                }
                if(groupNames.Count == 0)
                    groupNames.Add("__rv__Static");
                
                groupNames.Sort();
                
                string groupKey = string.Join("|", groupNames);
                
                if (!groupmap.ContainsKey(groupKey))
                {
                    groupmap.Add(groupKey, new List<MeshRenderer>());
                }
                
                groupmap[groupKey].Add(renderer);
                
                
            }
            

            foreach (string groupKey in groupmap.Keys)
            {
                Debug.Log("Group: " + groupKey + " Count: " + groupmap[groupKey].Count + " Parent: " + parent.name);

                if (groupmap.Count > 1)
                {
                    GameObject groupObject = new GameObject(groupKey);
                    groupObject.name = groupKey.Replace("__rv__", "");
                    groupObject.transform.SetParent(parent.transform);
                    groupObject.transform.localPosition = Vector3.zero;
                    groupObject.transform.localRotation = Quaternion.identity;
                    groupObject.transform.localScale = Vector3.one;
                    
                    Combine(groupmap[groupKey], groupObject);
                    AddGroups(groupObject, groupKey);
                    
                }
                else
                {
                    Combine(groupmap[groupKey], parent);
                    AddGroups(parent, groupKey);
                }
                    
                
                
                
            }
            
            return groupmap.Keys.Count;
        }

        private static void AddGroups(GameObject target, string groupKey)
        {
            string[] groupNames = groupKey.Split('|');
            
            List<string> groupNamesList = new List<string>();
            Group[] groups = target.GetComponents<Group>();
            foreach (Group group in groups)
            {
                string name;
                if(group.GroupNamePrefix!=null)
                    name= group.GroupNamePrefix.name+"~~~" + group.GroupName;
                else
                    name = group.GroupName;
                groupNamesList.Add(name);
            }
            
            foreach (var groupName in groupNames)
            {
                if (!groupNamesList.Contains(groupName))
                {
                    if(groupName.Contains("__rv__"))
                        continue;
                    Group group = target.AddComponent<Group>();
                    if(groupName.Contains("~~~"))
                    {
                        string[] parts = groupName.Split("~~~");
                        group.GroupName = parts[1];
                        GameObject go = GameObject.Find(parts[0]);
                        if (go != null)
                        {
                            group.GroupNamePrefix = go;
                        }
                    }
                    else
                    {
                        group.GroupName = groupName;
                    }
                   
                }
            }
        }

        private static void Combine(List<MeshRenderer> renderers, GameObject parent)
        {
            if (renderers.Count == 0)
                return;

            GameObject targetObject = parent;


            // Prepare lists for collecting mesh data
            List<Material> uniqueMaterials = new List<Material>();
            List<List<int>> submeshTriangles = new List<List<int>>();

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();

            int vertexOffset = 0;

            // First pass: Identify all unique materials
            foreach (var renderer in renderers)
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                Material[] mats = renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (!uniqueMaterials.Contains(mat))
                    {
                        uniqueMaterials.Add(mat);
                        submeshTriangles.Add(new List<int>());
                    }
                }
            }

            // Second pass: Combine all meshes and map their submeshes to the correct material indices
            foreach (var renderer in renderers)
            {
                bool flipX = false;
                if (renderer.transform.lossyScale.x * parent.transform.lossyScale.x < 0)
                {
                    flipX = true;
                }
                
                bool flipY = false;
                if (renderer.transform.lossyScale.y * parent.transform.lossyScale.y < 0)
                {
                    flipY = true;
                }
                
                bool flipZ = false;
                if (renderer.transform.lossyScale.z * parent.transform.lossyScale.z < 0)
                {
                    flipZ = true;
                }
                
                
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                Mesh mesh = meshFilter.sharedMesh;
                Material[] mats = renderer.sharedMaterials;

                // Get the transformation from the renderer space to the parent space
                // Since the combined object is placed directly under 'parent', we convert all vertices
                // into parent-local space. This ensures a consistent local space for the final mesh.
                Matrix4x4 localToParent = parent.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

                // Extract vertex data
                Vector3[] meshVerts = mesh.vertices;
                Vector3[] meshNorms = mesh.normals;
                Vector2[] meshUVs = mesh.uv;
                Color[] meshColors = mesh.colors;

                // Append transformed vertex data
                for (int v = 0; v < meshVerts.Length; v++)
                {
                    Vector3 transformedVertex = localToParent.MultiplyPoint3x4(meshVerts[v]);
                    verts.Add(transformedVertex);

                    if (meshNorms != null && meshNorms.Length > 0)
                    {
                        Vector3 transformedNormal = localToParent.MultiplyVector(meshNorms[v]).normalized;
                        norms.Add(transformedNormal);
                    }
                    else
                    {
                        norms.Add(Vector3.up);
                    }

                    if (meshUVs != null && meshUVs.Length > 0)
                        uvs.Add(meshUVs[v]);
                    else
                        uvs.Add(Vector2.zero);

                    if (meshColors != null && meshColors.Length > 0)
                        colors.Add(meshColors[v]);
                    else
                        colors.Add(Color.white);
                }

                // Assign triangles to correct submesh list based on material
                for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
                {
                    int[] subTriangles = mesh.GetTriangles(submeshIndex);
                    if (subTriangles.Length == 0) continue;

                    if (flipX)
                    {
                        subTriangles = InvertTriangles(subTriangles);
                    }

                    if (flipY)
                    {
                        subTriangles = InvertTriangles(subTriangles);
                    }
                    
                    if (flipZ)
                    {
                        subTriangles = InvertTriangles(subTriangles);
                    }
                  

                    Material mat = mats[submeshIndex];
                    int matIndex = uniqueMaterials.IndexOf(mat);

                    List<int> targetTriangleList = submeshTriangles[matIndex];
                    for (int t = 0; t < subTriangles.Length; t++)
                    {
                        targetTriangleList.Add(subTriangles[t] + vertexOffset);
                    }
                }

                vertexOffset += mesh.vertexCount;
            }

            // Create the final combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = parent.name + " (Combined)";

            if (verts.Count > 65535)
            {
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            combinedMesh.SetVertices(verts);
            combinedMesh.SetNormals(norms);
            combinedMesh.SetUVs(0, uvs);
            if (colors.Count > 0)
                combinedMesh.SetColors(colors);

            combinedMesh.subMeshCount = uniqueMaterials.Count;
            for (int i = 0; i < uniqueMaterials.Count; i++)
            {
                combinedMesh.SetTriangles(submeshTriangles[i], i);
            }

            combinedMesh.RecalculateBounds();
            // Normals should already be set, but if needed: combinedMesh.RecalculateNormals();
            //combinedMesh.RecalculateTangents();

            // Assign the combined mesh and materials
            MeshFilter combinedMeshFilter = targetObject.GetComponent<MeshFilter>();
            if (combinedMeshFilter == null)
                combinedMeshFilter = targetObject.AddComponent<MeshFilter>();
            MeshRenderer combinedMeshRenderer = targetObject.GetComponent<MeshRenderer>();
            if (combinedMeshRenderer == null)
                combinedMeshRenderer = targetObject.AddComponent<MeshRenderer>();

            combinedMeshFilter.sharedMesh = combinedMesh;
            combinedMeshRenderer.sharedMaterials = uniqueMaterials.ToArray();
            
            NewMeshes.Add(targetObject);
            
            
            // only destroy renderer and filter components in order to keep stuff like colliders, scripts etc !
            foreach (var renderer in renderers)
            {
              
                if (renderer.gameObject != targetObject)
                {
                    if(deleteOldMeshes)
                    {
                        MeshFilter meshFilter = renderer.gameObject.GetComponent<MeshFilter>();
                        GameObject.DestroyImmediate(renderer);
                        GameObject.DestroyImmediate(meshFilter);
                    }
                    else
                    {
                        deactivatedRenderer.Add(renderer);
                        renderer.enabled = false;
                    }
                }
            }

        }

        private static int[] InvertTriangles(int[] triangles)
        {
            int[] newTriangles = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                newTriangles[i] = triangles[i + 2];
                newTriangles[i + 1] = triangles[i + 1];
                newTriangles[i + 2] = triangles[i];
            }

            return newTriangles;

        }
    }

}

