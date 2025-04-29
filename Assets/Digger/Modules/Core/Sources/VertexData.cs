using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Digger.Modules.Core.Sources
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexData
    {
#if UNITY_6000_0_OR_NEWER && (USING_URP || USING_HDRP)
        public float3 Vertex;        // POSITION
        public float3 Normal;        // NORMAL
        public float4 Color;         // COLOR
        public float2 UV;            // TEXCOORD0
        public float4 SplatControl0; // TEXCOORD1
        public float4 SplatControl1; // TEXCOORD2
        public float4 SplatControl2; // TEXCOORD3
        public float4 SplatControl3; // TEXCOORD4
        public float4 SplatControl4; // TEXCOORD5

        public static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Normal),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord5, VertexAttributeFormat.Float32, 4),
        };
#elif USING_URP || USING_HDRP
        public float3 Vertex;        // POSITION
        public float3 Normal;        // NORMAL
        public float4 Color;         // COLOR
        public float2 UV;            // TEXCOORD0
        public float4 SplatControl0; // TEXCOORD1
        public float4 SplatControl1; // TEXCOORD2
        public float4 SplatControl2; // TEXCOORD3
        public float4 SplatControl3; // TEXCOORD4

        public static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Normal),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.Float32, 4),
        };
#else
        public float3 Vertex;        // POSITION
        public float3 Normal;        // NORMAL
        public float4 SplatControl3; // TANGENT
        public float4 Color;         // COLOR
        public float2 UV;            // TEXCOORD0
        public float4 SplatControl0; // TEXCOORD1
        public float4 SplatControl1; // TEXCOORD2
        public float4 SplatControl2; // TEXCOORD3

        public static readonly VertexAttributeDescriptor[] Layout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position),
            new VertexAttributeDescriptor(VertexAttribute.Normal),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
        };
#endif
    }
}