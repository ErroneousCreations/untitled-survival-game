using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct GetSurfaceChunksJob : IJobParallelFor
    {
        public int SizeVox;
        public float HeightmapScaleY;
        public int SizeOfMesh;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float> Heights;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> Holes;

        [NativeDisableParallelForRestriction]
        public NativeParallelHashSet<int>.ParallelWriter ChunkOnSurfaceY;

        public void Execute(int index)
        {
            var pi = Utils.HoleIndexToXZ(index, SizeVox);
            if (Utils.IsOnHole(pi, SizeVox, Holes)) {
                var voxelAltitude = (int)(Heights[Utils.XYZToHeightIndex(pi, SizeVox)] / HeightmapScaleY);
                var chunkAltitude = RoundingDownDivision(voxelAltitude-1, SizeOfMesh);
                ChunkOnSurfaceY.Add(chunkAltitude);
            }
        }
        
        private static int RoundingDownDivision(int a, int b) {
            int res = a / b;
            return (a < 0 && a != b * res) ? res - 1 : res;
        }
    }
}