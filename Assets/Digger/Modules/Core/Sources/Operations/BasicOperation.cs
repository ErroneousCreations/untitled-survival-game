using Digger.Modules.Core.Sources.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Digger.Modules.Core.Sources.Operations
{
    public class BasicOperation : IOperation<VoxelModificationJob>
    {
        public ModificationParameters Params;

        public ModificationArea GetAreaToModify(DiggerSystem digger)
        {
            var action = Params.Action;
            if (action != ActionType.Paint && action != ActionType.PaintHoles && Params.Opacity < 0f)
            {
                Debug.LogWarning("Opacity can only be negative when action type is 'Paint' or 'PaintHoles'");
                return new ModificationArea
                {
                    NeedsModification = false
                };
            }

            if (Params.Brush == BrushType.Custom && Params.CustomBrush)
            {
                return ModificationAreaUtils.GetAABBAreaToModify(digger, Params.Position, math.ceil(Params.CustomBrush.InputSizeVox * Params.Size));
            }

            return ModificationAreaUtils.GetSphericalAreaToModify(digger, Params.Position, math.max(math.max(Params.Size.x, Params.Size.y), Params.Size.z));
        }

        public VoxelModificationJob Do(VoxelChunk chunk)
        {
            var inputVoxels = new NativeArray<Voxel>(0, Allocator.Persistent);
            var inputSizeVox = new int3(0, 0, 0);
            var inputOriginVox = new int3(0, 0, 0);

            if (Params.Brush == BrushType.Custom && Params.CustomBrush && Params.CustomBrush.InputVoxels != null)
            {
                inputVoxels.Dispose();
                inputVoxels = new NativeArray<Voxel>(Params.CustomBrush.InputVoxels, Allocator.Persistent);
                inputSizeVox = Params.CustomBrush.InputSizeVox;
                inputOriginVox = Utils.UnityToVoxelPosition(Params.Position, chunk.HeightmapScale) - chunk.AbsoluteVoxelPosition - (int3)math.round(Params.CustomBrush.InputOriginVox * Params.Size);
            }

            var job = new VoxelModificationJob
            {
                SizeVox = chunk.SizeVox,
                SizeVox2 = chunk.SizeVox * chunk.SizeVox,
                HeightmapScale = chunk.HeightmapScale,
                ChunkAltitude = chunk.WorldPosition.y,
                Voxels = new NativeArray<Voxel>(chunk.VoxelArray, Allocator.Persistent),
                Heights = new NativeArray<float>(chunk.HeightArray, Allocator.Persistent),
                Holes = new NativeArray<int>(chunk.HolesArray, Allocator.Persistent),
                NewHolesConcurrentCounter = new NativeArray<int>(1, Allocator.Persistent),
                Brush = Params.Brush,
                Action = Params.Action,
                Intensity = Params.Opacity,
                IsTargetIntensity = Params.OpacityIsTarget,
                Center = Params.Position - chunk.AbsoluteWorldPosition,
                Size = Params.Size,
                UpsideDown = Params.StalagmiteUpsideDown,
                TextureIndex = (uint)Params.TextureIndex,
                InputOriginVox = inputOriginVox,
                InputSizeVox = inputSizeVox,
                InputVoxels = inputVoxels
            };
            job.PostConstruct();
            return job;
        }

        public void Complete(VoxelModificationJob job, VoxelChunk chunk)
        {
            job.Voxels.CopyTo(chunk.VoxelArray);
            job.Voxels.Dispose();
            job.Heights.Dispose();
            job.InputVoxels.Dispose();

            var cutter = chunk.Cutter;
            if (job.Action != ActionType.Reset)
            {
                if (job.NewHolesConcurrentCounter[0] > 0)
                {
                    cutter.Cut(job.Holes, chunk.VoxelPosition, chunk.ChunkPosition);
                }
            }
            else
            {
                cutter.UnCut(job.Holes, chunk.VoxelPosition, chunk.ChunkPosition);
            }

            job.NewHolesConcurrentCounter.Dispose();
            job.Holes.Dispose();
        }
    }
}