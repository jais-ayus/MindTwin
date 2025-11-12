using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class IsoSurface
    {
        public static void CreateMesh(VolumeTracker tracker, float target)
        {
            GameObject obj = GameObject.Instantiate((GameObject)UnityEngine.Resources.Load<GameObject>("IsoSurface"));
            obj.transform.position = tracker.transform.position;
            obj.transform.rotation = tracker.transform.rotation;
            obj.transform.localScale = Vector3.one;
            obj.name = "IsoSurface";

            DistanceTracker sdf = tracker.distanceTracker;
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            MarchingCubes marchingCubes = new MarchingCubes(1000000, Vector3.one * 0.01f);

            marchingCubes.March(
                sdf.sdf,
                target / tracker.settings.size.x,
                0,
                0,
                -tracker.transform.localScale * 0.5f,
                true,
                true
            );

            Mesh mesh = marchingCubes.ToMesh(invert: true);
            marchingCubes.Release();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;
        }
    }
}