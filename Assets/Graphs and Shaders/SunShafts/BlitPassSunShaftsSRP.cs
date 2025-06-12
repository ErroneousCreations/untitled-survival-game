using Artngame.SKYMASTER;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
//GRAPH
//using UnityEngine.Experimental.Rendering.RenderGraphModule;
#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace Artngame.SKYMASTER //UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Copy the given color buffer to the given destination color buffer.
    ///
    /// You can use this pass to copy a color buffer to the destination,
    /// so you can use it later in rendering. For example, you can copy
    /// the opaque texture to use it for distortion effects.
    /// </summary>
    internal class BlitPassSunShaftsSRP : UnityEngine.Rendering.Universal.ScriptableRenderPass
    {

#if UNITY_2023_3_OR_NEWER
        //GRAPH
        /// ///////// GRAPH
        /// </summary>
        // This class stores the data needed by the pass, passed as parameter to the delegate function that executes the pass
        private class PassData
        {    //v0.1               
            internal TextureHandle src;
            public Material BlitMaterial { get; set; }
        }
        private Material m_BlitMaterial;

        private const float AHEADAMOUNT = 100;

        TextureHandle tmpBuffer1A;
        TextureHandle tmpBuffer1Aa;

        RTHandle _handleA;
        TextureHandle tmpBuffer2A;

        RTHandle _handleTAART;
        TextureHandle _handleTAA;

        RTHandle _handleTAART2;
        TextureHandle _handleTAA2;

        Camera currentCamera;
        float prevDownscaleFactor;//v0.1
        //public Material blitMaterial = null;

        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (Camera.main != null)
            {

                //ConfigureInput(ScriptableRenderPassInput.Color);
                //ConfigureInput(ScriptableRenderPassInput.Depth);
                sunTransform = Camera.main.transform.position + Camera.main.transform.forward * AHEADAMOUNT;

                m_BlitMaterial = blitMaterial;

                Camera.main.depthTextureMode = DepthTextureMode.Depth;

                
                //if (cameraData != null)
                //{
                //    Matrix4x4 viewMatrix = cameraData.GetViewMatrix();
                //    //Matrix4x4 projectionMatrix = cameraData.GetGPUProjectionMatrix(0);
                //    Matrix4x4 projectionMatrix = cameraData.camera.projectionMatrix;
                //    //projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, true);
                //}

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                if (Camera.main != null && cameraData.camera != Camera.main)
                {
                    return;
                }

                //CONFIGURE

                //reflectionMapID = Shader.PropertyToID("_ReflectedColorMap");
                float downScaler = 1;
                float downScaledX = (desc.width / (float)(downScaler));
                float downScaledY = (desc.height / (float)(downScaler));
                //cmd.GetTemporaryRT(reflectionMapID, Mathf.CeilToInt(downScaledX), Mathf.CeilToInt(downScaledY), 0, FilterMode.Point, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Default, 1, false);

                //tempRenderID = Shader.PropertyToID("_TempTex");
                //cmd.GetTemporaryRT(tempRenderID, cameraTextureDescriptor, FilterMode.Trilinear);

                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                int rtW = desc.width;
                int rtH = desc.height;
                int xres = (int)(rtW / ((float)1));
                int yres = (int)(rtH / ((float)1));
                if (_handleA == null || _handleA.rt.width != xres || _handleA.rt.height != yres)
                {
                    //_handleA = RTHandles.Alloc(xres, yres, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, dimension: TextureDimension.Tex2D);
                    _handleA = RTHandles.Alloc(Mathf.CeilToInt(downScaledX), Mathf.CeilToInt(downScaledY), colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        dimension: TextureDimension.Tex2D);
                }
                tmpBuffer2A = renderGraph.ImportTexture(_handleA);//reflectionMapID                            

                if (_handleTAART == null || _handleTAART.rt.width != xres || _handleTAART.rt.height != yres || _handleTAART.rt.useMipMap == false)
                {
                    //_handleTAART.rt.DiscardContents();
                    //_handleTAART.rt.useMipMap = true;// = 8;
                    //_handleTAART.rt.autoGenerateMips = true;                       
                    _handleTAART = RTHandles.Alloc(xres, yres, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, dimension: TextureDimension.Tex2D,
                        useMipMap: true, autoGenerateMips: true
                        );
                    _handleTAART.rt.wrapMode = TextureWrapMode.Clamp;
                    _handleTAART.rt.filterMode = FilterMode.Trilinear;
                    //Debug.Log(_handleTAART.rt.mipmapCount);
                }
                _handleTAA = renderGraph.ImportTexture(_handleTAART); //_TempTex

                if (_handleTAART2 == null || _handleTAART2.rt.width != xres || _handleTAART2.rt.height != yres || _handleTAART2.rt.useMipMap == false)
                {
                    _handleTAART2 = RTHandles.Alloc(xres, yres, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, dimension: TextureDimension.Tex2D,
                        useMipMap: true, autoGenerateMips: true
                        );
                    _handleTAART2.rt.wrapMode = TextureWrapMode.Clamp;
                    _handleTAART2.rt.filterMode = FilterMode.Trilinear;
                }
                _handleTAA2 = renderGraph.ImportTexture(_handleTAART2); //_TempTex

                tmpBuffer1A = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "tmpBuffer1A", true);
                tmpBuffer1Aa = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "tmpBuffer1Aa", false);
                TextureHandle sourceTexture = resourceData.activeColorTexture;

                ////////////////  SUN SHAFTS  ////////////////////////////////////////////////////////////////////////////////
                Material sheetSHAFTS = blitMaterial;
                sheetSHAFTS.SetFloat("_Blend", blend);
                Camera camera = Camera.main;
                if (useDepthTexture)
                {
                    camera.depthTextureMode |= DepthTextureMode.Depth;
                }
                Vector3 v = Vector3.one * 0.5f;
                if (sunTransform != Vector3.zero)
                {
                    v = Camera.main.WorldToViewportPoint(sunTransform);// - Camera.main.transform.position;
                }
                else
                {
                    v = new Vector3(0.5f, 0.5f, 0.0f);
                }
                //v0.1
                //int rtW = desc.width;
               // int rtH = desc.height;

                var formatA = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default; //v3.4.9
     //         RenderTexture m_TemporaryColorTexture = RenderTexture.GetTemporary(desc.width, desc.height, 0, formatA);
     //         RenderTexture lrDepthBuffer = RenderTexture.GetTemporary(desc.width, desc.height, 0, formatA);

     //         cmd.GetTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name), opaqueDesc, filterMode);
                sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(1.0f, 1.0f, 0.0f, 0.0f) * sunShaftBlurRadius);
                sheetSHAFTS.SetVector("_SunThreshold", sunThreshold);


                //Debug.Log("IN1");
                //             cmd.Blit(source, m_TemporaryColorTexture); //KEEP BACKGROUND
                string passNameA = "DO 2";
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameA, out var passData))
                {
                    passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    builder.UseTexture(passData.src, AccessFlags.Read);
                    builder.SetRenderAttachment(tmpBuffer1A, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    passData.BlitMaterial = sheetSHAFTS;
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                       ExecuteBlitPassCLEAR(data, context, 14, passData.src));// 
                    //ExecuteBlitPass(data, context, 7, passData.src));
                }


                 passNameA = "DO 2a";
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameA, out var passData))
                {
                    passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    builder.UseTexture(passData.src, AccessFlags.Read);
                    builder.SetRenderAttachment(tmpBuffer2A, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    passData.BlitMaterial = sheetSHAFTS;
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    //   ExecuteBlitPassCLEAR(data, context, 14, passData.src));// 
                    ExecuteBlitPass(data, context, 7, passData.src));
                }


                //using (var builder = renderGraph.AddRasterRenderPass<PassData>("Color Blit Resolve", out var passData, m_ProfilingSampler))
                //{
                //    passData.BlitMaterial = sheetSHAFTS;
                //    // Similar to the previous pass, however now we set destination texture as input and source as output.
                //    passData.src = builder.UseTexture(tmpBuffer1A, IBaseRenderGraphBuilder.AccessFlags.Read);
                //    builder.SetRenderAttachment(sourceTexture, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                //    builder.AllowGlobalStateModification(true);
                //    // We use the same BlitTexture API to perform the Blit operation.
                //    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
                //}
                //return;

                if (!useDepthTexture)
                {
                    var format = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default; //v3.4.9
     //              RenderTexture tmpBuffer = RenderTexture.GetTemporary(rtW, rtH, 0, format);
     //               RenderTexture.active = tmpBuffer1A;
     ///               GL.ClearWithSkybox(false, camera);
    //                sheetSHAFTS.SetTexture("_Skybox", tmpBuffer1A);

                    //             cmd.Blit(source, lrDepthBuffer, sheetSHAFTS, 3);
                    string passNameAA = "DO 1";
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameAA, out var passData))
                    {
                        passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                        desc.msaaSamples = 1; desc.depthBufferBits = 0;
                        builder.UseTexture(passData.src, AccessFlags.Read);
                        builder.UseTexture(tmpBuffer1A, AccessFlags.Read);
                        builder.SetRenderAttachment(_handleTAA2, 0, AccessFlags.Write);
                        builder.AllowPassCulling(false);
                        passData.BlitMaterial = sheetSHAFTS;
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                            ExecuteBlitPassTWO2(data, context, 3+8, passData.src, tmpBuffer1A));
                    }
                    //             RenderTexture.ReleaseTemporary(tmpBuffer);
                }
                else
                {
                    //               cmd.Blit(source, lrDepthBuffer, sheetSHAFTS, 2);
                    string passNameAAA = "DO 1";
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameAAA, out var passData))
                    {
                        passData.src = resourceData.activeColorTexture;
                        desc.msaaSamples = 1; desc.depthBufferBits = 0;
                        builder.UseTexture(passData.src, AccessFlags.Read);
                        builder.UseTexture(tmpBuffer1A, AccessFlags.Read);
                        builder.SetRenderAttachment(_handleTAA2, 0, AccessFlags.Write);
                        builder.AllowPassCulling(false);
                        passData.BlitMaterial = sheetSHAFTS;
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                            ExecuteBlitPassTWO2(data, context, 2+8, passData.src, tmpBuffer1A));
                    }
                }

                //// lrDepthBuffer == _handleTAA2
                //// m_TemporaryColorTexture = _handleTAA

               

                radialBlurIterations = Mathf.Clamp(radialBlurIterations, 1, 4);

                float ofs = sunShaftBlurRadius * (1.0f / 768.0f);
               // Debug.Log(ofs);
                sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));

                float adjustX = 0.5f;
                if (v.x < 0.5f)
                {
                    float diff = 0.5f - v.x;
                    adjustX = adjustX - 0.5f * diff;
                }
                float adjustY = 0.5f;
                if (v.y > 1.25f)
                {
                    float diff2 = v.y - 1.25f;
                    adjustY = adjustY - 0.3f * diff2;
                }
                if (v.y > 1.8f)
                {
                    v.y = 1.8f;
                    float diff3 = v.y - 1.25f;
                    adjustY = 0.5f - 0.3f * diff3;
                }

                sheetSHAFTS.SetVector("_SunPosition", new Vector4(v.x * 0.5f + adjustX, v.y * 0.5f + adjustY, v.z, maxRadius));

                // lrColorB == _handleTAART
                //// lrDepthBuffer == _handleTAA2
                //// m_TemporaryColorTexture = _handleTAA

               

                //TEST2                
                for (int it2 = 0; it2 < radialBlurIterations; it2++)
                {
                    //                lrColorB = RenderTexture.GetTemporary(rtW, rtH, 0);
                    //                   cmd.Blit(lrDepthBuffer, lrColorB, sheetSHAFTS, 1);//Blit(cmd, lrDepthBuffer, lrColorB, sheetSHAFTS, 1); //Blit(cmd, lrDepthBuffer.Identifier(), lrColorB, sheetSHAFTS, 1);//v0.1
                    //                cmd.ReleaseTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name));//  lrDepthBuffer.id);//v0.1
                    string passName = "SAVE TEMP"+it2;
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                    {
                        //passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                        passData.src = resourceData.activeColorTexture;
                        desc.msaaSamples = 1; desc.depthBufferBits = 0;
                        //builder.UseTexture(passData.src, IBaseRenderGraphBuilder.AccessFlags.Read);
                        builder.UseTexture(_handleTAA2, AccessFlags.Read);
                        builder.SetRenderAttachment(_handleTAA, 0, AccessFlags.Write);
                        builder.AllowPassCulling(false);
                        //builder.AllowGlobalStateModification(true);
                        passData.BlitMaterial = sheetSHAFTS;                        
                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                            ExecuteBlitPassA(data, context, 1 + 8, _handleTAA2));
                    }



                    //passNameA = "DO 2+it2";
                    //using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameA, out var passData))
                    //{
                    //    passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    //    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    //    builder.UseTexture(_handleTAA, IBaseRenderGraphBuilder.AccessFlags.Read);
                    //    builder.SetRenderAttachment(tmpBuffer1Aa, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                    //    builder.AllowPassCulling(false);
                    //    passData.BlitMaterial = sheetSHAFTS;
                    //    builder.AllowGlobalStateModification(true);
                    //    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    //        ExecuteBlitPass(data, context, 14, _handleTAA));
                    //}
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Color Blit Resolve1", out var passData, m_ProfilingSampler))
                    {
                        passData.BlitMaterial = sheetSHAFTS;
                        // Similar to the previous pass, however now we set destination texture as input and source as output.
                        builder.UseTexture(_handleTAA, AccessFlags.Read);
                        passData.src = _handleTAA;
                        builder.SetRenderAttachment(_handleTAA2, 0, AccessFlags.Write);
                        builder.AllowGlobalStateModification(true);
                        // We use the same BlitTexture API to perform the Blit operation.
                        builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
                    }


              


                    ofs = sunShaftBlurRadius * (((it2 * 2.0f + 1.0f) * 6.0f)) / 768.0f;
                    sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
                    // cmd.GetTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name), opaqueDesc, filterMode);   //    lrDepthBuffer.id, opaqueDesc, filterMode);   //v0.1 
                    // cmd.Blit(lrColorB, lrDepthBuffer, sheetSHAFTS, 1); //Blit(cmd, lrColorB, lrDepthBuffer.Identifier(), sheetSHAFTS, 1);//v0.1
                    // RenderTexture.ReleaseTemporary(lrColorB);  //v0.1
                    // passName = "SAVE TEMP 2" + it2;
                    //using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                    //{
                    //    //passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    //    passData.src = resourceData.activeColorTexture;
                    //    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    //    //builder.UseTexture(passData.src, IBaseRenderGraphBuilder.AccessFlags.Read);
                    //    builder.UseTexture(tmpBuffer1Aa, IBaseRenderGraphBuilder.AccessFlags.Read);
                    //    builder.SetRenderAttachment(_handleTAA2, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                    //    builder.AllowPassCulling(false);
                    //    //builder.AllowGlobalStateModification(true);
                    //    passData.BlitMaterial = sheetSHAFTS;
                    //    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    //        ExecuteBlitPassA(data, context, 1+8, tmpBuffer1Aa));
                    //}



                    ofs = sunShaftBlurRadius * (((it2 * 2.0f + 2.0f) * 6.0f)) / 768.0f;
                    sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
                }

                //using (var builder = renderGraph.AddRasterRenderPass<PassData>("Color Blit Resolve1", out var passData, m_ProfilingSampler))
                //{
                //    passData.BlitMaterial = sheetSHAFTS;
                //    // Similar to the previous pass, however now we set destination texture as input and source as output.
                //    passData.src = builder.UseTexture(_handleTAA2, IBaseRenderGraphBuilder.AccessFlags.Read);
                //    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                //    builder.AllowGlobalStateModification(true);
                //    // We use the same BlitTexture API to perform the Blit operation.
                //    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
                //}
                //return;

                ////cmd.Blit(temp1, renderingData.cameraData.renderer.cameraColorTargetHandle); //v0.1
                //using (var builder = renderGraph.AddRasterRenderPass<PassData>("Color Blit Resolve", out var passData, m_ProfilingSampler))
                //{
                //    passData.BlitMaterial = sheetSHAFTS;
                //    // Similar to the previous pass, however now we set destination texture as input and source as output.
                //    passData.src = builder.UseTexture(_handleTAA2, IBaseRenderGraphBuilder.AccessFlags.Read);
                //    builder.SetRenderAttachment(sourceTexture, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                //    builder.AllowGlobalStateModification(true);
                //    // We use the same BlitTexture API to perform the Blit operation.
                //    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
                //}
                //return;////


                if (v.z >= 0.0f)
                {
                    sheetSHAFTS.SetVector("_SunColor", new Vector4(sunColor.r, sunColor.g, sunColor.b, sunColor.a) * sunShaftIntensity);
                }
                else
                {
                    sheetSHAFTS.SetVector("_SunColor", Vector4.zero); // no backprojection !
                }
                //       cmd.SetGlobalTexture("_ColorBuffer", lrDepthBuffer);
                //       cmd.Blit(m_TemporaryColorTexture, source, sheetSHAFTS, (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen) ? 0 : 4);

                if(screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen)
                {
                    sheetSHAFTS.SetFloat("isScreen", 1);
                }
                if (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Add)
                {
                    sheetSHAFTS.SetFloat("isScreen", 0);
                }

                string passNameAAa = "DO 1aa";
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameAAa, out var passData))
                {
                    passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    builder.UseTexture(passData.src, AccessFlags.Read);
                    builder.UseTexture(_handleTAA2, AccessFlags.Read);
                    builder.SetRenderAttachment(tmpBuffer2A, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    passData.BlitMaterial = sheetSHAFTS;
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        ExecuteBlitPassTWO2a(data, context, 0 + 8, passData.src, _handleTAA2));
                }

                //BLIT FINAL
                //cmd.Blit(temp1, renderingData.cameraData.renderer.cameraColorTargetHandle); //v0.1
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Color Blit Resolve", out var passData, m_ProfilingSampler))
                {
                    passData.BlitMaterial = sheetSHAFTS;
                    // Similar to the previous pass, however now we set destination texture as input and source as output.
                    builder.UseTexture(tmpBuffer2A, AccessFlags.Read);
                    passData.src = tmpBuffer2A;
                    builder.SetRenderAttachment(sourceTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    // We use the same BlitTexture API to perform the Blit operation.
                    builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
                }
                // this.prevViewProjectionMatrix = cameraData.camera.nonJitteredProjectionMatrix * cameraData.camera.worldToCameraMatrix;
                //  cameraData.camera.ResetProjectionMatrix();

                /*
                passName = "SAVE TEMP2";
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    //passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    passData.src = resourceData.activeColorTexture;
                    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    //builder.UseTexture(passData.src, IBaseRenderGraphBuilder.AccessFlags.Read);
                    builder.UseTexture(_handleTAA, IBaseRenderGraphBuilder.AccessFlags.Read);
                    builder.SetRenderAttachment(tmpBuffer1A, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    passData.BlitMaterial = sheetSHAFTS;
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        ExecuteBlitPass(data, context, 2, _handleTAA));
                }
                passName = "SAVE TEMP";
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    //passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    passData.src = resourceData.activeColorTexture;
                    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    //builder.UseTexture(passData.src, IBaseRenderGraphBuilder.AccessFlags.Read);
                    builder.UseTexture(_handleTAA2, IBaseRenderGraphBuilder.AccessFlags.Read);
                    builder.SetRenderAttachment(_handleTAA, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    passData.BlitMaterial = sheetSHAFTS;
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        ExecuteBlitPass(data, context, 2, _handleTAA2));
                }
                passName = "SAVE TEMP";
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    //passData.src = resourceData.activeColorTexture; //SOURCE TEXTURE
                    passData.src = resourceData.activeColorTexture;
                    desc.msaaSamples = 1; desc.depthBufferBits = 0;
                    //builder.UseTexture(passData.src, IBaseRenderGraphBuilder.AccessFlags.Read);
                    builder.UseTexture(tmpBuffer1A, IBaseRenderGraphBuilder.AccessFlags.Read);
                    builder.SetRenderAttachment(_handleTAA2, 0, IBaseRenderGraphBuilder.AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    passData.BlitMaterial = sheetSHAFTS;
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        ExecuteBlitPass(data, context, 2, tmpBuffer1A));
                }
                */
                // END PING PONG


                //RESET CAMERA
                //cameraData.camera.ResetWorldToCameraMatrix();
                //cameraData.camera.ResetProjectionMatrix();

                //cameraData.camera.nonJitteredProjectionMatrix = cameraData.camera.projectionMatrix;

                //Matrix4x4 p = cameraData.camera.projectionMatrix;
                //float2 jitter = (float2)(2 * Halton2364Seq[Time.frameCount % HaltonLength] - 1) * JitterSpread;
                //p.m02 = jitter.x / (float)Screen.width;
                //p.m12 = jitter.y / (float)Screen.height;
                //cameraData.camera.projectionMatrix = p;

            }
        }
        static void ExecuteBlitPassTEX9NAME(PassData data, RasterGraphContext context, int pass,
     string texname1, TextureHandle tmpBuffer1,
     string texname2, TextureHandle tmpBuffer2,
     string texname3, TextureHandle tmpBuffer3,
     string texname4, TextureHandle tmpBuffer4,
     string texname5, TextureHandle tmpBuffer5,
     string texname6, TextureHandle tmpBuffer6,
     string texname7, TextureHandle tmpBuffer7,
     string texname8, TextureHandle tmpBuffer8,
     string texname9, TextureHandle tmpBuffer9,
     string texname10, TextureHandle tmpBuffer10
     )
        {
            data.BlitMaterial.SetTexture(texname1, tmpBuffer1);
            data.BlitMaterial.SetTexture(texname2, tmpBuffer2);
            data.BlitMaterial.SetTexture(texname3, tmpBuffer3);
            data.BlitMaterial.SetTexture(texname4, tmpBuffer4);
            data.BlitMaterial.SetTexture(texname5, tmpBuffer5);
            data.BlitMaterial.SetTexture(texname6, tmpBuffer6);
            data.BlitMaterial.SetTexture(texname7, tmpBuffer7);
            data.BlitMaterial.SetTexture(texname8, tmpBuffer8);
            data.BlitMaterial.SetTexture(texname9, tmpBuffer9);
            data.BlitMaterial.SetTexture(texname10, tmpBuffer10);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        //temporal
        static void ExecuteBlitPassTEN(PassData data, RasterGraphContext context, int pass,
            TextureHandle tmpBuffer1, TextureHandle tmpBuffer2, TextureHandle tmpBuffer3,
            string varname1, float var1,
            string varname2, float var2,
            string varname3, Matrix4x4 var3,
            string varname4, Matrix4x4 var4,
            string varname5, Matrix4x4 var5,
            string varname6, Matrix4x4 var6,
            string varname7, Matrix4x4 var7
            )
        {
            data.BlitMaterial.SetTexture("_CloudTex", tmpBuffer1);
            data.BlitMaterial.SetTexture("_PreviousColor", tmpBuffer2);
            data.BlitMaterial.SetTexture("_PreviousDepth", tmpBuffer3);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
            //lastFrameViewProjectionMatrix = viewProjectionMatrix;
            //lastFrameInverseViewProjectionMatrix = viewProjectionMatrix.inverse;
        }

        static void ExecuteBlitPassTHREE(PassData data, RasterGraphContext context, int pass,
            TextureHandle tmpBuffer1, TextureHandle tmpBuffer2, TextureHandle tmpBuffer3)
        {
            data.BlitMaterial.SetTexture("_ColorBuffer", tmpBuffer1);
            data.BlitMaterial.SetTexture("_PreviousColor", tmpBuffer2);
            data.BlitMaterial.SetTexture("_PreviousDepth", tmpBuffer3);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        static void ExecuteBlitPass(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1aa)
        {
            data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1aa);
            if (data.BlitMaterial == null)
            {
                Debug.Log("data.BlitMaterial == null");
            }

            Blitter.BlitTexture(context.cmd,
                data.src,
                new Vector4(1, 1, 0, 0),
                data.BlitMaterial,
                pass);
        }

        static void ExecuteBlitPassCLEAR(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1aa)
        {
            //context.cmd.ClearRenderTarget(true, true, Color.clear);
            //GL.ClearWithSkybox(false, Camera.main);
            //RenderTexture.active = context.cmd.ta;

            //context.cmd.
            //context.cmd.

            //data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1aa);
            //if (data.BlitMaterial == null)
            //{
            //    Debug.Log("data.BlitMaterial == null");
            //}

            Blitter.BlitTexture(context.cmd,
                data.src,
                new Vector4(1, 1, 0, 0),
                data.BlitMaterial,
                pass);
            //RenderTexture.active = data.src;
            //GL.ClearWithSkybox(false, Camera.main);
        }

        static void ExecuteBlitPassA(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1aa)
        {
            data.BlitMaterial.SetTexture("_MainTexA", tmpBuffer1aa);
            if (data.BlitMaterial == null)
            {
                Debug.Log("data.BlitMaterial == null");
            }

            Blitter.BlitTexture(context.cmd,
                data.src,
                new Vector4(1, 1, 0, 0),
                data.BlitMaterial,
                pass);
        }
        static void ExecuteBlitPassNOTEX(PassData data, RasterGraphContext context, int pass, UniversalCameraData cameraData)
        {
            //Matrix4x4 projectionMatrix = cameraData.GetGPUProjectionMatrix(0);



            // data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1aa);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }

        static void ExecuteBlitPassTWO2(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1, TextureHandle tmpBuffer2)
        {
            data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1);// _CloudTexP", tmpBuffer1);
            data.BlitMaterial.SetTexture("_Skybox", tmpBuffer2);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        static void ExecuteBlitPassTWO2a(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1, TextureHandle tmpBuffer2)
        {
            data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1);// _CloudTexP", tmpBuffer1);
            data.BlitMaterial.SetTexture("_ColorBuffer", tmpBuffer2);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }

        static void ExecuteBlitPassTWO(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1, TextureHandle tmpBuffer2)
        {
            data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1);// _CloudTexP", tmpBuffer1);
            data.BlitMaterial.SetTexture("_TemporalAATexture", tmpBuffer2);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        static void ExecuteBlitPassTWO_MATRIX(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1, TextureHandle tmpBuffer2, Matrix4x4 matrix)
        {
            data.BlitMaterial.SetTexture("_MainTex", tmpBuffer1);// _CloudTexP", tmpBuffer1);
            data.BlitMaterial.SetTexture("_CameraDepthCustom", tmpBuffer2);
            data.BlitMaterial.SetMatrix("frustumCorners", matrix);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        static void ExecuteBlitPassTEXNAME(PassData data, RasterGraphContext context, int pass, TextureHandle tmpBuffer1aa, string texname)
        {
            data.BlitMaterial.SetTexture(texname, tmpBuffer1aa);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        static void ExecuteBlitPassTEX5NAME(PassData data, RasterGraphContext context, int pass,
            string texname1, TextureHandle tmpBuffer1,
            string texname2, TextureHandle tmpBuffer2,
            string texname3, TextureHandle tmpBuffer3,
            string texname4, TextureHandle tmpBuffer4,
            string texname5, TextureHandle tmpBuffer5
            )
        {
            data.BlitMaterial.SetTexture(texname1, tmpBuffer1);
            data.BlitMaterial.SetTexture(texname2, tmpBuffer2);
            data.BlitMaterial.SetTexture(texname3, tmpBuffer3);
            data.BlitMaterial.SetTexture(texname4, tmpBuffer4);
            data.BlitMaterial.SetTexture(texname5, tmpBuffer5);
            Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, pass);
        }
        // It is static to avoid using member variables which could cause unintended behaviour.
        static void ExecutePass(PassData data, RasterGraphContext rgContext)
        {
            Blitter.BlitTexture(rgContext.cmd, data.src, new Vector4(1, 1, 0, 0), data.BlitMaterial, 6);
        }
        //private Material m_BlitMaterial;
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("After Opaques");
        ////// END GRAPH

#endif













        //v0.4  - Unity 2020.1
#if UNITY_2020_2_OR_NEWER
        public BlitSunShaftsSRP.BlitSettings settings;


#if UNITY_2022_1_OR_NEWER
        RTHandle _handle;
#else
        RenderTargetHandle _handle;
#endif

        public override void OnCameraSetup(CommandBuffer cmd, ref UnityEngine.Rendering.Universal.RenderingData renderingData)
        {

            //RAM LEAK FIX
            if (renderingData.cameraData.camera != Camera.main)
            {
                return;
            }

            //_handle.Init(settings.textureId);
            //destination = (settings.destination == BlitSunShaftsSRP.Target.Color)
            //    ? UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget
            //    : _handle;

            //var renderer = renderingData.cameraData.renderer;
            //source = renderer.cameraColorTarget;
            var renderer = renderingData.cameraData.renderer;

#if UNITY_2022_1_OR_NEWER
            //v0.1
            //_handle.Init(settings.textureId);
            _handle = RTHandles.Alloc(settings.textureId, name: settings.textureId);
            destination = (settings.destination == BlitSunShaftsSRP.Target.Color)
                ? renderingData.cameraData.renderer.cameraColorTargetHandle //cameraColorTarget//  cameraTargetDescriptor //  UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget
                : _handle;

            //v0.1
            //source = renderer.cameraColorTarget;
            source = renderer.cameraColorTargetHandle;
#else
            //v0.1
            //_handle.Init(settings.textureId);
            _handle.Init(settings.textureId);
            destination = (settings.destination == BlitSunShaftsSRP.Target.Color)
                ?  UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget
                : _handle;

            //v0.1
            source = renderer.cameraColorTarget;            
#endif
        }
#endif


        public bool enableShafts = true;
        //SUN SHAFTS         
        public BlitSunShaftsSRP.BlitSettings.SunShaftsResolution resolution = BlitSunShaftsSRP.BlitSettings.SunShaftsResolution.Normal;
        public BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode screenBlendMode = BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen;
        public Vector3 sunTransform = new Vector3(0f, 0f, 0f); // Transform sunTransform;
        public int radialBlurIterations = 2;
        public Color sunColor = Color.white;
        public Color sunThreshold = new Color(0.87f, 0.74f, 0.65f);
        public float sunShaftBlurRadius = 2.5f;
        public float sunShaftIntensity = 1.15f;
        public float maxRadius = 0.75f;
        public bool useDepthTexture = true;
        public float blend = 0.5f;




        public enum RenderTarget
        {
            Color,
            RenderTexture,
        }

        public Material blitMaterial = null;
        public int blitShaderPassIndex = 0;
        public FilterMode filterMode { get; set; }

        private RenderTargetIdentifier source { get; set; }

        //private UnityEngine.Rendering.Universal.RenderTargetHandle destination { get; set; }
#if UNITY_2022_1_OR_NEWER
        private RTHandle destination { get; set; }
#else
        private RenderTargetHandle destination { get; set; }
#endif

        //RTHandle m_TemporaryColorTexture;
        string m_ProfilerTag;


        //SUN SHAFTS
        RenderTexture lrColorB;
       // RenderTexture lrDepthBuffer;
       // RenderTargetHandle lrColorB;
       // RTHandle lrDepthBuffer;

        /// <summary>
        /// Create the CopyColorPass
        /// </summary>
        public BlitPassSunShaftsSRP(UnityEngine.Rendering.Universal.RenderPassEvent renderPassEvent, Material blitMaterial, int blitShaderPassIndex, string tag,BlitSunShaftsSRP.BlitSettings settings)
        {
            this.renderPassEvent = renderPassEvent;
            this.blitMaterial = blitMaterial;
            this.blitShaderPassIndex = blitShaderPassIndex;
            m_ProfilerTag = tag;
            //m_TemporaryColorTexture.Init("_TemporaryColorTexture");
           // m_TemporaryColorTexture = RTHandles.Alloc("_TemporaryColorTexture", name: "_TemporaryColorTexture");
            //lrDepthBuffer = RTHandles.Alloc("lrDepthBuffer", name: "lrDepthBuffer");

            //SUN SHAFTS
            this.resolution = settings.resolution;
            this.screenBlendMode = settings.screenBlendMode;
            this.sunTransform = settings.sunTransform;
            this.radialBlurIterations = settings.radialBlurIterations;
            this.sunColor = settings.sunColor;
            this.sunThreshold = settings.sunThreshold;
            this.sunShaftBlurRadius = settings.sunShaftBlurRadius;
            this.sunShaftIntensity = settings.sunShaftIntensity;
            this.maxRadius = settings.maxRadius;
            this.useDepthTexture = settings.useDepthTexture;
            this.blend = settings.blend;
    }

        /// <summary>
        /// Configure the pass with the source and destination to execute on.
        /// </summary>
        /// <param name="source">Source Render Target</param>
        /// <param name="destination">Destination Render Target</param>
#if UNITY_2022_1_OR_NEWER
        public void Setup(RenderTargetIdentifier source, RTHandle destination)
        {
            this.source = source;
            this.destination = destination;
        }
#else
        public void Setup(RenderTargetIdentifier source, RenderTargetHandle destination)
        {
            this.source = source;
            this.destination = destination;
        }
#endif

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref UnityEngine.Rendering.Universal.RenderingData renderingData)
        {            
            //if still null, disable effect
            bool connectorFound = Camera.main;
            if (connectorFound) { sunTransform = Camera.main.transform.position + Camera.main.transform.forward * AHEADAMOUNT; }

            if (enableShafts && connectorFound)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
                opaqueDesc.depthBufferBits = 0;

                // Can't read and write to same color target, create a temp render target to blit. 
#if UNITY_2022_1_OR_NEWER
                // Can't read and write to same color target, create a temp render target to blit. 
                if (destination == renderingData.cameraData.renderer.cameraColorTargetHandle)//  UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget) //v0.1
                {
#else
                // Can't read and write to same color target, create a temp render target to blit. 
                if (destination == UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget) //v0.1
                {
#endif
                    //cmd.GetTemporaryRT(Shader.PropertyToID(m_TemporaryColorTexture.name), opaqueDesc, filterMode);
                    //Blit(cmd, source, m_TemporaryColorTexture.Identifier(), blitMaterial, 0);// blitShaderPassIndex);
                    //Blit(cmd, m_TemporaryColorTexture.Identifier(), source);

                    ////blitMaterial.SetFloat("_Delta",100);
                    //Blit(cmd, source, m_TemporaryColorTexture.Identifier(), blitMaterial, 0);// blitShaderPassIndex);
                    //Blit(cmd, m_TemporaryColorTexture.Identifier(), source);

                    RenderShafts(context, renderingData, cmd, opaqueDesc);
                }
                else
                {
                    //Blit(cmd, source, destination.Identifier(), blitMaterial, blitShaderPassIndex);
                }

                // RenderShafts(context, renderingData);
                //Camera camera = Camera.main;
                //cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                //cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, blitMaterial);
                //cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

                //context.ExecuteCommandBuffer(cmd);
                // CommandBufferPool.Release(cmd);
            }
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
#if UNITY_2022_1_OR_NEWER
#else
           // if (destination == UnityEngine.Rendering.Universal.RenderTargetHandle.CameraTarget)
           // {
               // cmd.ReleaseTemporaryRT(m_TemporaryColorTexture.id);

               // cmd.ReleaseTemporaryRT(lrColorB.id);
                //cmd.ReleaseTemporaryRT(lrDepthBuffer.id);
                //RenderTexture.ReleaseTemporary(lrColorBACK);
            //}
#endif
        }

        //RenderTexture lrColorBACK;
        //RenderTargetHandle lrColorBACK;

        //SUN SHAFTS
        public void RenderShafts(ScriptableRenderContext context, UnityEngine.Rendering.Universal.RenderingData renderingData, CommandBuffer cmd, RenderTextureDescriptor opaqueDesc)
        {

            //CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            //RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;

            //var sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/GrayscaleShafts"));

            ////           var sheetSHAFTS = context.propertySheets.Get(Shader.Find("Hidden/Custom/GrayscaleShafts"));
            Material sheetSHAFTS = blitMaterial;

            //sheet.properties.SetFloat("_Blend", settings.blend);
            sheetSHAFTS.SetFloat("_Blend", blend);

            //scontext.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);

            //if (CheckResources() == false)
            //{
            //    Graphics.Blit(source, destination);
            //    return;
            //}
            Camera camera = Camera.main;
            // we actually need to check this every frame
            if (useDepthTexture)
            {
                // GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
                camera.depthTextureMode |= DepthTextureMode.Depth;
            }
            //int divider = 4;
            //if (settings.resolution == SunShaftsHDRP.SunShaftsResolution.Normal)
            //    divider = 2;
            // else if (settings.resolution == SunShaftsHDRP.SunShaftsResolution.High)
            //    divider = 1;

            Vector3 v = Vector3.one * 0.5f;
           // Debug.Log(sunTransform);
            if (sunTransform != Vector3.zero) {
                //v = camera.WorldToViewportPoint(sunTransform);
                //v = sunTransform;
                //v = camera.WorldToViewportPoint(-sunTransform);
                v = Camera.main.WorldToViewportPoint(sunTransform);// - Camera.main.transform.position;
            }
            else {
                v = new Vector3(0.5f, 0.5f, 0.0f);
            }
            //Debug.Log("v="+v);


            //TextureDimension dim = renderingData.cameraData.cameraTargetDescriptor.dimension;


            //v0.1
            int rtW = opaqueDesc.width;///context.width; //source.width / divider;
            int rtH = opaqueDesc.height;// context.width; //source.height / divider;

            // Debug.Log(rtW + " ... " + rtH);

            var formatA = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default; //v3.4.9
            RenderTexture m_TemporaryColorTexture = RenderTexture.GetTemporary(opaqueDesc.width, opaqueDesc.height, 0, formatA);
            RenderTexture lrDepthBuffer = RenderTexture.GetTemporary(opaqueDesc.width, opaqueDesc.height, 0, formatA);


            // lrColorB = RenderTexture.GetTemporary(rtW, rtH, 0);
            //        lrDepthBuffer = RenderTexture.GetTemporary(rtW, rtH, 0);
            cmd.GetTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name), opaqueDesc, filterMode);

            //TEST1
            // Blit(cmd, source, lrDepthBuffer.Identifier(), blitMaterial,1);// blitShaderPassIndex);
            // Blit(cmd, lrDepthBuffer.Identifier(), source);
            // cmd.ReleaseTemporaryRT(lrDepthBuffer.id);
            // return;


            // mask out everything except the skybox
            // we have 2 methods, one of which requires depth buffer support, the other one is just comparing images

            //    sunShaftsMaterial.SetVector("_BlurRadius4", new Vector4(1.0f, 1.0f, 0.0f, 0.0f) * sunShaftBlurRadius);
            //    sunShaftsMaterial.SetVector("_SunPosition", new Vector4(v.x, v.y, v.z, maxRadius));
            //    sunShaftsMaterial.SetVector("_SunThreshold", sunThreshold);
            sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(1.0f, 1.0f, 0.0f, 0.0f) * sunShaftBlurRadius);
           // sheetSHAFTS.SetVector("_SunPosition", new Vector4(v.x*0.5f+0.5f, v.y , v.z, maxRadius)); //new Vector4(v.x+0.25f, v.y, v.z, maxRadius));
            //Debug.Log(v.x);
            //Debug.Log(v.y);
            sheetSHAFTS.SetVector("_SunThreshold", sunThreshold);

            if (!useDepthTexture)
            {
                //var format= GetComponent<Camera>().hdr ? RenderTextureFormat.DefaultHDR: RenderTextureFormat.Default;
                var format = camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default; //v3.4.9
                RenderTexture tmpBuffer = RenderTexture.GetTemporary(rtW, rtH, 0, format);
                RenderTexture.active = tmpBuffer;
                GL.ClearWithSkybox(false, camera);

                //sunShaftsMaterial.SetTexture("_Skybox", tmpBuffer);
                sheetSHAFTS.SetTexture("_Skybox", tmpBuffer);
                //        Graphics.Blit(source, lrDepthBuffer, sunShaftsMaterial, 3);

                //             context.command.BlitFullscreenTriangle(source, lrDepthBuffer, sheetSHAFTS, 3);
                cmd.Blit( source, lrDepthBuffer, sheetSHAFTS, 3);

                RenderTexture.ReleaseTemporary(tmpBuffer);
            }
            else
            {
                //          Graphics.Blit(source, lrDepthBuffer, sunShaftsMaterial, 2);
                //              context.command.BlitFullscreenTriangle(source, lrDepthBuffer, sheetSHAFTS, 2);
                cmd.Blit( source, lrDepthBuffer, sheetSHAFTS, 2);
            }
            //  context.command.BlitFullscreenTriangle(lrDepthBuffer, context.destination, sheet, 5);
            // return;
            // paint a small black small border to get rid of clamping problems
            //      DrawBorder(lrDepthBuffer, simpleClearMaterial);

            // radial blur:

            //Blit(cmd, source, lrDepthBuffer.Identifier(), blitMaterial,1);// blitShaderPassIndex);
            //cmd.SetGlobalTexture("_ColorBuffer", lrDepthBuffer.Identifier());
            //Blit(cmd, source, lrDepthBuffer.Identifier(), blitMaterial, 5);   
            // Blit(cmd, source, lrColorB, blitMaterial, 5);
            // Blit(cmd, lrColorB, source);
            //cmd.ReleaseTemporaryRT(lrDepthBuffer.id);
            // return;

            
            //cmd.GetTemporaryRT(Shader.PropertyToID(m_TemporaryC



            //        lrColorBACK = RenderTexture.GetTemporary(rtW, rtH, 0);
            // cmd.GetTemporaryRT(lrColorBACK.id, opaqueDesc, FilterMode.Bilinear);
            cmd.Blit( source, m_TemporaryColorTexture); //KEEP BACKGROUND
            //Blit(cmd, source, lrColorBACK.Identifier());

            //settings.radialBlurIterations =  Mathf.Clamp((int)settings.radialBlurIterations, 1, 4);
            radialBlurIterations = Mathf.Clamp(radialBlurIterations, 1, 4);

            float ofs = sunShaftBlurRadius * (1.0f / 768.0f);

            //sunShaftsMaterial.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
            //sunShaftsMaterial.SetVector("_SunPosition", new Vector4(v.x, v.y, v.z, maxRadius));
            sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));

            float adjustX = 0.5f;
            if (v.x < 0.5f) {
                //adjustX = -0.5f;
                float diff = 0.5f - v.x;
                adjustX = adjustX - 0.5f * diff;
            }
            float adjustY = 0.5f;
            if (v.y > 1.25f)
            {
                //adjustX = -0.5f;
                float diff2 = v.y - 1.25f;
                adjustY = adjustY - 0.3f * diff2;
            }
            if (v.y > 1.8f)
            {
                //adjustX = -0.5f;
                v.y = 1.8f;
                float diff3 = v.y - 1.25f;
                adjustY = 0.5f - 0.3f * diff3;
            }

            sheetSHAFTS.SetVector("_SunPosition", new Vector4(v.x * 0.5f + adjustX, v.y * 0.5f + adjustY, v.z, maxRadius));
            //Debug.Log(v.y);

            //TEST2
            //Blit(cmd, lrDepthBuffer.Identifier(), source);
            //cmd.GetTemporaryRT(lrColorB.id, opaqueDesc, filterMode);
            //RenderTexture lrColorBA = RenderTexture.GetTemporary(rtW, rtH, 0);
            // Blit(cmd, lrDepthBuffer.Identifier(), lrColorBA, sheetSHAFTS, 1);
            // Blit(cmd, lrColorBA, source);
            // Blit(cmd, lrDepthBuffer.Identifier(), source);
            // return;
            //RenderTexture.ReleaseTemporary(lrColorB);
            for (int it2 = 0; it2 < radialBlurIterations; it2++)
            {
                // each iteration takes 2 * 6 samples
                // we update _BlurRadius each time to cheaply get a very smooth look

                lrColorB = RenderTexture.GetTemporary(rtW, rtH, 0);
                cmd.Blit(lrDepthBuffer, lrColorB, sheetSHAFTS, 1);//Blit(cmd, lrDepthBuffer, lrColorB, sheetSHAFTS, 1); //Blit(cmd, lrDepthBuffer.Identifier(), lrColorB, sheetSHAFTS, 1);//v0.1
                cmd.ReleaseTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name));//  lrDepthBuffer.id);//v0.1

                ofs = sunShaftBlurRadius * (((it2 * 2.0f + 1.0f) * 6.0f)) / 768.0f;
                sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
                cmd.GetTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name), opaqueDesc, filterMode);   //    lrDepthBuffer.id, opaqueDesc, filterMode);   //v0.1 

                cmd.Blit(lrColorB, lrDepthBuffer, sheetSHAFTS, 1); //Blit(cmd, lrColorB, lrDepthBuffer.Identifier(), sheetSHAFTS, 1);//v0.1
                RenderTexture.ReleaseTemporary(lrColorB);  //v0.1

                ofs = sunShaftBlurRadius * (((it2 * 2.0f + 2.0f) * 6.0f)) / 768.0f;
                sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));

                /*
               lrColorB = RenderTexture.GetTemporary(rtW, rtH, 0);               
               
 //               cmd.GetTemporaryRT(lrColorB.id, opaqueDesc, filterMode);
                // Graphics.Blit(lrDepthBuffer, lrColorB, sunShaftsMaterial, 1);

                //             context.command.BlitFullscreenTriangle(lrDepthBuffer, lrColorB, sheetSHAFTS, 1);
                Blit(cmd, lrDepthBuffer.Identifier(), lrColorB, sheetSHAFTS, 1);

 //              RenderTexture.ReleaseTemporary(lrDepthBuffer.Identifier());
                cmd.ReleaseTemporaryRT(lrDepthBuffer.id);
                ofs = sunShaftBlurRadius * (((it2 * 2.0f + 1.0f) * 6.0f)) / 768.0f;
                //sunShaftsMaterial.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
                sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));

 //               lrDepthBuffer = RenderTexture.GetTemporary(rtW, rtH, 0);
                cmd.GetTemporaryRT(lrDepthBuffer.id, opaqueDesc, filterMode);

                // Graphics.Blit(lrColorB, lrDepthBuffer, sunShaftsMaterial, 1);
                //              context.command.BlitFullscreenTriangle(lrColorB, lrDepthBuffer, sheetSHAFTS, 1);
                Blit(cmd, lrColorB, lrDepthBuffer.Identifier(), sheetSHAFTS, 1);

               RenderTexture.ReleaseTemporary(lrColorB);
  //              cmd.ReleaseTemporaryRT(lrColorB.id);
                ofs = sunShaftBlurRadius * (((it2 * 2.0f + 2.0f) * 6.0f)) / 768.0f;
                // sunShaftsMaterial.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
                sheetSHAFTS.SetVector("_BlurRadius4", new Vector4(ofs, ofs, 0.0f, 0.0f));
                */
            }
            
            // put together:

            if (v.z >= 0.0f)
            {
                //sunShaftsMaterial.SetVector("_SunColor", new Vector4(sunColor.r, sunColor.g, sunColor.b, sunColor.a) * sunShaftIntensity);
                sheetSHAFTS.SetVector("_SunColor", new Vector4(sunColor.r, sunColor.g, sunColor.b, sunColor.a) * sunShaftIntensity);
            }
            else
            {
                // sunShaftsMaterial.SetVector("_SunColor", Vector4.zero); // no backprojection !
                sheetSHAFTS.SetVector("_SunColor", Vector4.zero); // no backprojection !
            }
            //sunShaftsMaterial.SetTexture("_ColorBuffer", lrDepthBuffer);
            //         sheetSHAFTS.SetTexture("_ColorBuffer", lrDepthBuffer.);
            cmd.SetGlobalTexture("_ColorBuffer", lrDepthBuffer);
            //    Graphics.Blit(context.source, context.destination, sunShaftsMaterial, (screenBlendMode == ShaftsScreenBlendMode.Screen) ? 0 : 4);


            //          context.command.BlitFullscreenTriangle(context.source, context.destination, sheetSHAFTS, (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen) ? 0 : 4);
            //Blit(cmd, source, destination.Identifier(), sheetSHAFTS, (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen) ? 0 : 4);
            // Blit(cmd, source, destination.Identifier(), sheetSHAFTS, (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen) ? 0 : 4);

            // Blit(cmd, source, lrDepthBuffer.Identifier(), blitMaterial, 5);
            cmd.Blit( m_TemporaryColorTexture, source, sheetSHAFTS, (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen) ? 0 : 4);
            //Blit(cmd, lrColorBACK.Identifier(), source, sheetSHAFTS, (screenBlendMode == BlitSunShaftsSRP.BlitSettings.ShaftsScreenBlendMode.Screen) ? 0 : 4);
//
           // cmd.ReleaseTemporaryRT(Shader.PropertyToID(lrDepthBuffer.name));//m_TemporaryColorTexture.id);            
           // cmd.ReleaseTemporaryRT(Shader.PropertyToID(m_TemporaryColorTexture.name));//lrDepthBuffer.id);
            RenderTexture.ReleaseTemporary(lrDepthBuffer);
            RenderTexture.ReleaseTemporary(m_TemporaryColorTexture);
            //cmd.ReleaseTemporaryRT(lrColorBACK.id);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            //cmd.ReleaseTemporaryRT(lrColorB.id);
            RenderTexture.ReleaseTemporary(lrColorB);
            //RenderTexture.ReleaseTemporary(lrColorBACK);
            //          RenderTexture.ReleaseTemporary(lrDepthBuffer);

        }


    }
}
