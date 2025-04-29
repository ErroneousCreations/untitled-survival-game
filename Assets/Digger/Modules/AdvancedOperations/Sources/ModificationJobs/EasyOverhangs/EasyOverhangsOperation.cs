using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Operations;
using Unity.Collections;
using Unity.Mathematics;

namespace Digger.Modules.AdvancedOperations.Sources.ModificationJobs.EasyOverhangs
{
    public class EasyOverhangsOperation : IOperation<EasyOverhangsModificationJob>
    {
        public float3 Position;
        public uint TextureIndex;
        public float Opacity;
        public float Size;

        public ModificationArea GetAreaToModify(DiggerSystem digger)
        {
            return ModificationAreaUtils.GetSphericalAreaToModify(digger, Position, Size);
        }

        public EasyOverhangsModificationJob Do(VoxelChunk chunk)
        {
            var job = new EasyOverhangsModificationJob
            {
                SizeVox = chunk.SizeVox,
                SizeVox2 = chunk.SizeVox * chunk.SizeVox,
                HeightmapScale = chunk.HeightmapScale,
                ChunkWorldPosition = chunk.WorldPosition,
                Voxels = new NativeArray<Voxel>(chunk.VoxelArray, Allocator.Persistent),
                Heights = new NativeArray<float>(chunk.HeightArray, Allocator.Persistent),
                Holes = new NativeArray<int>(chunk.HolesArray, Allocator.Persistent),
                Normals = new NativeArray<float3>(chunk.NormalArray, Allocator.Persistent),
                Center = Position - chunk.AbsoluteWorldPosition,
                Radius = Size,
                TextureIndex = TextureIndex,
                Intensity = Opacity
            };
            return job;
        }

        public void Complete(EasyOverhangsModificationJob job, VoxelChunk chunk)
        {
            job.Voxels.CopyTo(chunk.VoxelArray);
            job.Voxels.Dispose();
            job.Heights.Dispose();
            job.Normals.Dispose();

            chunk.Cutter.Cut(job.Holes, chunk.VoxelPosition, chunk.ChunkPosition);
            job.Holes.Dispose();
        }
    }
}