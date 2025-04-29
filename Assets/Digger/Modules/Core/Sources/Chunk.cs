using System.Collections.Generic;
using System.Globalization;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace Digger.Modules.Core.Sources
{
    public class Chunk : MonoBehaviour
    {
        [SerializeField] private DiggerSystem digger;
        [SerializeField] private ChunkLODGroup chunkLodGroup;
        [SerializeField] private VoxelChunk voxelChunk;
        [SerializeField] private Vector3i chunkPosition;
        [SerializeField] private Vector3i voxelPosition;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private Vector3 sizeInWorld;
        [SerializeField] private bool hasVisualMesh;

        public Vector3i ChunkPosition => chunkPosition;
        public Vector3i VoxelPosition => voxelPosition;
        public Vector3 WorldPosition => worldPosition;
        public DiggerSystem Digger => digger;
        public bool HasVisualMesh => hasVisualMesh;

        internal VoxelChunk VoxelChunk => voxelChunk;

        private bool IsLoaded => voxelChunk != null && voxelChunk.IsLoaded;

        internal NavMeshBuildSource NavMeshBuildSource =>
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                area = digger.DefaultNavMeshArea,
                transform = transform.localToWorldMatrix,
                sourceObject = chunkLodGroup.GetMeshForNavigation(),
                component = this,
                size = digger.GetChunkBounds().size
            };

        public static string GetName(Vector3i chunkPosition)
        {
            return $"Chunk_{chunkPosition.x}_{chunkPosition.y}_{chunkPosition.z}";
        }

        public static Vector3i GetPositionFromName(string chunkName)
        {
            var coords = chunkName.Replace("Chunk_", "").Replace($".{DiggerSystem.VoxelFileExtension}", "").Split('_');
            return new Vector3i(int.Parse(coords[0], CultureInfo.InvariantCulture),
                                int.Parse(coords[1], CultureInfo.InvariantCulture),
                                int.Parse(coords[2], CultureInfo.InvariantCulture));
        }

        internal static Chunk CreateChunk(Vector3i chunkPosition,
                                          DiggerSystem digger,
                                          Terrain terrain,
                                          Material[] materials,
                                          int layer,
                                          string tag)
        {
            Utils.Profiler.BeginSample("CreateChunk");
            var voxelPosition = GetVoxelPosition(digger, chunkPosition);
            var worldPosition = (Vector3) voxelPosition;
            worldPosition.x *= digger.HeightmapScale.x;
            worldPosition.y *= digger.HeightmapScale.y;
            worldPosition.z *= digger.HeightmapScale.z;

            var go = new GameObject(GetName(chunkPosition))
            {
                layer = layer,
                hideFlags = digger.ShowDebug ? HideFlags.None : HideFlags.HideInHierarchy | HideFlags.HideInInspector,
                transform =
                {
                    parent = digger.transform,
                    localPosition = worldPosition + Vector3.up * 0.001f,
                    localRotation = Quaternion.identity,
                    localScale = Vector3.one
                }
            };

            var chunk = go.AddComponent<Chunk>();
            chunk.digger = digger;
            chunk.chunkPosition = chunkPosition;
            chunk.voxelPosition = voxelPosition;
            chunk.worldPosition = worldPosition;
            chunk.sizeInWorld = digger.SizeOfMesh * digger.HeightmapScale;

            chunk.voxelChunk = VoxelChunk.Create(digger, chunk);
            chunk.chunkLodGroup = ChunkLODGroup.Create(chunkPosition, chunk, digger, terrain, materials, layer, tag);
            chunk.UpdateStaticEditorFlags();

            Utils.Profiler.EndSample();
            return chunk;
        }

        public void UpdateStaticEditorFlags()
        {
#if UNITY_EDITOR
            if (chunkLodGroup)
                chunkLodGroup.UpdateStaticEditorFlags(digger.EnableOcclusionCulling, digger.EnableContributeGI);
#endif
        }

#if UNITY_EDITOR
        public void SaveMeshesAsAssets()
        {
            if (chunkLodGroup)
                chunkLodGroup.SaveMeshesAsAssets(digger);
        }
        
        public void GenerateSecondaryUVSet()
        {
            if (chunkLodGroup)
                chunkLodGroup.GenerateSecondaryUVSet();
        }
#endif
        
        public void PrepareOperationJob<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            voxelChunk.PrepareOperationJob(operation);
        }
        
        public void ScheduleOperationJob<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            voxelChunk.ScheduleOperationJob<T>();
        }

        public void CompleteOperation<T>(IOperation<T> operation) where T : struct, IJobParallelFor
        {
            voxelChunk.CompleteOperation(operation);
        }
        
        internal void BuildVisualMesh(int lod)
        {
            voxelChunk.BuildMesh(lod);
        }
        
        internal void CompleteBuildVisualMeshJob()
        {
            voxelChunk.CompleteBuildMeshJob();
        }
        
        internal void CompleteBuildVisualMesh(int lod, int lodIndex)
        {
            var nextMesh = chunkLodGroup.NextMesh(lodIndex);
            voxelChunk.CompleteBuildMesh(nextMesh, lod);
        }
        
        internal void BakePhysicMesh()
        {
            voxelChunk.BakePhysicMesh();
        }
        
        internal void CompleteBakePhysicMesh()
        {
            voxelChunk.CompleteBakePhysicMesh();
        }
        
        internal void UpdateVoxelsOnSurface()
        {
            voxelChunk.UpdateVoxelsOnSurface();
        }

        internal void CompleteUpdateVoxelsOnSurface()
        {
            voxelChunk.CompleteUpdateVoxelsOnSurface();
        }
        
        internal void GetSurfaceChunksOnHoles()
        {
            voxelChunk.GetSurfaceChunksOnHoles();
        }

        internal HashSet<int3> CompleteGetSurfaceChunksOnHoles()
        {
            return voxelChunk.CompleteGetSurfaceChunksOnHoles();
        }

        internal void ApplyModify()
        {
            for (var lodIndex = 0; lodIndex < chunkLodGroup.LODCount; ++lodIndex) {
                var nextMesh = chunkLodGroup.NextMesh(lodIndex);
                var res = chunkLodGroup.PostBuild(lodIndex, nextMesh);
                if (lodIndex == 0)
                    hasVisualMesh = res;
            }
            ResetVoxelArrayBeforeOperation();
        }

        public bool LoadVoxels(bool syncVoxelsWithTerrain)
        {
            var newVoxelChunk = false;
            if (!voxelChunk) {
                voxelChunk = GetComponentInChildren<VoxelChunk>();
                if (!voxelChunk) {
                    voxelChunk = VoxelChunk.Create(digger, this);
                    newVoxelChunk = true;
                }
            }

            voxelChunk.Load();
            if (syncVoxelsWithTerrain) {
                voxelChunk.RefreshVoxels();
            }

            return newVoxelChunk;
        }

        public void RebuildMeshes()
        {
            for (var lodIndex = 0; lodIndex < chunkLodGroup.LODCount; ++lodIndex) {
                var lod = ChunkLODGroup.IndexToLod(lodIndex);
                var nextMesh = chunkLodGroup.NextMesh(lodIndex);
                if (voxelChunk.BuildMeshSync(lod, nextMesh)) {
                    voxelChunk.BakePhysicMesh();
                }

                var res = chunkLodGroup.PostBuild(lodIndex,lodIndex == digger.ColliderLodIndex || chunkLodGroup.LODCount == 1);
                if (lodIndex == 0)
                    hasVisualMesh = res;
            }
        }

        private static Vector3i GetVoxelPosition(DiggerSystem digger, Vector3i chunkPosition)
        {
            return chunkPosition * digger.SizeOfMesh;
        }

        internal void ResetVoxelArrayBeforeOperation()
        {
            voxelChunk.ResetVoxelArrayBeforeOperation();
        }

        internal void LazyLoad()
        {
            if (IsLoaded)
                return;

            Utils.D.Log($"LazyLoad of {this.name}");
            if (!voxelChunk) {
                voxelChunk = GetComponentInChildren<VoxelChunk>();
                if (!voxelChunk) {
                    Debug.LogError(
                        $"VoxelChunk component is missing from Chunk children. Chunk {name} is in incoherent state. " +
                        "Creating a new VoxelChunk to fix this...");
                    voxelChunk = VoxelChunk.Create(digger, this);
                }
            }

            LoadVoxels(false);
        }
    }
}