// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef TRIPLANAR_TERRAIN_SPLATMAP_COMMON_CGINC_INCLUDED
#define TRIPLANAR_TERRAIN_SPLATMAP_COMMON_CGINC_INCLUDED

// Since 2018.3 we changed from _TERRAIN_NORMAL_MAP to _NORMALMAP to save 1 keyword.
// Since 2019.2 terrain keywords are changed to  local keywords so it doesn't really matter. You can use both.
#if defined(_NORMALMAP) && !defined(_TERRAIN_NORMAL_MAP)
    #define _TERRAIN_NORMAL_MAP
#elif !defined(_NORMALMAP) && defined(_TERRAIN_NORMAL_MAP)
    #define _NORMALMAP
#endif

#if defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLES)
    // GL doesn't support sperating the samplers from the texture object
    #undef TERRAIN_USE_SEPARATE_VERTEX_SAMPLER
#else
    #define TERRAIN_USE_SEPARATE_VERTEX_SAMPLER
#endif

struct Input
{
    float4 tc;
    #ifndef TERRAIN_BASE_PASS
        UNITY_FOG_COORDS(0) // needed because finalcolor oppresses fog code generation.
#endif
    float3 worldPos;
    float3 vertNormal;
};

sampler2D _Control;
float4 _Control_ST;
float4 _Control_TexelSize;
sampler2D _Splat0, _Splat1, _Splat2, _Splat3;
float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;

float _TerrainWidthInv;
float _TerrainHeightInv;
#if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X)
    // Some drivers have undefined behaviors when samplers are used from the vertex shader
    // with anisotropic filtering enabled. This causes some artifacts on some devices. To be
    // sure to avoid this we use the vertex_linear_clamp_sampler sampler to sample terrain
    // maps from the VS when we can.
    #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
        UNITY_DECLARE_TEX2D(_TerrainHeightmapTexture);
        UNITY_DECLARE_TEX2D(_TerrainNormalmapTexture);
        SamplerState sampler__TerrainNormalmapTexture;
        SamplerState vertex_linear_clamp_sampler;
    #else
        sampler2D _TerrainHeightmapTexture;
        sampler2D _TerrainNormalmapTexture;
    #endif

    float4    _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
    float4    _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
    UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData) // float4(xBase, yBase, skipScale, ~)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _NORMALMAP
    sampler2D _Normal0, _Normal1, _Normal2, _Normal3;
    float _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
#endif

#ifdef _ALPHATEST_ON
    sampler2D _TerrainHolesTexture;

    void ClipHoles(float2 uv)
    {
        float hole = tex2D(_TerrainHolesTexture, uv).r;
        clip(hole == 0.0f ? -1 : 1);
    }
#endif

#if defined(TERRAIN_BASE_PASS) && defined(UNITY_PASS_META)
    // When we render albedo for GI baking, we actually need to take the ST
    float4 _MainTex_ST;
#endif

void SplatmapVert(inout appdata_full v, out Input data)
{
    UNITY_INITIALIZE_OUTPUT(Input, data);

#if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X)

    float2 patchVertex = v.vertex.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float4 uvscale = instanceData.z * _TerrainHeightmapRecipSize;
    float4 uvoffset = instanceData.xyxy * uvscale;
    uvoffset.xy += 0.5f * _TerrainHeightmapRecipSize.xy;
    float2 sampleCoords = (patchVertex.xy * uvscale.xy + uvoffset.xy);

    #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
        float hm = UnpackHeightmap(_TerrainHeightmapTexture.SampleLevel(vertex_linear_clamp_sampler, sampleCoords, 0));
    #else
        float hm = UnpackHeightmap(tex2Dlod(_TerrainHeightmapTexture, float4(sampleCoords, 0, 0)));
    #endif

    v.vertex.xz = (patchVertex.xy + instanceData.xy) * _TerrainHeightmapScale.xz * instanceData.z;  //(x + xBase) * hmScale.x * skipScale;
    v.vertex.y = hm * _TerrainHeightmapScale.y;
    v.vertex.w = 1.0f;

    v.texcoord.xy = (patchVertex.xy * uvscale.zw + uvoffset.zw);
    v.texcoord3 = v.texcoord2 = v.texcoord1 = v.texcoord;

    #ifdef TERRAIN_INSTANCED_PERPIXEL_NORMAL
        v.normal = float3(0, 1, 0); // TODO: reconstruct the tangent space in the pixel shader. Seems to be hard with surface shader especially when other attributes are packed together with tSpace.
        data.tc.zw = sampleCoords;
    #else
        #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
            float3 nor = _TerrainNormalmapTexture.SampleLevel(vertex_linear_clamp_sampler, sampleCoords, 0).xyz;
        #else
            float3 nor = tex2Dlod(_TerrainNormalmapTexture, float4(sampleCoords, 0, 0)).xyz;
        #endif
        v.normal = 2.0f * nor - 1.0f;
    #endif
#endif

    data.vertNormal = v.normal;
    v.tangent.xyz = cross(v.normal, float3(0,0,1));
    v.tangent.w = -1;

    data.tc.xy = v.texcoord.xy;
#ifdef TERRAIN_BASE_PASS
    #ifdef UNITY_PASS_META
        data.tc.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    #endif
#else
    float4 pos = UnityObjectToClipPos(v.vertex);
    UNITY_TRANSFER_FOG(data, pos);
#endif
}

#ifndef TERRAIN_BASE_PASS

#ifdef TERRAIN_STANDARD_SHADER
void SplatmapMix(Input IN, half4 defaultAlpha, out half4 splat_control, out half weight, out fixed4 mixedDiffuse, inout fixed3 mixedNormal)
#else
void SplatmapMix(Input IN, out half4 splat_control, out half weight, out fixed4 mixedDiffuse, inout fixed3 mixedNormal)
#endif
{
    #ifdef _ALPHATEST_ON
        ClipHoles(IN.tc.xy);
    #endif

    // adjust splatUVs so the edges of the terrain tile lie on pixel centers
    float2 splatUV = (IN.tc.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    splat_control = tex2D(_Control, splatUV);
    weight = dot(splat_control, half4(1,1,1,1));

    #if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
        clip(weight == 0.0f ? -1 : 1);
    #endif

    // Normalize weights before lighting and restore weights in final modifier functions so that the overal
    // lighting result can be correctly weighted.
    splat_control /= (weight + 1e-3f);
    
    float2 _TerrainSizeInv = float2(_TerrainWidthInv, _TerrainHeightInv);
    float2 uvSplat0 = _Splat0_ST.xy * _TerrainSizeInv;
    float2 uvSplat1 = _Splat1_ST.xy * _TerrainSizeInv;
    float2 uvSplat2 = _Splat2_ST.xy * _TerrainSizeInv;
    float2 uvSplat3 = _Splat3_ST.xy * _TerrainSizeInv;

    mixedDiffuse = 0.0f;
    mixedNormal = 0.0f;
    
    fixed3 normal = abs(IN.vertNormal);
    float y = normal.y;
    float z = normal.z;

    if (splat_control.r > 1e-2f) {
	    float2 y0 = IN.worldPos.zy * uvSplat0 + _Splat0_ST.zw;
	    float2 x0 = IN.worldPos.xz * uvSplat0 + _Splat0_ST.zw;
	    float2 z0 = IN.worldPos.xy * uvSplat0 + _Splat0_ST.zw;
	    fixed4 cX0 = tex2D(_Splat0, y0);
	    fixed4 cY0 = tex2D(_Splat0, x0);
	    fixed4 cZ0 = tex2D(_Splat0, z0);
	    half4 side0 = lerp(cX0, cZ0, z);
	    half4 top0 = lerp(side0, cY0, y);
	    mixedDiffuse += splat_control.r * top0 * half4(1.0, 1.0, 1.0, defaultAlpha.r);
        
        #ifdef _NORMALMAP
        fixed4 nX0 = tex2D(_Normal0, y0);
        fixed4 nY0 = tex2D(_Normal0, x0);
        fixed4 nZ0 = tex2D(_Normal0, z0);
        half4 side0n = lerp(nX0, nZ0, z);
        half4 top0n = lerp(side0n, nY0, y);
        mixedNormal += UnpackNormalWithScale(top0n, _NormalScale0) * splat_control.r;
        #endif
    }
	
    if (splat_control.g > 1e-2f) {
	    float2 y1 = IN.worldPos.zy * uvSplat1 + _Splat1_ST.zw;
	    float2 x1 = IN.worldPos.xz * uvSplat1 + _Splat1_ST.zw;
	    float2 z1 = IN.worldPos.xy * uvSplat1 + _Splat1_ST.zw;
	    fixed4 cX1 = tex2D(_Splat1, y1);
	    fixed4 cY1 = tex2D(_Splat1, x1);
	    fixed4 cZ1 = tex2D(_Splat1, z1);
	    half4 side1 = lerp(cX1, cZ1, z);
	    half4 top1 = lerp(side1, cY1, y);
	    mixedDiffuse += splat_control.g * top1 * half4(1.0, 1.0, 1.0, defaultAlpha.g);
		
        #ifdef _NORMALMAP
        fixed4 nX1 = tex2D(_Normal1, y1);
        fixed4 nY1 = tex2D(_Normal1, x1);
        fixed4 nZ1 = tex2D(_Normal1, z1);
        half4 side1n = lerp(nX1, nZ1, z);
        half4 top1n = lerp(side1n, nY1, y);
        mixedNormal += UnpackNormalWithScale(top1n, _NormalScale1) * splat_control.g;
        #endif
    }
	
    if (splat_control.b > 1e-2f) {
	    float2 y2 = IN.worldPos.zy * uvSplat2 + _Splat2_ST.zw;
	    float2 x2 = IN.worldPos.xz * uvSplat2 + _Splat2_ST.zw;
	    float2 z2 = IN.worldPos.xy * uvSplat2 + _Splat2_ST.zw;
	    fixed4 cX2 = tex2D(_Splat2, y2);
	    fixed4 cY2 = tex2D(_Splat2, x2);
	    fixed4 cZ2 = tex2D(_Splat2, z2);
	    half4 side2 = lerp(cX2, cZ2, z);
	    half4 top2 = lerp(side2, cY2, y);
	    mixedDiffuse += splat_control.b * top2 * half4(1.0, 1.0, 1.0, defaultAlpha.b);
		
        #ifdef _NORMALMAP
        fixed4 nX2 = tex2D(_Normal2, y2);
        fixed4 nY2 = tex2D(_Normal2, x2);
        fixed4 nZ2 = tex2D(_Normal2, z2);
        half4 side2n = lerp(nX2, nZ2, z);
        half4 top2n = lerp(side2n, nY2, y);
        mixedNormal += UnpackNormalWithScale(top2n, _NormalScale2) * splat_control.b;
        #endif
    }
	
    if (splat_control.a > 1e-2f) {
	    float2 y3 = IN.worldPos.zy * uvSplat3 + _Splat3_ST.zw;
	    float2 x3 = IN.worldPos.xz * uvSplat3 + _Splat3_ST.zw;
	    float2 z3 = IN.worldPos.xy * uvSplat3 + _Splat3_ST.zw;
	    fixed4 cX3 = tex2D(_Splat3, y3);
	    fixed4 cY3 = tex2D(_Splat3, x3);
	    fixed4 cZ3 = tex2D(_Splat3, z3);
	    half4 side3 = lerp(cX3, cZ3, z);
	    half4 top3 = lerp(side3, cY3, y);
	    mixedDiffuse += splat_control.a * top3 * half4(1.0, 1.0, 1.0, defaultAlpha.a);
        
        #ifdef _NORMALMAP
        fixed4 nX3 = tex2D(_Normal3, y3);
        fixed4 nY3 = tex2D(_Normal3, x3);
        fixed4 nZ3 = tex2D(_Normal3, z3);
        half4 side3n = lerp(nX3, nZ3, z);
        half4 top3n = lerp(side3n, nY3, y);
        mixedNormal += UnpackNormalWithScale(top3n, _NormalScale3) * splat_control.a;
        #endif
    }

#if defined(SHADER_API_SWITCH)
        mixedNormal.z += UNITY_HALF_MIN; // to avoid nan after normalizing
#else
        mixedNormal.z += 1e-5f; // to avoid nan after normalizing
#endif

    #if defined(INSTANCING_ON) && defined(SHADER_TARGET_SURFACE_ANALYSIS) && defined(TERRAIN_INSTANCED_PERPIXEL_NORMAL)
        mixedNormal = float3(0, 0, 1); // make sure that surface shader compiler realizes we write to normal, as UNITY_INSTANCING_ENABLED is not defined for SHADER_TARGET_SURFACE_ANALYSIS.
    #endif

    #if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X) && defined(TERRAIN_INSTANCED_PERPIXEL_NORMAL)

        #if defined(TERRAIN_USE_SEPARATE_VERTEX_SAMPLER)
            float3 geomNormal = normalize(_TerrainNormalmapTexture.Sample(sampler__TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #else
            float3 geomNormal = normalize(tex2D(_TerrainNormalmapTexture, IN.tc.zw).xyz * 2 - 1);
        #endif

        #ifdef _NORMALMAP
            float3 geomTangent = normalize(cross(geomNormal, float3(0, 0, 1)));
            float3 geomBitangent = normalize(cross(geomTangent, geomNormal));
            mixedNormal = mixedNormal.x * geomTangent
                          + mixedNormal.y * geomBitangent
                          + mixedNormal.z * geomNormal;
        #else
            mixedNormal = geomNormal;
        #endif
        mixedNormal = mixedNormal.xzy;
    #endif
}

#ifndef TERRAIN_SURFACE_OUTPUT
    #define TERRAIN_SURFACE_OUTPUT SurfaceOutput
#endif

void SplatmapFinalColor(Input IN, TERRAIN_SURFACE_OUTPUT o, inout fixed4 color)
{
    color *= o.Alpha;
    #ifdef TERRAIN_SPLAT_ADDPASS
        UNITY_APPLY_FOG_COLOR(IN.fogCoord, color, fixed4(0,0,0,0));
    #else
        UNITY_APPLY_FOG(IN.fogCoord, color);
    #endif
}

void SplatmapFinalGBuffer(Input IN, TERRAIN_SURFACE_OUTPUT o, inout half4 outGBuffer0, inout half4 outGBuffer1, inout half4 outGBuffer2, inout half4 emission)
{
    UnityStandardDataApplyWeightToGbuffer(outGBuffer0, outGBuffer1, outGBuffer2, o.Alpha);
    emission *= o.Alpha;
}

#endif // TERRAIN_BASE_PASS

#endif // TERRAIN_SPLATMAP_COMMON_CGINC_INCLUDED
