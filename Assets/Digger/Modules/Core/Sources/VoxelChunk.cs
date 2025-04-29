using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Digger.Modules.Core.Sources.Jobs;
using Digger.Modules.Core.Sources.Polygonizers;
using Digger.Modules.Core.Sources.TerrainInterface;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Digger.Modules.Core.Sources
{
    public class VoxelChunk : MonoBehaviour
    {
        [SerializeField] private DiggerSystem digger;
        [SerializeField] private int sizeVox;
        [SerializeField] private int sizeOfMesh;
        [SerializeField] private Vector3i chunkPosition;
        [SerializeField] private Vector3i voxelPosition;
        [SerializeField] private Vector3 worldPosition;

        [NonSerialized] private Voxel[] voxelArray;
        [NonSerialized] private float[] heightArray;
        [NonSerialized] private float3[] normalArray;
        [NonSerialized] private float[] alphamapArray;
        [NonSerialized] private int3 alphamapArraySize;
        [NonSerialized] private int2 alphamapArrayOrigin;

        [NonSerialized] private Voxel[] voxelArrayBeforeOperation;

        [NonSerialized] private JobHandle? currentJobHandle;
        [NonSerialized] private IJobParallelFor currentJob;
        [NonSerialized] private NativeArray<Voxel> voxels;
        [NonSerialized] private NativeArray<float> heights;
        [NonSerialized] private NativeArray<int> holes;
        [NonSerialized] private NativeParallelHashSet<int> chunkOnSurfaceY;
        [NonSerialized] private readonly Dictionary<int, IPolygonizer> polygonizersPerLod = new();
        [NonSerialized] private int needToBakePhysicMeshInstanceID;

        private IPolygonizer GetPolygonizer(int lod)
        {
            if (polygonizersPerLod.TryGetValue(lod, out var polygonizer)) return polygonizer;
            polygonizer = digger.PolygonizerProvider ? digger.PolygonizerProvider.NewPolygonizer(digger) : new MarchingCubesPolygonizer();
            polygonizersPerLod.Add(lod, polygonizer);
            return polygonizer;
        }
        
        
        public bool IsLoaded => VoxelArray is { Length: > 0 };
        public Vector3i ChunkPosition => chunkPosition;
        public Vector3i VoxelPosition => voxelPosition;
        private float Altitude => voxelPosition.y * digger.HeightmapScale.y;
        public float3 WorldPosition => worldPosition;
        public float3 AbsoluteWorldPosition => digger.transform.TransformPoint(worldPosition);
        public int3 AbsoluteVoxelPosition => Utils.UnityToVoxelPosition(digger.transform.TransformPoint(worldPosition), HeightmapScale);
        public int SizeVox => sizeVox;
        public int SizeOfMesh => sizeOfMesh;
        public float3 HeightmapScale => digger.HeightmapScale;
        public Voxel[] VoxelArray => voxelArray;
        public float[] HeightArray => heightArray;
        public float3[] NormalArray => normalArray;
        public float[] AlphamapArray => alphamapArray;
        public int[] HolesArray => digger.Cutter.GetHoles(chunkPosition, voxelPosition);
        public float3 CutMargin => digger.CutMargin;
        public TerrainCutter Cutter => digger.Cutter;
        public DiggerSystem Digger => digger;
        public int3 AlphamapArraySize => alphamapArraySize;
        public int2 AlphamapArrayOrigin => alphamapArrayOrigin;

        internal static VoxelChunk Create(DiggerSystem digger, Chunk chunk)
        {
            Utils.Profiler.BeginSample("VoxelChunk.Create");
            var go = new GameObject("VoxelChunk")
            {
                hideFlags = HideFlags.DontSaveInBuild,
                transform =
                {
                    parent = chunk.transform,
                    localPosition = Vector3.zero,
                    localRotation = Quaternion.identity,
                    localScale = Vector3.one
                }
            };
            var voxelChunk = go.AddComponent<VoxelChunk>();
            voxelChunk.digger = digger;
            voxelChunk.sizeVox = digger.SizeVox;
            voxelChunk.sizeOfMesh = digger.SizeOfMesh;
            voxelChunk.chunkPosition = chunk.ChunkPosition;
            voxelChunk.voxelPosition = chunk.VoxelPosition;
            voxelChunk.worldPosition = chunk.WorldPosition;
            voxelChunk.Load();

            Utils.Profiler.EndSample();
            return voxelChunk;
        }

        private static void GenerateVoxels(DiggerSystem digger, float[] heightArray, float chunkAltitude,
            ref Voxel[] voxelArray)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.GenerateVoxels");
            var sizeVox = digger.SizeVox;
            voxelArray ??= new Voxel[sizeVox * sizeVox * sizeVox];

            var heights = new NativeArray<float>(heightArray, Allocator.TempJob);
            var voxels = new NativeArray<Voxel>(sizeVox * sizeVox * sizeVox, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            // Set up the job data
            var jobData = new VoxelGenerationJob
            {
                ChunkAltitude = chunkAltitude,
                Heights = heights,
                Voxels = voxels,
                SizeVox = sizeVox,
                SizeVox2 = sizeVox * sizeVox,
                HeightmapScale = digger.HeightmapScale,
            };

            // Schedule the job
            var handle = jobData.Schedule(voxels.Length, 64);

            // Wait for the job to complete
            handle.Complete();

            voxels.CopyTo(voxelArray);
            heights.Dispose();
            voxels.Dispose();

            Utils.Profiler.EndSample();
        }

        public void RefreshVoxels()
        {
            Utils.Profiler.BeginSample("VoxelChunk.RefreshVoxels");
            if (VoxelArray == null)
                return;

            heights = new NativeArray<float>(HeightArray, Allocator.TempJob);
            voxels = new NativeArray<Voxel>(VoxelArray, Allocator.TempJob);
            holes = new NativeArray<int>(digger.Cutter.GetHoles(chunkPosition, voxelPosition), Allocator.TempJob);

            // Set up the job data
            var jobData = new VoxelRefreshJob()
            {
                ChunkAltitude = Altitude,
                Heights = heights,
                Holes = holes,
                Voxels = voxels,
                SizeVox = SizeVox,
                SizeVox2 = SizeVox * SizeVox,
                HeightmapScale = digger.HeightmapScale,
            };

            // Schedule the job
            var handle = jobData.Schedule(voxels.Length, 64);

            // Wait for the job to complete
            handle.Complete();

            voxels.CopyTo(VoxelArray);
            heights.Dispose();
            voxels.Dispose();
            holes.Dispose();

            Utils.Profiler.EndSample();
        }

        public void PrepareOperationJob<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            var job = operation.Do(this);
            currentJob = job;
        }
        
        public void ScheduleOperationJob<T>() where T : struct, IJobParallelFor
        {
            currentJobHandle = ((T)currentJob).Schedule(VoxelArray.Length, digger.SizeVox);
        }

        public void CompleteOperation<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            CompleteBackgroundJob();
            operation.Complete((T)currentJob, this);
            RecordUndoIfNeeded();
            digger.EnsureChunkWillBePersisted(this);
        }


        public void UpdateVoxelsOnSurface()
        {
            if (VoxelArray == null)
                return;

            heights = new NativeArray<float>(HeightArray, Allocator.Persistent);
            voxels = new NativeArray<Voxel>(VoxelArray, Allocator.Persistent);
            var cutter = digger.Cutter;
            holes = new NativeArray<int>(cutter.GetHoles(chunkPosition, voxelPosition), Allocator.Persistent);

            // Set up the job data
            var jobData = new VoxelFillSurfaceJob()
            {
                ChunkAltitude = Altitude,
                Heights = heights,
                Voxels = voxels,
                Holes = holes,
                SizeVox = SizeVox,
                SizeVox2 = SizeVox * SizeVox,
                HeightmapScale = digger.HeightmapScale,
            };

            // Schedule the job
            currentJobHandle = jobData.Schedule(voxels.Length, 64);
        }

        public void CompleteUpdateVoxelsOnSurface()
        {
            CompleteBackgroundJob();
            voxels.CopyTo(VoxelArray);
            heights.Dispose();
            voxels.Dispose();
            holes.Dispose();
        }
        
        public void GetSurfaceChunksOnHoles()
        {
            if (VoxelArray == null)
                return;

            heights = new NativeArray<float>(HeightArray, Allocator.Persistent);
            chunkOnSurfaceY = new NativeParallelHashSet<int>(100, Allocator.Persistent);
            var cutter = digger.Cutter;
            holes = new NativeArray<int>(cutter.GetHoles(chunkPosition, voxelPosition), Allocator.Persistent);

            // Set up the job data
            var jobData = new GetSurfaceChunksJob()
            {
                ChunkOnSurfaceY = chunkOnSurfaceY.AsParallelWriter(),
                Heights = heights,
                Holes = holes,
                SizeVox = SizeVox,
                SizeOfMesh = SizeOfMesh,
                HeightmapScaleY = digger.HeightmapScale.y,
            };

            // Schedule the job
            currentJobHandle = jobData.Schedule(holes.Length, 64);
        }
        
        private void CompleteBackgroundJob()
        {
            if (!currentJobHandle.HasValue)
                return;
            currentJobHandle.Value.Complete();
            currentJobHandle = null;
        }
        
        private void CompleteJobSync()
        {
            if (!currentJobHandle.HasValue)
                return;
            currentJobHandle.Value.Complete();
            currentJobHandle = null;
        }

        public HashSet<int3> CompleteGetSurfaceChunksOnHoles()
        {
            CompleteBackgroundJob();
            var result = new HashSet<int3>();
            foreach (var chunkY in chunkOnSurfaceY)
            {
                result.Add(new int3(chunkPosition.x, chunkY, chunkPosition.z));
            }
            chunkOnSurfaceY.Dispose();
            heights.Dispose();
            holes.Dispose();
            return result;
        }

        public bool HasAlteredVoxels()
        {
            return VoxelArray != null && VoxelArray.Any(voxel => voxel.Alteration != Voxel.Unaltered);
        }

        public void BuildMesh(int lod)
        {
            currentJobHandle = GetPolygonizer(lod).BuildMesh(this, lod);
        }
        
        public bool BuildMeshSync(int lod, Mesh mesh)
        {
            currentJobHandle = GetPolygonizer(lod).BuildMesh(this, lod);
            CompleteJobSync();
            return GetPolygonizer(lod).CompleteBuildMesh(mesh, Digger.GetChunkBounds());
        }
        
        public void CompleteBuildMeshJob()
        {
            CompleteBackgroundJob();
        }
        
        public void CompleteBuildMesh(Mesh mesh, int lod)
        {
            needToBakePhysicMeshInstanceID = GetPolygonizer(lod).CompleteBuildMesh(mesh, Digger.GetChunkBounds()) ? 
                mesh.GetInstanceID() : 0;
        }

        public void BakePhysicMesh()
        {
            if (needToBakePhysicMeshInstanceID == 0)
                return;
            var job = new PhysicsBakeMeshJob
            {
                MeshInstanceId = needToBakePhysicMeshInstanceID
            };
            currentJobHandle = job.Schedule();
        }
        
        public void CompleteBakePhysicMesh()
        {
            CompleteBackgroundJob();
        }

        private void RecordUndoIfNeeded()
        {
#if UNITY_EDITOR
            if (VoxelArray == null || VoxelArray.Length == 0) {
                Debug.LogError("Voxel array should not be null when recording undo");
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.RecordUndoIfNeeded");
            var path = digger.GetEditorOnlyPathVoxelFile(chunkPosition);

            var savePath = digger.GetPathVersionedVoxelFile(chunkPosition, digger.PreviousVersion);
            if (File.Exists(path) && !File.Exists(savePath)) {
                File.Copy(path, savePath);
            }
            Utils.Profiler.EndSample();
#endif
        }

        public void Persist()
        {
            if (VoxelArray == null || VoxelArray.Length == 0) {
                Debug.LogError("Voxel array should not be null in saving");
                return;
            }

            Utils.Profiler.BeginSample("[Dig] VoxelChunk.Persist");
            var path = digger.GetPathVoxelFile(chunkPosition, true);

            var voxelsToPersist = new NativeArray<Voxel>(VoxelArray, Allocator.Temp);
            var bytes = new NativeSlice<Voxel>(voxelsToPersist).SliceConvert<byte>();
            File.WriteAllBytes(path, bytes.ToArray());
            voxelsToPersist.Dispose();

#if UNITY_EDITOR
            var savePath = digger.GetPathVersionedVoxelFile(chunkPosition, digger.Version);
            File.Copy(path, savePath, true);
#endif
            Utils.Profiler.EndSample();
        }

        public void Load()
        {
            Utils.Profiler.BeginSample("VoxelChunk.Load");
            // Feed heights again in case they have been modified
            heightArray = digger.HeightsFeeder.GetHeights(chunkPosition, voxelPosition);
            normalArray = digger.NormalsFeeder.GetNormals(chunkPosition, voxelPosition);
            var alphamapsInfo = digger.AlphamapsFeeder.GetAlphamaps(chunkPosition, worldPosition, SizeOfMesh);
            alphamapArray = alphamapsInfo.AlphamapArray;
            alphamapArraySize = alphamapsInfo.AlphamapArraySize;
            alphamapArrayOrigin = alphamapsInfo.AlphamapArrayOrigin;

            var path = digger.GetPathVoxelFile(chunkPosition, false);
            var rawBytes = Utils.GetBytes(path);

            if (rawBytes == null) {
                if (VoxelArray == null) {
                    // If there is no persisted voxels but voxel array is null, then we fall back and (re)generate them.
                    GenerateVoxels(digger, HeightArray, Altitude, ref voxelArray);
                    digger.EnsureChunkWillBePersisted(this);
                }

                Utils.Profiler.EndSample();
                return;
            }

            ReadVoxelFile(SizeVox, rawBytes, ref voxelArray);
            Utils.Profiler.EndSample();
        }

        public void InitVoxelArrayBeforeOperation()
        {
            voxelArrayBeforeOperation = new Voxel[VoxelArray.Length];
            Array.Copy(VoxelArray, voxelArrayBeforeOperation, VoxelArray.Length);
        }

        internal void ResetVoxelArrayBeforeOperation()
        {
            voxelArrayBeforeOperation = null;
        }

        private static void ReadVoxelFile(int sizeVox, byte[] rawBytes, ref Voxel[] voxelArray)
        {
            voxelArray ??= new Voxel[sizeVox * sizeVox * sizeVox];

            var voxelBytes = new NativeArray<byte>(rawBytes, Allocator.Temp);
            var bytes = new NativeSlice<byte>(voxelBytes);
            var voxelSlice = bytes.SliceConvert<Voxel>();
            DirectNativeCollectionsAccess.CopyTo(voxelSlice, voxelArray);
            voxelBytes.Dispose();
        }

        public static NativeArray<Voxel> LoadVoxels(DiggerSystem digger, Vector3i chunkPosition)
        {
            Utils.Profiler.BeginSample("[Dig] VoxelChunk.LoadVoxels");

            if (!digger.IsChunkBelongingToMe(chunkPosition)) {
                var neighbor = digger.GetNeighborAt(chunkPosition);
                if (neighbor) {
                    var neighborChunkPosition = neighbor.ToChunkPosition(digger.ToWorldPosition(chunkPosition));
                    if (!neighbor.IsChunkBelongingToMe(neighborChunkPosition)) {
                        Debug.LogError(
                            $"neighborChunkPosition {neighborChunkPosition} should always belong to neighbor");
                        return new NativeArray<Voxel>(1, Allocator.Persistent);
                    }

                    return LoadVoxels(neighbor, neighborChunkPosition);
                }
            }

            if (digger.GetChunk(chunkPosition, out var chunk)) {
                if (chunk.VoxelChunk.voxelArrayBeforeOperation != null) {
                    return new NativeArray<Voxel>(chunk.VoxelChunk.voxelArrayBeforeOperation, Allocator.Persistent);
                }

                chunk.LazyLoad();
                return new NativeArray<Voxel>(chunk.VoxelChunk.VoxelArray, Allocator.Persistent);
            }

            return new NativeArray<Voxel>(1, Allocator.Persistent);
        }
    }
}