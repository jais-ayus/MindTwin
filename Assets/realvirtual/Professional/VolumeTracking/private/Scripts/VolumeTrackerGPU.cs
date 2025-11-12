// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;

namespace realvirtual.VolumeTracking
{
    public class VolumeTrackerGPU
    {
        public VolumeTrackerGPUData data;
        private ComputeShader compute;
        private Transform transform;
        private VolumetricGrid[] targets;
        private float resolution;
        private Vector3 size;
        private Vector3Int capacities;

        private bool isTimeTracking;

        public void Init(VolumetricGrid[] targets, Transform transform, Vector3 size, float resolution,
            int trackedCellCount, Vector3Int capacities, RenderTexture volumeTexture, bool isTimeTracking)
        {
            compute = UnityEngine.Resources.Load<ComputeShader>("VolumeCompute");

            this.targets = targets;
            this.transform = transform;
            this.size = size;
            this.resolution = resolution;
            this.capacities = capacities;
            this.isTimeTracking = isTimeTracking;


            data = new VolumeTrackerGPUData();

            data.volumeTexture = volumeTexture;

            data.volumeTexture.wrapMode = TextureWrapMode.Clamp;
            data.volumeTexture.filterMode = FilterMode.Trilinear;

            data.volumeTexture.Create();

            data.matricesBuffer = new ComputeBuffer(targets.Length, 64);
            data.localGridPositionsBuffer = new ComputeBuffer(trackedCellCount, 12);
            data.matrixIndicesBuffer = new ComputeBuffer(trackedCellCount, 4);

            Vector3[] localGridPositionsArray = new Vector3[trackedCellCount];
            int[] matrixIndicesArray = new int[trackedCellCount];
            int index = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                for (int j = 0; j < targets[i].cellPositions.Length; j++)
                {
                    localGridPositionsArray[index] = targets[i].cellPositions[j];
                    matrixIndicesArray[index] = i;
                    index++;
                }
            }


            data.localGridPositionsBuffer.SetData(localGridPositionsArray);
            data.matrixIndicesBuffer.SetData(matrixIndicesArray);

            if (isTimeTracking)
            {
                AllocateTemporaryTexture();
            }
        }

        public void Iterate()
        {
            UpdateMatrices();
            if (isTimeTracking)
            {
                UpdateTimeVolumeTexture();
            }
            else
            {
                UpdateVolumeTexture();
            }
        }

        public void UpdateMatrices()
        {
            Matrix4x4[] matricesArray = new Matrix4x4[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                matricesArray[i] = targets[i].transform.localToWorldMatrix;
            }

            data.matricesBuffer.SetData(matricesArray);
        }

        void UpdateVolumeTexture()
        {
            compute.SetTexture(0, "_VolumeTexture", data.volumeTexture);
            compute.SetBuffer(0, "_Matrices", data.matricesBuffer);
            compute.SetBuffer(0, "_LocalGridPositions", data.localGridPositionsBuffer);
            compute.SetBuffer(0, "_MatrixIndices", data.matrixIndicesBuffer);
            compute.SetInt("_MatrixCount", targets.Length);
            compute.SetInt("_LocalGridPositionCount", data.localGridPositionsBuffer.count);
            compute.SetInts("_VolumeCapacities", new int[] { capacities.x, capacities.y, capacities.z });
            compute.SetFloat("_Resolution", resolution);
            compute.SetVector("_Size", size);
            compute.SetVector("_Anchor", transform.position - size * 0.5f);

            compute.Dispatch(0, data.localGridPositionsBuffer.count / 1024, 1, 1);
        }

        void AllocateTemporaryTexture()
        {
            data.tmpTexture = new RenderTexture(data.volumeTexture.width, data.volumeTexture.height, 0,
                data.volumeTexture.format);
            data.tmpTexture.wrapMode = TextureWrapMode.Clamp;
            data.tmpTexture.filterMode = FilterMode.Point;
            data.tmpTexture.enableRandomWrite = true;
            data.tmpTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            data.tmpTexture.volumeDepth = data.volumeTexture.volumeDepth;
            data.tmpTexture.Create();
        }

        void UpdateTimeVolumeTexture()
        {
            // 1. clear temporary

            compute.SetTexture(2, "_VolumeTexture", data.tmpTexture);

            compute.Dispatch(2, capacities.x / 8 + 1, capacities.y / 8 + 1, capacities.z / 8 + 1);


            // 2. copy current volume to temporary

            compute.SetTexture(0, "_VolumeTexture", data.tmpTexture);
            compute.SetBuffer(0, "_Matrices", data.matricesBuffer);
            compute.SetBuffer(0, "_LocalGridPositions", data.localGridPositionsBuffer);
            compute.SetBuffer(0, "_MatrixIndices", data.matrixIndicesBuffer);
            compute.SetInt("_MatrixCount", targets.Length);
            compute.SetInt("_LocalGridPositionCount", data.localGridPositionsBuffer.count);
            compute.SetInts("_VolumeCapacities", new int[] { capacities.x, capacities.y, capacities.z });
            compute.SetFloat("_Resolution", resolution);
            compute.SetVector("_Size", size);
            compute.SetVector("_Anchor", transform.position - size * 0.5f);

            compute.Dispatch(0, data.localGridPositionsBuffer.count / 1024, 1, 1);


            // 3. add temporary to time volume using deltatime

            compute.SetTexture(1, "_VolumeTexture", data.volumeTexture);
            compute.SetTexture(1, "_TmpTexture", data.tmpTexture);
            compute.SetFloat("_DeltaTime", Time.fixedDeltaTime);
            compute.SetInts("_VolumeCapacities", new int[] { capacities.x, capacities.y, capacities.z });

            compute.Dispatch(1, capacities.x / 8 + 1, capacities.y / 8 + 1, capacities.z / 8 + 1);
        }


        public void Release()
        {
            data.Release();
        }
    }

    public class VolumeTrackerGPUData
    {
        public RenderTexture volumeTexture;

        public ComputeBuffer matricesBuffer;
        public ComputeBuffer localGridPositionsBuffer;
        public ComputeBuffer matrixIndicesBuffer;

        public RenderTexture tmpTexture;


        public void Release()
        {
            volumeTexture.Release();

            matricesBuffer.Release();
            matricesBuffer.Dispose();

            localGridPositionsBuffer.Release();
            localGridPositionsBuffer.Dispose();

            matrixIndicesBuffer.Release();
            matrixIndicesBuffer.Dispose();

            if (tmpTexture != null && tmpTexture.IsCreated())
            {
                tmpTexture.Release();
            }
        }
    }
}