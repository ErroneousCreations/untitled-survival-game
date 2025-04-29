using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Digger.Modules.Core.Sources.Jobs
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct MeshToVoxelsJob : IJobParallelFor
    {
        public int3 SizeVox;
        public int3 Origin;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> Vertices;

        [ReadOnly]
        [NativeDisableParallelForRestriction]
        public NativeArray<ushort> Triangles;

        [WriteOnly] public NativeArray<Voxel> Voxels;

        public void Execute(int index)
        {
            var p = IndexToXYZ(index);
            var minDistance = float.MaxValue;
            var maxOrthogonality = float.MinValue;
            for (int i = 0; i < Triangles.Length; i += 3)
            {
                var distance = SignedDistanceFromPointToTriangle(p, Vertices[Triangles[i + 0]], Vertices[Triangles[i + 1]], Vertices[Triangles[i + 2]], out var orthogonality);
                if (math.abs(distance) < math.max(minDistance - 1e-3f, 1e-3f) || (Utils.Approximately(math.abs(distance), minDistance, 1e-3f) && math.abs(orthogonality) > math.abs(maxOrthogonality)))
                {
                    minDistance = math.abs(distance);
                    maxOrthogonality = orthogonality;
                }
            }
            var voxel = new Voxel((maxOrthogonality < 0f ? -1f : 1f) * math.sqrt(minDistance));
            Voxels[index] = voxel;
        }

        private float3 IndexToXYZ(int index)
        {
            var yz = SizeVox.y * SizeVox.z;
            var xi = index / yz;
            var yi = (index - xi * yz) / SizeVox.z;
            var zi = index - xi * yz - yi * SizeVox.z;
            return new float3(xi, yi, zi);
        }


        float dot2(float3 v) { return math.dot(v, v); }

        float SignedDistanceFromPointToTriangle(float3 p, float3 v1, float3 v2, float3 v3, out float orthogonality)
        {
            v1 += Origin;
            v2 += Origin;
            v3 += Origin;

            // prepare data    
            float3 v21 = v2 - v1; float3 p1 = p - v1;
            float3 v32 = v3 - v2; float3 p2 = p - v2;
            float3 v13 = v1 - v3; float3 p3 = p - v3;
            float3 nor = math.normalize(math.cross(v21, v13));

            orthogonality = math.dot(nor, p1);

            return  // inside/outside prism test    
                    math.sign(math.dot(math.cross(v21, nor), p1)) +
                    math.sign(math.dot(math.cross(v32, nor), p2)) +
                    math.sign(math.dot(math.cross(v13, nor), p3)) < 2.0f
                    ?
                    // 3 edges    
                    math.min(math.min(
                    dot2(v21 * math.clamp(math.dot(v21, p1) / dot2(v21), 0.0f, 1.0f) - p1),
                    dot2(v32 * math.clamp(math.dot(v32, p2) / dot2(v32), 0.0f, 1.0f) - p2)),
                    dot2(v13 * math.clamp(math.dot(v13, p3) / dot2(v13), 0.0f, 1.0f) - p3))
                    :
                    // 1 face    
                    math.dot(nor, p1) * math.dot(nor, p1) / dot2(nor);
        }

    }
}