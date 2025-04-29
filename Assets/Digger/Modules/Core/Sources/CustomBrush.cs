using System;
using Digger.Modules.Core.Sources.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Digger.Modules.Core.Sources
{
    [AddComponentMenu("Digger/Custom Brush")]
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class CustomBrush : MonoBehaviour
    {
        [SerializeField] private string id;
        [NonSerialized] private Voxel[] inputVoxels;
        [NonSerialized] private int3 inputSizeVox;
        [NonSerialized] private int3 inputOriginVox;

        [NonSerialized] private Mesh usedMesh;
        [NonSerialized] private float3 usedRotation;
        [NonSerialized] private float3 usedScale;
        
        [SerializeField] public bool autoRefresh = true;

        public string Id => id;
        public Voxel[] InputVoxels => inputVoxels;
        public int3 InputSizeVox => inputSizeVox;
        public int3 InputOriginVox => inputOriginVox;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString();
        }

        private void Update()
        {
            if (autoRefresh && usedMesh != GetComponent<MeshFilter>().sharedMesh ||
                !Utils.Approximately(usedRotation, new float3(transform.localEulerAngles)) ||
                !Utils.Approximately(usedScale, new float3(transform.localScale)))
                ComputeVoxels();
        }

        public void ComputeVoxels()
        {
            var digger = FindFirstObjectByType<DiggerSystem>();
            if (!digger)
                return;
            var terrainData = digger.Terrain.terrainData;
            var diggerScale = new float3(1, 1, 1) * digger.ResolutionMult / terrainData.heightmapScale.x;
            if (!digger.AutoVoxelHeight)
                diggerScale.y = 1f / digger.VoxelHeight;

            usedMesh = GetComponent<MeshFilter>().sharedMesh;
            usedRotation = new float3(transform.localEulerAngles);
            usedScale = new float3(transform.localScale);

            var vertices = new NativeArray<float3>(usedMesh.vertexCount, Allocator.TempJob);
            var bounds = new Bounds();
            var objPos = transform.position;
            transform.position = Vector3.zero;
            for (int i = 0; i < usedMesh.vertices.Length; i++)
            {
                vertices[i] = transform.TransformPoint(usedMesh.vertices[i]) * diggerScale;
                bounds.Encapsulate(vertices[i]);
            }
            transform.position = objPos;

            var triangles = new NativeArray<ushort>(usedMesh.triangles.Length, Allocator.TempJob);
            for (int i = 0; i < usedMesh.triangles.Length; i++)
            {
                triangles[i] = (ushort)usedMesh.triangles[i];
            }

            var size = new int3((int3)math.round(bounds.size) + new int3(4, 4, 4));
            inputSizeVox = size;
            inputOriginVox = size / 2;

            var voxels = new NativeArray<Voxel>(size.x * size.y * size.z, Allocator.TempJob);

            var customBrushJob = new MeshToVoxelsJob
            {
                SizeVox = size,
                Origin = inputOriginVox,
                Vertices = vertices,
                Triangles = triangles,
                Voxels = voxels
            };

            // Schedule the job
            var handle = customBrushJob.Schedule(voxels.Length, 64);

            // Wait for the job to complete
            handle.Complete();
            inputVoxels = voxels.ToArray();
            voxels.Dispose();
            vertices.Dispose();
            triangles.Dispose();
        }
    }
}