using Unity.Jobs;
using UnityEngine;

namespace Digger.Modules.Core.Sources.Polygonizers
{
    public interface IPolygonizer
    {
        JobHandle BuildMesh(VoxelChunk chunk, int lod);

        bool CompleteBuildMesh(Mesh mesh, Bounds bounds);
    }
}