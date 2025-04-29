using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Digger.Modules.Core.Sources;
using Digger.Modules.Core.Sources.Polygonizers;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
#if __MICROSPLAT_DIGGER__
using JBooth.MicroSplat;
#endif

namespace Digger.Modules.Core.Editor
{
    [CustomEditor(typeof(DiggerSystem))]
    public class DiggerSystemEditor : UnityEditor.Editor
    {
        private const int TxtCountPerPass = 4;
        private const int MaxPassCount = 4;

        private DiggerSystem diggerSystem;
        private static readonly int TerrainWidthInvProperty = Shader.PropertyToID("_TerrainWidthInv");
        private static readonly int TerrainHeightInvProperty = Shader.PropertyToID("_TerrainHeightInv");
        private static readonly int EnableHeightBlend = Shader.PropertyToID("_EnableHeightBlend");
        private static readonly int HeightTransition = Shader.PropertyToID("_HeightTransition");
        private static readonly int EnableInstancedPerPixelNormal = Shader.PropertyToID("_EnableInstancedPerPixelNormal");

        public void OnEnable()
        {
            diggerSystem = (DiggerSystem) target;
            Init(diggerSystem, false);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox($"Digger data for this terrain can be found in {diggerSystem.BasePathData}",
                                    MessageType.Info);
            EditorGUILayout.HelpBox($"Raw voxel data can be found in {diggerSystem.BasePathData}/.internal",
                                    MessageType.Info);
            EditorGUILayout.HelpBox("DO NOT CHANGE / RENAME / MOVE this folder.", MessageType.Warning);
            EditorGUILayout.HelpBox("Don\'t forget to backup this folder as well when you backup your project.",
                                    MessageType.Warning);

            EditorGUILayout.LabelField("Use Digger Master to start digging.");

            var showDebug = EditorGUILayout.Toggle("Show debug data", diggerSystem.ShowDebug);
            if (showDebug != diggerSystem.ShowDebug) {
                diggerSystem.ShowDebug = showDebug;
                foreach (Transform child in diggerSystem.transform) {
                    child.gameObject.hideFlags =
                        showDebug ? HideFlags.None : HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                }

                EditorApplication.DirtyHierarchyWindowSorting();
                EditorApplication.RepaintHierarchyWindow();
            }

            if (showDebug) {
                EditorGUILayout.LabelField($"GUID: {diggerSystem.Guid}");
                EditorGUILayout.LabelField($"Undo/redo stack version: {diggerSystem.Version}");

                DrawDefaultInspector();
            }
        }

        public static void Init(DiggerSystem diggerSystem, bool forceRefresh)
        {
            if (!forceRefresh && diggerSystem.IsInitialized)
                return;

            diggerSystem.PreInit(true);

            if (diggerSystem.Materials == null || forceRefresh)
                SetupMaterial(diggerSystem, forceRefresh);

            diggerSystem.Init(forceRefresh ? LoadType.Minimal_and_LoadVoxels_and_SyncVoxelsWithTerrain_and_RebuildMeshes : LoadType.Minimal);
            if (forceRefresh) {
                diggerSystem.PersistDiggerVersion();
                diggerSystem.PersistAndRecordUndo(true, false);
            }
        }

        public static void ImportShaders()
        {
            if (IsBuiltInURP()) {
                ImportURPShaders(false);
            } else if (IsBuiltInHDRP()) {
                ImportHDRPShaders(false);
            }
        }

        private static void SetupMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            Utils.Profiler.BeginSample("[Dig] SetupMaterial");

            if (IsBlockMaterial()) {
                diggerSystem.MaterialType = TerrainMaterialType.Block;
                Debug.Log("Setting up Digger with block material");
                SetupBlockMaterial(diggerSystem);
            } else if (EditorUtils.MicroSplatExists(diggerSystem.Terrain)) {
                diggerSystem.MaterialType = TerrainMaterialType.MicroSplat;
                Debug.Log("Setting up Digger with MicroSplat shaders");
                SetupMicroSplatMaterials(diggerSystem);
            } else if (IsBuiltInURP()) {
                ImportURPShaders(forceRefresh);
                diggerSystem.MaterialType = TerrainMaterialType.URP;
                Debug.Log("Setting up Digger with URP shaders");
                SetupURPMaterials(diggerSystem, forceRefresh);
            } else if (IsBuiltInHDRP()) {
                ImportHDRPShaders(forceRefresh);
                diggerSystem.MaterialType = TerrainMaterialType.HDRP;
                Debug.Log("Setting up Digger with HDRP shaders");
#if USING_HDRP_12_OR_ABOVE
                SetupHDRPMaterials(diggerSystem, forceRefresh);
#else
                Debug.LogError("Digger is compatible with HDRP version 12.1.6 or above. Please, upgrade the HDRP package.");
#endif
            } else {
                diggerSystem.MaterialType = TerrainMaterialType.Standard;
                Debug.Log("Setting up Digger with standard shaders");
                SetupDefaultMaterials(diggerSystem, forceRefresh);
            }

            Utils.Profiler.EndSample();
        }

        private static bool IsBlockMaterial()
        {
            var provider = FindFirstObjectByType<APolygonizerProvider>();
            if (!provider)
                return false;
            return provider.GetMaterials() != null;
        }

        private static bool IsBuiltInURP()
        {
            return GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial.shader.name == "Universal Render Pipeline/Terrain/Lit" ||
                 GraphicsSettings.defaultRenderPipeline != null && GraphicsSettings.defaultRenderPipeline.name.Contains("URP");
        }

        private static bool IsBuiltInHDRP()
        {
            return GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial.shader.name == "HDRP/TerrainLit" ||
                 GraphicsSettings.defaultRenderPipeline != null && GraphicsSettings.defaultRenderPipeline.name.Contains("HDRenderPipelineAsset");
        }

        #region BLOCK
        
        private static void SetupBlockMaterial(DiggerSystem diggerSystem)
        {
            var provider = FindFirstObjectByType<APolygonizerProvider>();
            diggerSystem.Materials = provider.GetMaterials();
        }

        #endregion


        #region STANDARD

        private static void SetupStandardTerrainMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            if (forceRefresh || !diggerSystem.Terrain.materialTemplate ||
                diggerSystem.Terrain.materialTemplate.shader.name != "Digger/Terrain/Standard") {
                var terrainMaterial = new Material(Shader.Find("Digger/Terrain/Standard"));
                terrainMaterial.name = "terrainMaterial";
                terrainMaterial = EditorUtils.CreateOrReplaceAsset(terrainMaterial,
                                                                   Path.Combine(diggerSystem.BasePathData, "terrainMaterial.mat"));
                terrainMaterial.SetFloat(TerrainWidthInvProperty, 1f / diggerSystem.Terrain.terrainData.size.x);
                terrainMaterial.SetFloat(TerrainHeightInvProperty, 1f / diggerSystem.Terrain.terrainData.size.z);
                diggerSystem.Terrain.materialTemplate = terrainMaterial;
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name != "Digger/Terrain/Standard")
                Debug.LogWarning("Looks like terrain material doesn't match cave meshes material.");
        }

        private static void SetupDefaultMaterials(DiggerSystem diggerSystem, bool forceRefresh)
        {
            SetupStandardTerrainMaterial(diggerSystem, forceRefresh);

            var tData = diggerSystem.Terrain.terrainData;
            var passCount = GetPassCount(tData);

            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != passCount) {
                diggerSystem.Materials = new Material[passCount];
            }

            var textures = new List<Texture2D>();
            for (var pass = 0; pass < passCount; ++pass) {
                SetupDefaultMaterial(pass, diggerSystem, textures);
            }

            diggerSystem.TerrainTextures = textures.ToArray();
        }

        private static void SetupDefaultMaterial(int pass, DiggerSystem diggerSystem, List<Texture2D> textures)
        {
            var material = diggerSystem.Materials[pass];
            var expectedShaderName = $"Digger/Mesh/Diffuse-Pass{pass}";
            if (!material || material.shader.name != expectedShaderName) {
                material = new Material(Shader.Find(expectedShaderName));
            }

            var tData = diggerSystem.Terrain.terrainData;
            var offset = pass * TxtCountPerPass;
            for (var i = 0; i + offset < tData.terrainLayers.Length && i < TxtCountPerPass; i++) {
                var terrainLayer = tData.terrainLayers[i + offset];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                material.SetFloat($"_BaryContrast", 20f);
                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_NormalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_LayerHasMask{i}", terrainLayer.maskMapTexture ? 1 : 0);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", terrainLayer.smoothness);
                material.SetTexture($"_Splat{i}", terrainLayer.diffuseTexture);
                material.SetTexture($"_Normal{i}", terrainLayer.normalMapTexture);
                material.SetTexture($"_Mask{i}", terrainLayer.maskMapTexture);
                material.SetVector($"_MaskMapRemapOffset{i}", terrainLayer.maskMapRemapMin);
                material.SetVector($"_MaskMapRemapScale{i}", terrainLayer.maskMapRemapMax);
                material.SetVector($"_DiffuseRemapScale{i}", terrainLayer.diffuseRemapMax - terrainLayer.diffuseRemapMin);
                material.SetTextureScale($"_Splat{i}",new Vector2(1f / terrainLayer.tileSize.x, 1f / terrainLayer.tileSize.y));
                material.SetTextureOffset($"_Splat{i}", terrainLayer.tileOffset);
                textures.Add(terrainLayer.diffuseTexture);
            }

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass{pass}.mat");
            material.name = $"meshMaterialPass{pass}";
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[pass] = material;
        }

        #endregion


        #region URP

        private static void ImportURPShaders(bool forceRefresh)
        {
            if (!forceRefresh && Shader.Find("Digger/Terrain/URP/Lit") != null && Shader.Find("Digger/Mesh/URP/Lit-Pass0") != null)
                return;

            ImportPackageImmediately("Assets/Digger/Shaders/URP/Digger-URP17-shaders.unitypackage");

            AssetDatabase.ImportAsset("Assets/Digger/Shaders/URP",
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("Digger URP shaders imported.");
        }

        private static void SetupURPTerrainMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            var terrainAlreadyHasDiggerMaterial = diggerSystem.Terrain.materialTemplate &&
                                                  diggerSystem.Terrain.materialTemplate.shader.name == "Digger/Terrain/URP/Lit";

            if (forceRefresh || !terrainAlreadyHasDiggerMaterial) {
                var terrainMaterial = new Material(Shader.Find("Digger/Terrain/URP/Lit"));
                terrainMaterial.SetFloat(TerrainWidthInvProperty, 1f / diggerSystem.Terrain.terrainData.size.x);
                terrainMaterial.SetFloat(TerrainHeightInvProperty, 1f / diggerSystem.Terrain.terrainData.size.z);
                if (diggerSystem.Terrain.materialTemplate && diggerSystem.Terrain.materialTemplate.IsKeywordEnabled("_TERRAIN_BLEND_HEIGHT")) {
                    terrainMaterial.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                    terrainMaterial.SetFloat(EnableHeightBlend, 1);
                } else {
                    terrainMaterial.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
                    terrainMaterial.SetFloat(EnableHeightBlend, 0);
                }

                if (diggerSystem.Terrain.materialTemplate && diggerSystem.Terrain.materialTemplate.IsKeywordEnabled("ENABLE_TERRAIN_PERPIXEL_NORMAL")) {
                    terrainMaterial.EnableKeyword("ENABLE_TERRAIN_PERPIXEL_NORMAL");
                    terrainMaterial.SetFloat(EnableInstancedPerPixelNormal, 1);
                } else {
                    terrainMaterial.DisableKeyword("ENABLE_TERRAIN_PERPIXEL_NORMAL");
                    terrainMaterial.SetFloat(EnableInstancedPerPixelNormal, 0);
                }

                if (diggerSystem.Terrain.materialTemplate) {
                    terrainMaterial.SetFloat(HeightTransition, diggerSystem.Terrain.materialTemplate.GetFloat(HeightTransition));
                }

                terrainMaterial.name = "terrainMaterial";
                terrainMaterial = EditorUtils.CreateOrReplaceAsset(terrainMaterial,
                                                                   Path.Combine(diggerSystem.BasePathData, "terrainMaterial.mat"));
                diggerSystem.Terrain.materialTemplate = terrainMaterial;
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name != "Digger/Terrain/URP/Lit")
                Debug.LogWarning("Looks like terrain material doesn't match cave meshes material.");
        }

        private static void SetupURPMaterials(DiggerSystem diggerSystem, bool forceRefresh)
        {
            SetupURPTerrainMaterial(diggerSystem, forceRefresh);

            var tData = diggerSystem.Terrain.terrainData;
            var passCount = GetPassCount(tData);

            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != passCount) {
                diggerSystem.Materials = new Material[passCount];
            }

            var textures = new List<Texture2D>();
            for (var pass = 0; pass < passCount; ++pass) {
                SetupURPMaterial(pass, diggerSystem, textures);
            }

            var warnUseOpacityAsDensity = -1;
            for (var i = 0; i < tData.terrainLayers.Length; i++) {
                if (tData.terrainLayers[i].diffuseRemapMin.w > 0.1f) {
                    warnUseOpacityAsDensity = i;
                    break;
                }
            }

            if (warnUseOpacityAsDensity >= 0) {
                Debug.LogWarning($"The terrain layer \"{tData.terrainLayers[warnUseOpacityAsDensity].name}\" has \"Opacity as Density\" enabled. " +
                                 "This is not well supported by Digger.");
                if (forceRefresh) {
                    EditorUtility.DisplayDialog(
                        "Opacity as Density",
                        $"The terrain layer \"{tData.terrainLayers[warnUseOpacityAsDensity].name}\" has \"Opacity as Density\" enabled.\n\n" +
                        "This is not well supported by Digger as it may creates visual difference between Digger meshes and the terrain. It is recommended " +
                        "to disable it and click on \"Sync & Refresh\" again.",
                        "Ok");
                }
            }

            diggerSystem.TerrainTextures = textures.ToArray();
        }

        private static void SetupURPMaterial(int pass, DiggerSystem diggerSystem, List<Texture2D> textures)
        {
            var material = diggerSystem.Materials[pass];
            var expectedShaderName = $"Digger/Mesh/URP/Lit-Pass{pass}";
            if (!material || material.shader.name != expectedShaderName) {
                material = new Material(Shader.Find(expectedShaderName));
            }

            var tData = diggerSystem.Terrain.terrainData;

            if (tData.terrainLayers.Length <= 4 && diggerSystem.Terrain.materialTemplate.IsKeywordEnabled("_TERRAIN_BLEND_HEIGHT")) {
                material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                material.SetFloat(EnableHeightBlend, 1);
                material.SetFloat(HeightTransition, diggerSystem.Terrain.materialTemplate.GetFloat("_HeightTransition"));
            } else {
                material.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
                material.SetFloat(EnableHeightBlend, 0);
            }

            var normalmap = false;
            var maskmap = false;
            var offset = pass * TxtCountPerPass;
            for (var i = 0; i + offset < tData.terrainLayers.Length && i < TxtCountPerPass; i++) {
                var terrainLayer = tData.terrainLayers[i + offset];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                if (terrainLayer.normalMapTexture)
                    normalmap = true;
                if (terrainLayer.maskMapTexture)
                    maskmap = true;
                
                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_NormalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_LayerHasMask{i}", terrainLayer.maskMapTexture ? 1 : 0);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", terrainLayer.smoothness);
                material.SetTexture($"_Splat{i}", terrainLayer.diffuseTexture);
                material.SetTexture($"_Normal{i}", terrainLayer.normalMapTexture);
                material.SetTexture($"_Mask{i}", terrainLayer.maskMapTexture);
                material.SetVector($"_MaskMapRemapOffset{i}", terrainLayer.maskMapRemapMin);
                material.SetVector($"_MaskMapRemapScale{i}", terrainLayer.maskMapRemapMax);
                material.SetVector($"_DiffuseRemapScale{i}", terrainLayer.diffuseRemapMax - terrainLayer.diffuseRemapMin);
                material.SetTextureScale($"_Splat{i}",new Vector2(1f / terrainLayer.tileSize.x, 1f / terrainLayer.tileSize.y));
                material.SetTextureOffset($"_Splat{i}", terrainLayer.tileOffset);
                textures.Add(terrainLayer.diffuseTexture);
            }

            if (normalmap) {
                material.EnableKeyword("_NORMALMAP");
            } else {
                material.DisableKeyword("_NORMALMAP");
            }

            if (maskmap) {
                material.EnableKeyword("_MASKMAP");
            } else {
                material.DisableKeyword("_MASKMAP");
            }

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass{pass}.mat");
            material.name = $"meshMaterialPass{pass}";
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[pass] = material;
        }

        #endregion


        #region HDRP
        
        private static void ImportHDRPShaders(bool forceRefresh)
        {
            if (!forceRefresh && Shader.Find("Digger/HDRP/TerrainLit") != null && Shader.Find("Digger/HDRP/MeshLit") != null)
                return;
            
            ImportPackageImmediately("Assets/Digger/Shaders/HDRP/Digger-HDRP17-shaders.unitypackage");

            AssetDatabase.ImportAsset("Assets/Digger/Shaders/HDRP", 
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            Debug.Log("Digger HDRP shaders imported.");
        }

        private static void SetupHDRPTerrainMaterial(DiggerSystem diggerSystem, bool forceRefresh)
        {
            const string SHADER_NAME = "Digger/HDRP/TerrainLit";

            if (forceRefresh || !diggerSystem.Terrain.materialTemplate ||
                diggerSystem.Terrain.materialTemplate.shader.name != SHADER_NAME) {
                var terrainMaterial = new Material(Shader.Find(SHADER_NAME));
                terrainMaterial.name = "terrainMaterial";
#if USING_HDRP_14_OR_ABOVE
                terrainMaterial.EnableKeyword("USING_HDRP_14_OR_ABOVE");
#endif
                terrainMaterial = EditorUtils.CreateOrReplaceAsset(terrainMaterial,
                                                                   Path.Combine(diggerSystem.BasePathData, "terrainMaterial.mat"));
                terrainMaterial.SetFloat(TerrainWidthInvProperty, 1f / diggerSystem.Terrain.terrainData.size.x);
                terrainMaterial.SetFloat(TerrainHeightInvProperty, 1f / diggerSystem.Terrain.terrainData.size.z);
                diggerSystem.Terrain.materialTemplate = terrainMaterial;
            }

            if (diggerSystem.Terrain.materialTemplate.shader.name != SHADER_NAME)
                Debug.LogWarning("Looks like terrain material doesn't match cave meshes material.");
        }

        private static void EnableTerrainHoleSupport()
        {
#if USING_HDRP
            foreach (var renderPipeline in GraphicsSettings.allConfiguredRenderPipelines)
            {
                if (renderPipeline is UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset asset)
                {
                    var settings = asset.currentPlatformRenderPipelineSettings;
                    settings.supportTerrainHole = true;
#if USING_HDRP_14_OR_ABOVE
                    asset.currentPlatformRenderPipelineSettings = settings;
#else
                    var prop = asset.GetType().GetField("m_RenderPipelineSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    prop.SetValue(asset, settings);
                    EditorUtility.SetDirty(asset);
#endif
                }
            }
#endif
        }

        private static void SetupHDRPMaterials(DiggerSystem diggerSystem, bool forceRefresh)
        {
            EnableTerrainHoleSupport();
            SetupHDRPTerrainMaterial(diggerSystem, forceRefresh);

            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != 1) {
                diggerSystem.Materials = new Material[1];
            }

            var textures = new List<Texture2D>();
            SetupHDRPMaterial(diggerSystem, textures);

            diggerSystem.TerrainTextures = textures.ToArray();
        }

        private static void SetupHDRPMaterial(DiggerSystem diggerSystem, List<Texture2D> textures)
        {
            const string SHADER_NAME = "Digger/HDRP/MeshLit";

            var material = new Material(Shader.Find(SHADER_NAME));
#if USING_HDRP_14_OR_ABOVE
            material.EnableKeyword("USING_HDRP_14_OR_ABOVE");
#endif

            var tData = diggerSystem.Terrain.terrainData;
            if (tData.terrainLayers.Length > 4) {
                material.EnableKeyword("_TERRAIN_8_LAYERS");
            }

            var enableMaskMap = false;

            for (var i = 0; i < tData.terrainLayers.Length && i < 8; i++) {
                var terrainLayer = tData.terrainLayers[i];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                if (terrainLayer.maskMapTexture) {
                    enableMaskMap = true;
                }
                
                var importer = (TextureImporter) TextureImporter.GetAtPath(AssetDatabase.GetAssetPath(terrainLayer.diffuseTexture));
                
                material.SetFloat($"_tiles{i}x", 1.0f / terrainLayer.tileSize.x);
                material.SetFloat($"_tiles{i}y", 1.0f / terrainLayer.tileSize.y);
                material.SetFloat($"_offset{i}x", terrainLayer.tileOffset.x);
                material.SetFloat($"_offset{i}y", terrainLayer.tileOffset.y);
                material.SetFloat($"_NormalScale{i}", terrainLayer.normalScale);
                material.SetFloat($"_LayerHasMask{i}", terrainLayer.maskMapTexture ? 1 : 0);
                material.SetFloat($"_Metallic{i}", terrainLayer.metallic);
                material.SetFloat($"_Smoothness{i}", importer && importer.DoesSourceTextureHaveAlpha() ? 1 : terrainLayer.smoothness);
                material.SetTexture($"_Splat{i}", terrainLayer.diffuseTexture);
                material.SetTexture($"_Normal{i}", terrainLayer.normalMapTexture);
                material.SetTexture($"_Mask{i}", terrainLayer.maskMapTexture);
                material.SetVector($"_MaskMapRemapOffset{i}", terrainLayer.maskMapRemapMin);
                material.SetVector($"_MaskMapRemapScale{i}", terrainLayer.maskMapRemapMax);
                material.SetVector($"_DiffuseRemapScale{i}", terrainLayer.diffuseRemapMax - terrainLayer.diffuseRemapMin);
                material.SetTextureScale($"_Splat{i}",new Vector2(1f / terrainLayer.tileSize.x, 1f / terrainLayer.tileSize.y));
                material.SetTextureOffset($"_Splat{i}", terrainLayer.tileOffset);
                textures.Add(terrainLayer.diffuseTexture);
            }

            if (enableMaskMap) {
                material.EnableKeyword("_MASKMAP");
            }

            var matPath = Path.Combine(diggerSystem.BasePathData, $"meshMaterialPass.mat");
            material.name = $"meshMaterialPass";
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[0] = material;
        }

#endregion

        #region MicroSplat

        private static void SetupMicroSplatMaterials(DiggerSystem diggerSystem)
        {
            if (diggerSystem.Materials == null || diggerSystem.Materials.Length != 1) {
                diggerSystem.Materials = new Material[1];
            }

            var textures = new List<Texture2D>();
            var tData = diggerSystem.Terrain.terrainData;

            for (var i = 0; i < tData.terrainLayers.Length && i < 28; i++) {
                var terrainLayer = tData.terrainLayers[i];
                if (terrainLayer == null || terrainLayer.diffuseTexture == null)
                    continue;

                textures.Add(terrainLayer.diffuseTexture);
            }

            diggerSystem.TerrainTextures = textures.ToArray();
#if __MICROSPLAT_DIGGER__
            CheckMicroSplatTerrainFeatures(diggerSystem);
            SetupMicroSplatMaterial(diggerSystem);
            SetupMicroSplatMaterialSyncEventHandler(diggerSystem);
#endif // __MICROSPLAT_DIGGER__
        }

#if __MICROSPLAT_DIGGER__
        private static void CheckMicroSplatTerrainFeatures(DiggerSystem diggerSystem)
        {
            var microSplat = diggerSystem.Terrain.GetComponent<MicroSplatTerrain>();
            if (!microSplat) {
                Debug.LogError($"Could not find MicroSplatTerrain on terrain {diggerSystem.Terrain.name}");
                return;
            }

#if __MICROSPLAT_TRIPLANAR__
            if (!microSplat.keywordSO.IsKeywordEnabled("_TRIPLANAR")) {
                microSplat.keywordSO.EnableKeyword("_TRIPLANAR");
            }

            if (microSplat.keywordSO.IsKeywordEnabled("_TRIPLANARLOCALSPACE")) {
                microSplat.keywordSO.DisableKeyword("_TRIPLANARLOCALSPACE");
            }
#else
            Debug.LogError("MicroSplat Digger integration requires the MicroSplat Triplanar module.");
#endif
        }

        private static void SetupMicroSplatMaterial(DiggerSystem diggerSystem)
        {
            var microSplat = diggerSystem.Terrain.GetComponent<MicroSplatTerrain>();
            if (!microSplat) {
                Debug.LogError($"Could not find MicroSplatTerrain on terrain {diggerSystem.Terrain.name}");
                return;
            }

            var microSplatShader = MicroSplatUtilities.GetDiggerShader(microSplat);
            if (microSplatShader == null) {
                Debug.LogError($"Could not find MicroSplat Digger shader");
                return;
            }

            var material = new Material(microSplatShader);
            material.CopyPropertiesFromMaterial(microSplat.matInstance);
            
            var matPath = Path.Combine(diggerSystem.BasePathData, $"diggerMicroSplat.mat");
            material.name = "diggerMicroSplat";
            material = EditorUtils.CreateOrReplaceAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath, ImportAssetOptions.ForceUpdate);
            diggerSystem.Materials[0] = material;
        }

        private static void SetupMicroSplatMaterialSyncEventHandler(DiggerSystem diggerSystem)
        {
            var msSync = diggerSystem.gameObject.GetComponent<MicroSplatSync>();
            if (!msSync) {
                diggerSystem.gameObject.AddComponent<MicroSplatSync>();
            } else {
                msSync.OnDisable();
                msSync.OnEnable();
            }
        }
#endif // __MICROSPLAT_DIGGER__

        #endregion

        private static int GetPassCount(TerrainData tData)
        {
            var passCount = tData.terrainLayers.Length / TxtCountPerPass;
            if (tData.terrainLayers.Length % TxtCountPerPass != 0) {
                passCount++;
            }

            return Mathf.Min(passCount, MaxPassCount);
        }
        
        private static void ImportPackageImmediately(string packagePath)
        {
            var method = typeof(AssetDatabase).GetMethod("ImportPackageImmediately", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, new object[] { packagePath });
            }
            else
            {
                AssetDatabase.ImportPackage(packagePath, false);
            }
        }
    }
}