﻿Shader "Unlit/SunShaftsSRP_FORWARD_URP"
{

	Properties
	{
		[HideInInspector]_MainTex("Base (RGB)", 2D) = "white" {}
	//[HideInInspector]_ColorBuffer("Base (RGB)", 2D) = "white" {}
	 
		//[HideInInspector]_MainTex("Base (RGB)", 2D) = "white" {}
		//_Delta("Line Thickness", Range(0.0005, 0.0025)) = 0.001
		//[Toggle(RAW_OUTLINE)]_Raw("Outline Only", Float) = 0
		//[Toggle(POSTERIZE)]_Poseterize("Posterize", Float) = 0
		//_PosterizationCount("Count", int) = 8

		_SunThreshold("sun thres", Color) = (0.87, 0.74, 0.65,1)
		_SunColor("sun color", Color) = (1.87, 1.74, 1.65,1)
		_BlurRadius4("blur", Color) = (0.00325, 0.00325, 0,0)
		_SunPosition("sun pos", Color) = (111, 11,339, 11)
	}

		HLSLINCLUDE

		//#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl" //unity 2018.3
//#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl" 
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		//#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/SurfaceInput.hlsl"
		//#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		//#include "PostProcessing/Shaders/StdLib.hlsl" //unity 2018.1-2
		//#include "UnityCG.cginc"

		TEXTURE2D(_MainTex);
	TEXTURE2D(_ColorBuffer);
	TEXTURE2D(_Skybox);

	SAMPLER(sampler_MainTex);
	SAMPLER(sampler_ColorBuffer);
	SAMPLER(sampler_Skybox);
	float _Blend;

	//sampler2D _MainTex;
	//sampler2D _ColorBuffer;
	//sampler2D _Skybox;
	//sampler2D_float _CameraDepthTexture;
	TEXTURE2D(_CameraDepthTexture);
	SAMPLER(sampler_CameraDepthTexture);
	half4 _CameraDepthTexture_ST;

	half4 _SunThreshold = half4(0.87, 0.74, 0.65, 1);

	half4 _SunColor = half4(0.87, 0.74, 0.65, 1);
	uniform half4 _BlurRadius4 = half4(2.5 / 768, 2.5 / 768, 0.0, 0.0);
	uniform half4 _SunPosition = half4(1,1,1,1);
	uniform half4 _MainTex_TexelSize;

#define SAMPLES_FLOAT 16.0f
#define SAMPLES_INT 16

	// Vertex manipulation
	float2 TransformTriangleVertexToUV(float2 vertex)
	{
		float2 uv = (vertex + 1.0) * 0.5;
		return uv;
	}

	struct v2f {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
#if UNITY_UV_STARTS_AT_TOP
		float2 uv1 : TEXCOORD1;
#endif		
	};

	struct v2f_radial {
		float4 pos : SV_POSITION;
		float2 uv : TEXCOORD0;
		float2 blurVector : TEXCOORD1;
	};

	struct Varyings
	{
		float2 uv        : TEXCOORD0;
		float4 vertex : SV_POSITION;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	float Linear01DepthA(float2 uv)
	{
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		return SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, uv, unity_StereoEyeIndex).r;
#else
		return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
#endif
	}

	float4 FragGrey(v2f i) : SV_Target
	{
		float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
		half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.uv.xy);
		//float luminance = dot(color.rgb, float3(0.2126729, 0.7151522, 0.0721750));
		//color.rgb = lerp(color.rgb, luminance.xxx, _Blend.xxx);
		//return color/2 + colorB/2;
		return color ;
	}

	half4 fragScreen(v2f i) : SV_Target{

				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

			//half4 colorA = tex2D(_MainTex, i.uv.xy);
			half4 colorA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy); // half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
		#if !UNITY_UV_STARTS_AT_TOP
																				 ///half4 colorB = tex2D(_ColorBuffer, i.uv1.xy);
			half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.uv.xy);//v0.2 //i.uv1.xy);//v0.2
		#else
																				 //half4 colorB = tex2D(_ColorBuffer, i.uv.xy);
			half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.uv.xy);//v1.1
		#endif
			half4 depthMask = saturate(colorB * _SunColor);
			return  1.0f - (1.0f - colorA) * (1.0f - depthMask);//colorA * 5.6;// 1.0f - (1.0f - colorA) * (1.0f - depthMask);
	}


	half4 fragAdd(v2f i) : SV_Target{

		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

		//half4 colorA = tex2D(_MainTex, i.uv.xy);
		half4 colorA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
#if !UNITY_UV_STARTS_AT_TOP
		//half4 colorB = tex2D(_ColorBuffer, i.uv1.xy);
		half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.uv.xy); //v0.1 - i.uv1.xy
#else
		//half4 colorB = tex2D(_ColorBuffer, i.uv.xy);
		half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.uv.xy);
#endif
		half4 depthMask = saturate(colorB * _SunColor);
		return 1 * colorA + depthMask;
	}

	struct Attributes
	{
		float4 positionOS       : POSITION;
		float2 uv               : TEXCOORD0;
	};

	v2f vert(Attributes v) {//v2f vert(AttributesDefault v) { //appdata_img v) {
							//v2f o;
		v2f o = (v2f)0;
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

		//VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
		//o.pos = vertexInput.positionCS;
		//o.uv = v.uv;
		//Varyings output = (Varyings)0;
		//UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
		//output.vertex = vertexInput.positionCS;
		//output.uv = input.uv;
		//return output;


		//o.pos = UnityObjectToClipPos(v.vertex);
		//	o.pos = float4(v.vertex.xy, 0.0, 1.0);
		//	float2 uv = TransformTriangleVertexToUV(v.vertex.xy);

		o.pos = float4(vertexInput.positionCS.xy, 0.0, 1.0);
		float2 uv = v.uv;

		//o.uv = uv;// v.texcoord.xy;

		//o.uv1 = uv.xy;



		//// NEW 1
		//o.pos = float4(v.positionOS.xy, 0.0, 1.0);
		//uv = TransformTriangleVertexToUV(v.positionOS.xy);

#if !UNITY_UV_STARTS_AT_TOP
		uv = uv * float2(1.0, -1.0) + float2(0.0, 1.0);
		//uv.y = 1-uv.y;
#endif

		o.uv = uv;// v.texcoord.xy;

#if !UNITY_UV_STARTS_AT_TOP
		o.uv = uv.xy;//o.uv1 = uv.xy;
		if (_MainTex_TexelSize.y < 0)
			o.uv.y = 1 - o.uv.y;//o.uv1.y = 1 - o.uv1.y;
#endif	




		return o;
	}

	v2f_radial vert_radial(Attributes v) {//v2f_radial vert_radial(AttributesDefault v) { //appdata_img v) {
		//v2f_radial o;

		v2f_radial o = (v2f_radial)0;
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		////		o.pos = UnityObjectToClipPos(v.vertex);

		//o.pos = float4(v.vertex.xyz,1);
		//o.pos = float4(v.vertex.xy, 0.0, 1.0);
		//float2 uv = TransformTriangleVertexToUV(v.vertex.xy);

		VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
		o.pos = float4(vertexInput.positionCS.xy, 0.0, 1.0);
		float2 uv = v.uv;
		//output.vertex = vertexInput.positionCS;

		//uv = TransformTriangleVertexToUV(vertexInput.positionCS.xy);

		#if !UNITY_UV_STARTS_AT_TOP
				//uv = uv * float2(1.0, -1.0) + float2(0.0, 1.0);
		#endif

		o.uv.xy = uv;//v.texcoord.xy;
					 //o.blurVector = (_SunPosition.xy - v.texcoord.xy) * _BlurRadius4.xy;
		//o.uv1 = uv.xy;
		//o.uv.y = 1 - o.uv.y;
		//uv.y = 1 - uv.y;
		//o.uv.y = 1 - o.uv.y;
		//_SunPosition.y = _SunPosition.y*0.5 + 0.5;
		//_SunPosition.x = _SunPosition.x*0.5 + 0.5;
		o.blurVector = (_SunPosition.xy - uv.xy) * _BlurRadius4.xy;

		return o;
	}

	half4 frag_radial(v2f_radial i) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

		half4 color = half4(0,0,0,0);
		for (int j = 0; j < SAMPLES_INT; j++)
		{
			//half4 tmpColor = tex2D(_MainTex, i.uv.xy);
			half4 tmpColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
			color += tmpColor;
			i.uv.xy += i.blurVector;
		}
		return color / SAMPLES_FLOAT;
	}

	half TransformColor(half4 skyboxValue) {
		return dot(max(skyboxValue.rgb - _SunThreshold.rgb, half3(0, 0, 0)), half3(1, 1, 1)); // threshold and convert to greyscale
	}

	half4 frag_depth(v2f i) : SV_Target{

		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	#if !UNITY_UV_STARTS_AT_TOP
			//float depthSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv1.xy);
			float depthSample = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv.xy), _ZBufferParams); //v0.1 URP i.uv1.xy
	#else
			//float depthSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv.xy);
			float depthSample = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv.xy), _ZBufferParams);
	#endif

		//half4 tex = tex2D(_MainTex, i.uv.xy);
		half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
		//depthSample = Linear01Depth(depthSample, _ZBufferParams);

		//return tex;

		// consider maximum radius
	#if !UNITY_UV_STARTS_AT_TOP
		half2 vec = _SunPosition.xy - i.uv.xy; //i.uv1.xy;
	#else
		half2 vec = _SunPosition.xy - i.uv.xy;
	#endif
		half dist = saturate(_SunPosition.w - length(vec.xy));

		half4 outColor = 0;

		// consider shafts blockers
		//if (depthSample > 0.99)
		//if (depthSample > 0.103)
		if (depthSample > 1- 0.018) {//if (depthSample < 0.018) {
			//outColor = TransformColor(tex) * dist;
		}





#if !UNITY_UV_STARTS_AT_TOP
		if (depthSample < 0.018) {
			outColor = TransformColor(tex) * dist;
		}
#else
		if (depthSample > 1 - 0.018) {
			outColor = TransformColor(tex) * dist;
		}
#endif

		return outColor * 1;
	}

	//inline half Luminance(half3 rgb)
	//{
		//return dot(rgb, unity_ColorSpaceLuminance.rgb);
	//	return dot(rgb, rgb);
	//}

	half4 frag_nodepth(v2f i) : SV_Target{

		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

	#if !UNITY_UV_STARTS_AT_TOP
			//float4 sky = (tex2D(_Skybox, i.uv1.xy));
			float4 sky = SAMPLE_TEXTURE2D(_Skybox, sampler_Skybox, i.uv.xy);
	#else
			//float4 sky = (tex2D(_Skybox, i.uv.xy));
			float4 sky = SAMPLE_TEXTURE2D(_Skybox, sampler_Skybox, i.uv.xy); //i.uv1.xy;
	#endif

			//float4 tex = (tex2D(_MainTex, i.uv.xy));
			half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
			//sky = float4(0.3, 0.05, 0.05,  1);
			/// consider maximum radius
	#if !UNITY_UV_STARTS_AT_TOP
			half2 vec = _SunPosition.xy - i.uv.xy;
	#else
			half2 vec = _SunPosition.xy - i.uv.xy;//i.uv1.xy;
	#endif
			half dist = saturate(_SunPosition.w - length(vec));

			half4 outColor = 0;

			// find unoccluded sky pixels
			// consider pixel values that differ significantly between framebuffer and sky-only buffer as occluded


			if (Luminance(abs(sky.rgb - tex.rgb)) < 0.2) {
				outColor = TransformColor(tex) * dist;
				//outColor = TransformColor(sky) * dist;
			}

			return outColor * 1;
	}

		ENDHLSL

		//		SubShader
		//	{
		//		//Cull Off ZWrite Off ZTest Always
		//
		//			Pass
		//		{
		//			HLSLPROGRAM
		//
		//#pragma vertex VertDefault
		//#pragma fragment Frag
		//
		//			ENDHLSL
		//		}
		//	}
		Subshader {

		//PASS 0
		//Tags{ "RenderType" = "Opaque" }
			Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

#pragma vertex vert
#pragma fragment fragScreen

			ENDHLSL
		}
		//PASS 1
			Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM


			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			//	#include "BlitSunShafts.hlsl"//v0.2
				//#pragma vertex Vert
#pragma vertex vert_radial
#pragma fragment frag_radial

			ENDHLSL
		}
		//PASS 2
			Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			//	#include "BlitSunShafts.hlsl"//v0.2
			//	#pragma vertex Vert
#pragma vertex vert
#pragma fragment frag_depth

			ENDHLSL
		}
		//PASS 3
			Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
				//#include "BlitSunShafts.hlsl"//v0.2
				//#pragma vertex Vert
#pragma vertex vert
#pragma fragment frag_nodepth

			ENDHLSL
		}
		//PASS 4
			Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

#pragma vertex vert
#pragma fragment fragAdd

			ENDHLSL
		}


			//PASS 5
			Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

#pragma vertex vert
#pragma fragment FragGrey

			ENDHLSL
		}


		 //// GRAPH
		Pass //6 BLIT
			{
				Name "ColorBlitPass"
				HLSLPROGRAM
					// Core.hlsl includes URP basic variables needed for any shader. The Blit.hlsl provides a
					//Vert and Fragment function that abstracts platform differences when handling a full screen shader pass.
					//It also declares a _BlitTex texture that is bound by the Blitter API.
					#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
					//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
					// This is a simple read shader so we use the default provided Vert and FragNearest
					//functions. If you would like to do a bilinear sample you could use the FragBilinear functions instead.
					#include "BlitSunShafts.hlsl"//v0.2

					#pragma vertex Vert
					#pragma fragment FragNearest
				ENDHLSL
		}
		Pass //7 BLIT BACKGROUND
		{
				Name "ColorBlitPasss"
				HLSLPROGRAM
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
				#include "BlitSunShafts.hlsl"//v0.2
				#pragma vertex Vert
				#pragma fragment Frag
				//
				float4 Frag(VaryingsB input) : SV_Target0
				{
					// this is needed so we account XR platform differences in how they handle texture arrays
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
					// sample the texture using the SAMPLE_TEXTURE2D_X_LOD
					float2 uv = input.texcoord.xy;
					half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
					// Inverts the sampled color
					//return half4(1, 1, 1, 1) - color;
					return color*1;
				}
				ENDHLSL
			}



				//PASS 0-8
			//Tags{ "RenderType" = "Opaque" }
					Pass{
						ZTest Always Cull Off ZWrite Off

						HLSLPROGRAM

						#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
						#include "BlitSunShafts.hlsl"//v0.2
						#pragma vertex Vert
						#pragma fragment fragScreenA
			float isScreen=0;
			half4 fragScreenA(VaryingsB i) : SV_Target{

				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

					//half4 colorA = tex2D(_MainTex, i.uv.xy);
					half4 colorA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord.xy); // half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				#if !UNITY_UV_STARTS_AT_TOP
																						 ///half4 colorB = tex2D(_ColorBuffer, i.uv1.xy);
					half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.texcoord.xy);//v0.2 //i.uv1.xy);//v0.2
				#else
																						 //half4 colorB = tex2D(_ColorBuffer, i.uv.xy);
					half4 colorB = SAMPLE_TEXTURE2D(_ColorBuffer, sampler_ColorBuffer, i.texcoord.xy);//v1.1
				#endif
					half4 depthMask = saturate(colorB * _SunColor);

					//ADD
					if (isScreen == 0) {
						return 1 * colorA + depthMask;
					}
					else {
						//SCREEN
						return  1.0f - (1.0f - colorA) * (1.0f - depthMask);//colorA * 5.6;// 1.0f - (1.0f - colorA) * (1.0f - depthMask);
					}
			}

			ENDHLSL
		}
		//PASS 1-9
		Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "BlitSunShafts.hlsl"//v0.2
			//#pragma vertex Vert
			#pragma vertex vert_radialA
			#pragma fragment frag_radialA

				struct v2f_radialA {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					float2 blurVector : TEXCOORD1;
				};

				struct VaryingsBA
				{
					float4 positionCS : SV_POSITION;
					float2 texcoord   : TEXCOORD0;
					float2 blurVector : TEXCOORD1;
					UNITY_VERTEX_OUTPUT_STEREO
				};

				VaryingsBA vert_radialA(AttributesB input)
				{
					VaryingsBA output;
					UNITY_SETUP_INSTANCE_ID(input);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				#if SHADER_API_GLES
					float4 pos = input.positionOS;
					float2 uv  = input.uv;
				#else
					float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
					float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);
				#endif

					output.positionCS = pos;
					output.texcoord   = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;

					output.blurVector = (_SunPosition.xy - output.texcoord.xy) * _BlurRadius4.xy;

					return output;
				}
				
					/*
			v2f_radialA vert_radialA(Attributes v) {

				v2f_radialA o = (v2f_radialA)0;
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
				o.pos = float4(vertexInput.positionCS.xy, 0.0, 1.0);
				float2 uv = v.uv;

				#if !UNITY_UV_STARTS_AT_TOP
						//uv = uv * float2(1.0, -1.0) + float2(0.0, 1.0);
				#endif

				o.uv.xy = uv;
				o.blurVector = (_SunPosition.xy - uv.xy) * _BlurRadius4.xy;

				return o;
			}
			*/

			TEXTURE2D(_MainTexA);
			SAMPLER(sampler_MainTexA);
			#define SAMPLES_FLOATA 29.0f
			#define SAMPLES_INTA 29

			half4 frag_radialA(VaryingsBA i) : SV_Target//half4 frag_radialA(v2f_radialA i) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				half4 color = half4(0,0,0,0);

				//return SAMPLE_TEXTURE2D(_MainTexA, sampler_MainTexA, i.texcoord.xy);
				//return float4(1*i.texcoord.y,0,0,1)*i.blurVector.r;

				for (int j = 0; j < SAMPLES_INTA; j++)
				{
					half4 tmpColor = SAMPLE_TEXTURE2D(_MainTexA, sampler_MainTexA, i.texcoord.xy);
					color += tmpColor;
					i.texcoord.xy += i.blurVector;
				}
				return color / SAMPLES_FLOATA;
			}
			ENDHLSL
		}
		//PASS 2-10
		Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "BlitSunShafts.hlsl"//v0.2
			#pragma vertex Vert
			//#pragma vertex vert
			#pragma fragment frag_depthA

			half4 frag_depthA(VaryingsB i) : SV_Target{

					//return float4(0,0,0,0);

					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

					#if !UNITY_UV_STARTS_AT_TOP
							//float depthSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv1.xy);
							float depthSample = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoord.xy), _ZBufferParams); //v0.1 URP i.uv1.xy
					#else
							//float depthSample = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv.xy);
							float depthSample = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoord.xy), _ZBufferParams);
					#endif
					//return depthSample;

					//half4 tex = tex2D(_MainTex, i.uv.xy);
					half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord.xy);
					//depthSample = Linear01Depth(depthSample, _ZBufferParams);
					//return tex;

					// consider maximum radius
					#if !UNITY_UV_STARTS_AT_TOP
						half2 vec = _SunPosition.xy - i.texcoord.xy; //i.uv1.xy;
					#else
						half2 vec = _SunPosition.xy - i.texcoord.xy;
					#endif
					half dist = saturate(_SunPosition.w - length(vec.xy));

					half4 outColor = 0;

					if (depthSample > 1- 0.018) {//if (depthSample < 0.018) {
						//outColor = TransformColor(tex) * dist;
					}

					#if !UNITY_UV_STARTS_AT_TOP
							if (depthSample < 0.018) {
								outColor = TransformColor(tex) * dist;
							}
					#else
							if (depthSample > 1 - 0.018) {
								outColor = TransformColor(tex) * dist;
							}
					#endif
						return outColor * 1;
				}

			ENDHLSL
		}
		//PASS 3-11
		Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "BlitSunShafts.hlsl"//v0.2
			#pragma vertex Vert
			//#pragma vertex vert
			#pragma fragment frag_nodepthA
			

			half4 frag_nodepthA(VaryingsB i) : SV_Target{

					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

					#if !UNITY_UV_STARTS_AT_TOP
							//float4 sky = (tex2D(_Skybox, i.uv1.xy));
							float4 sky = SAMPLE_TEXTURE2D(_Skybox, sampler_Skybox, i.texcoord.xy);
					#else
							//float4 sky = (tex2D(_Skybox, i.uv.xy));
							float4 sky = SAMPLE_TEXTURE2D(_Skybox, sampler_Skybox, i.texcoord.xy); //i.uv1.xy;
					#endif

							//float4 tex = (tex2D(_MainTex, i.uv.xy));
					half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord.xy);
							//sky = float4(0.3, 0.05, 0.05,  1);
							/// consider maximum radius
					#if !UNITY_UV_STARTS_AT_TOP
							half2 vec = _SunPosition.xy - i.texcoord.xy;
					#else
							half2 vec = _SunPosition.xy - i.texcoord.xy;//i.uv1.xy;
					#endif
					half dist = saturate(_SunPosition.w - length(vec));

					half4 outColor = 0;

					// find unoccluded sky pixels
					// consider pixel values that differ significantly between framebuffer and sky-only buffer as occluded

					if (Luminance(abs(sky.rgb - tex.rgb)) < 0.2) {
						outColor = TransformColor(tex) * dist;
						//outColor = TransformColor(sky) * dist;
					}

					return outColor * 1;
			}

			ENDHLSL
		}
		//PASS 4-12
		Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "BlitSunShafts.hlsl"//v0.2
			#pragma vertex Vert
			#pragma fragment fragAdd

			ENDHLSL
		}


		//PASS 5-13
		Pass{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "BlitSunShafts.hlsl"//v0.2
			#pragma vertex Vert
			#pragma fragment FragGrey

			ENDHLSL
		}

			Pass //14 BLIT ONE TO ANOTHER
		{
				Name "ColorBlitPasss"
				HLSLPROGRAM
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
				//#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
				#include "BlitSunShafts.hlsl"//v0.2
				#pragma vertex Vert
				#pragma fragment Frag
				//
				TEXTURE2D(_MainTexA);
				SAMPLER(sampler_MainTexA);

				sampler2D _skyboxOnly;

				float4 Frag(VaryingsB input) : SV_Target0
				{
					// this is needed so we account XR platform differences in how they handle texture arrays
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
					// sample the texture using the SAMPLE_TEXTURE2D_X_LOD
					float2 uv = input.texcoord.xy;
					half4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
					// Inverts the sampled color
					//return half4(1, 1, 1, 1) - color;

					color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord.xy);
					//color = SAMPLE_TEXTURE2D_X_LOD(_MainTexA, sampler_MainTexA, uv, _BlitMipLevel);
					color = tex2D(_skyboxOnly,input.texcoord.xy);

					return color*1;
				}
				ENDHLSL
		}


	}//END SUBSHADER
}