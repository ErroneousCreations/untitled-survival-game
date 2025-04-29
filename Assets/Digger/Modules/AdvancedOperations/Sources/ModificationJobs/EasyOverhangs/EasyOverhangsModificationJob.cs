using System;
using Digger.Modules.Core.Sources;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.AdvancedOperations.Sources.ModificationJobs.EasyOverhangs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct EasyOverhangsModificationJob : IJobParallelFor
    {
        public int SizeVox;
        public int SizeVox2;
        public float3 HeightmapScale;
        public float3 Center;
        public float Radius;
        public float3 ChunkWorldPosition;
        public uint TextureIndex;
        public float Intensity;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float> Heights;

        public NativeArray<Voxel> Voxels;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> Holes;
        
        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float3> Normals;

        public void Execute(int index)
        {
            var pi = Utils.IndexToXYZ(index, SizeVox, SizeVox2);
            var p = pi * HeightmapScale;
            var wp = p + ChunkWorldPosition;
            var terrainHeight = Heights[Utils.XYZToHeightIndex(pi, SizeVox)];
            var terrainHeightValue = wp.y - terrainHeight;

            var d = ComputeSphereDistance(p);
            if (d < 0) {
                return;
            }
            
            var voxel = Voxels[index];
            var nrm = Normals[Utils.XZToNormalIndex(pi.x, pi.z, SizeVox)];

            var newValue = math.max(voxel.Value, voxel.Value  + math.lerp(Intensity * 2f, 0, math.clamp(nrm.y, 0, 1)));
            voxel.Value = math.lerp(voxel.Value, newValue, math.clamp(d, 0f, 1f));
            voxel.Alteration = Voxel.FarAboveSurface;
            voxel.AddTexture(TextureIndex, 1f);

            if (voxel.Alteration != Voxel.Unaltered) {
                voxel = Utils.AdjustAlteration(voxel, pi, HeightmapScale.y, wp.y, terrainHeightValue, SizeVox, Heights);
            }

            if (voxel.IsAlteredNearBelowSurface || voxel.IsAlteredNearAboveSurface) {
                Core.Sources.NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x, pi.z, SizeVox));
                if (pi.x >= 1) {
                    Core.Sources.NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x - 1, pi.z, SizeVox));
                    if (pi.z >= 1) {
                        Core.Sources.NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x - 1, pi.z - 1, SizeVox));
                    }
                }

                if (pi.z >= 1) {
                    Core.Sources.NativeCollections.Utils.IncrementAt(Holes, Utils.XZToHoleIndex(pi.x, pi.z - 1, SizeVox));
                }
            }

            Voxels[index] = voxel;
        }
        
        private float ComputeSphereDistance(float3 p)
        {
            var vec = p - Center;
            var distance = (float)Math.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
            return Radius - distance;
        }
    }
}