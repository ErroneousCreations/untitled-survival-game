using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.NativeCollections;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Digger.Modules.Core.Sources.Polygonizers
{
    public class MarchingCubesPolygonizer : IPolygonizer
    {
        private readonly byte isLowPolyStyle;
        private PolyOut mcOut;
        private NativeArray<Voxel> voxels;
        private NativeArray<float3> normals;
        private NativeArray<float> alphamaps;
        private NativeCounter vertexCounter;

        public MarchingCubesPolygonizer(bool lowPolyStyle = false)
        {
            this.isLowPolyStyle = (byte)(lowPolyStyle ? 1 : 0);
        }

        public JobHandle BuildMesh(VoxelChunk chunk, int lod)
        {
            mcOut = PolyOut.New();
            var edgeTable = NativeCollectionsPool.Instance.GetMCEdgeTable();
            var triTable = NativeCollectionsPool.Instance.GetMCTriTable();
            var corners = NativeCollectionsPool.Instance.GetMCCorners();
            voxels = new NativeArray<Voxel>(chunk.VoxelArray, Allocator.Persistent);
            normals = new NativeArray<float3>(chunk.NormalArray, Allocator.Persistent);
            alphamaps = new NativeArray<float>(chunk.AlphamapArray, Allocator.Persistent);
            vertexCounter = new NativeCounter(Allocator.Persistent, 3);

            var alphamapsSize = chunk.Digger.AlphamapsSize;
            var uvScale = chunk.Digger.UVScale;
            var scale = new float3(chunk.Digger.HeightmapScale); // { y = 1f };

            // for retro-compatibility
            if (lod <= 0) lod = 1;

            // Set up the job data
            var jobData = new MarchingCubesJob(edgeTable,
                triTable,
                corners,
                vertexCounter.ToConcurrent(),
                voxels,
                normals,
                alphamaps,

                // out params
                mcOut,

                // misc
                scale,
                uvScale,
                chunk.WorldPosition,
                lod,
                chunk.AlphamapArrayOrigin,
                alphamapsSize,
                chunk.AlphamapArraySize,
                chunk.Digger.MaterialType);

            jobData.SizeVox = chunk.SizeVox;
            jobData.SizeVox2 = chunk.SizeVox * chunk.SizeVox;
            jobData.Isovalue = 0f;
            jobData.AlteredOnly = 1;
            jobData.FullOutput = 1;
            jobData.IsBuiltInHDRP = (byte)(chunk.Digger.MaterialType == TerrainMaterialType.HDRP ? 1 : 0);
            jobData.IsLowPolyStyle = isLowPolyStyle;


            // Schedule the job
            return jobData.Schedule(voxels.Length, 4);
        }
        
        public bool CompleteBuildMesh(Mesh mesh, Bounds bounds)
        {
            var vertexCount = vertexCounter.Count;
            mcOut.vertexCount = vertexCount;
            mcOut.triangleCount = vertexCount;
            
            var hasMesh = mcOut.TransferVertexData(mesh, bounds);

            voxels.Dispose();
            normals.Dispose();
            alphamaps.Dispose();
            vertexCounter.Dispose();
            mcOut.Dispose();
            return hasMesh;
        }
    }
}