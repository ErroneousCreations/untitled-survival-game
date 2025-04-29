using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Digger.Modules.Core.Sources
{
    public class ChunkObject : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private MeshFilter filter;
        [SerializeField] private MeshCollider meshCollider;
        [SerializeField] private bool hasCollider;
        [SerializeField] private bool isStatic;
        [SerializeField] private Terrain terrain;
        [SerializeField] private DiggerSystem digger;
        
        [SerializeField] private Mesh mesh1;
        [SerializeField] private Mesh mesh2;
        [SerializeField] private bool nextMeshIsMesh1;
        [NonSerialized] private Mesh mesh1PlayingInEditor;
        [NonSerialized] private Mesh mesh2PlayingInEditor;
        [NonSerialized] private bool nextMeshIsMesh1PlayingInEditor;

        public Mesh Mesh => filter.sharedMesh;
        public Terrain Terrain => terrain;
        public DiggerSystem Digger => digger;

        internal static ChunkObject Create(int lod,
            Vector3i chunkPosition,
            ChunkLODGroup chunkLodGroup,
            bool hasCollider,
            DiggerSystem digger,
            Terrain terrain,
            Material[] materials,
            int layer,
            string tag)
        {
            Utils.Profiler.BeginSample("ChunkObject.Create");
            var go = new GameObject(GetName(chunkPosition));
            go.layer = layer;
            go.tag = tag;
            go.hideFlags = digger.ShowDebug ? HideFlags.None : HideFlags.HideInHierarchy | HideFlags.HideInInspector;

            go.transform.parent = chunkLodGroup.transform;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var chunkObject = go.AddComponent<ChunkObject>();
            chunkObject.enabled = false;
            chunkObject.terrain = terrain;
            chunkObject.digger = digger;
            chunkObject.hasCollider = hasCollider;
            chunkObject.meshRenderer = go.AddComponent<MeshRenderer>();
            chunkObject.meshRenderer.lightmapScaleOffset = digger.Terrain.lightmapScaleOffset;
            chunkObject.meshRenderer.realtimeLightmapScaleOffset = digger.Terrain.realtimeLightmapScaleOffset;
            chunkObject.meshRenderer.sharedMaterials = materials ?? new Material[0];
            SetupMeshRenderer(digger.Terrain, chunkObject.meshRenderer);

            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            go.GetComponent<Renderer>().receiveShadows = true;
            chunkObject.filter = go.AddComponent<MeshFilter>();
            chunkObject.meshRenderer.enabled = false;
            chunkObject.mesh1 = new Mesh();
            chunkObject.mesh2 = new Mesh();
            chunkObject.nextMeshIsMesh1 = true;

            if (hasCollider) {
                chunkObject.meshCollider = go.AddComponent<MeshCollider>();
                var terrainCollider = terrain.GetComponent<TerrainCollider>();
                if (terrainCollider) {
                    chunkObject.meshCollider.sharedMaterial = terrainCollider.sharedMaterial;
                }
            }

            chunkObject.UpdateStaticEditorFlags(digger.EnableOcclusionCulling, digger.EnableContributeGI);

            digger.onChunkObjectCreated?.Invoke(chunkObject);
            Utils.Profiler.EndSample();
            return chunkObject;
        }

        public void UpdateStaticEditorFlags(bool enableOcclusionCulling, bool enableContributeGI)
        {
#if UNITY_EDITOR

            var runtime = FindFirstObjectByType<ADiggerRuntimeMonoBehaviour>();
            StaticEditorFlags flags = 0;

            if (!runtime)
            {
                isStatic = true;
                if (enableOcclusionCulling)
                {
                    flags |= StaticEditorFlags.OccludeeStatic |
                             StaticEditorFlags.OccluderStatic;
                }
                if (enableContributeGI)
                {
                    flags |= StaticEditorFlags.ContributeGI |
                             StaticEditorFlags.ReflectionProbeStatic;
                }
            }
            else
            {
                isStatic = false;
            }
            GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
#endif
        }
        
        public void GenerateSecondaryUVSet()
        {
#if UNITY_EDITOR
            if (isStatic && filter.sharedMesh)
            {
                meshRenderer.scaleInLightmap = 0.25f;
                meshRenderer.staticShadowCaster = true;
                meshRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                Unwrapping.GenerateSecondaryUVSet(filter.sharedMesh);
            }
#endif
        }

        private static void SetupMeshRenderer(Terrain terrain, MeshRenderer meshRenderer)
        {
#if UNITY_EDITOR
            var terrainSerializedObject = new SerializedObject(terrain);
            var serializedObject = new SerializedObject(meshRenderer);
            var terrainLightmapParameters = terrainSerializedObject.FindProperty("m_LightmapParameters");
            var lightmapParameters = serializedObject.FindProperty("m_LightmapParameters");
            lightmapParameters.objectReferenceValue = terrainLightmapParameters.objectReferenceValue;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        public static string GetName(Vector3i chunkPosition)
        {
            return $"ChunkObject_{chunkPosition.x}_{chunkPosition.y}_{chunkPosition.z}";
        }
        
        public Mesh NextMesh()
        {
            if (Application.isEditor && Application.isPlaying)
            {
                if (!mesh1PlayingInEditor) mesh1PlayingInEditor = new Mesh();
                if (!mesh2PlayingInEditor) mesh2PlayingInEditor = new Mesh();
                return nextMeshIsMesh1PlayingInEditor ? mesh1PlayingInEditor : mesh2PlayingInEditor;
            }
            if (!mesh1) mesh1 = new Mesh();
            if (!mesh2) mesh2 = new Mesh();
            return nextMeshIsMesh1 ? mesh1 : mesh2;
        }
        
        public void ClearOldMesh()
        {
            if (Application.isEditor && Application.isPlaying)
            {
                var meshToClear = nextMeshIsMesh1PlayingInEditor ? mesh2PlayingInEditor : mesh1PlayingInEditor;
                meshToClear.Clear();
                nextMeshIsMesh1PlayingInEditor = !nextMeshIsMesh1PlayingInEditor;
            }
            else
            {
                var meshToClear = nextMeshIsMesh1 ? mesh2 : mesh1;
                meshToClear.Clear();
                nextMeshIsMesh1 = !nextMeshIsMesh1;
            }
        }

        public bool PostBuild(bool withCollision)
        {
            Utils.Profiler.BeginSample("[Dig] Chunk.PostBuild");

            var mesh = NextMesh();
            var hasVisualMesh = false;
            if (mesh.vertexCount > 0) {
                filter.sharedMesh = mesh;
                meshRenderer.enabled = true;
                hasVisualMesh = true;
            } else {
                filter.sharedMesh = null;
                meshRenderer.enabled = false;
            }

            if (hasCollider) {
                if (mesh.vertexCount > 0 && withCollision) {
                    meshCollider.sharedMesh = mesh;
                    meshCollider.enabled = true;
                } else {
                    meshCollider.sharedMesh = null;
                    meshCollider.enabled = false;
                }
            }
            
            ClearOldMesh();

            Utils.Profiler.EndSample();
            return hasVisualMesh;
        }

#if UNITY_EDITOR
        public void SaveMeshesAsAssets(DiggerSystem digger, int lod)
        {
            var sameMeshes = meshCollider && filter && meshCollider.sharedMesh == filter.sharedMesh;
            
            if (filter && filter.sharedMesh) {
                filter.sharedMesh.name = $"{gameObject.name}_{lod}_mesh";
                EditorUtils.CreateOrUpdateMeshAsset(filter.sharedMesh, Path.Combine(digger.MeshesPathData, $"{gameObject.name}_{lod}_mesh.asset"));
            }
            if (meshCollider && meshCollider.sharedMesh && !sameMeshes)
            {
                meshCollider.sharedMesh.name = $"{gameObject.name}_{lod}_collisionMesh";
                EditorUtils.CreateOrUpdateMeshAsset(meshCollider.sharedMesh, Path.Combine(digger.MeshesPathData, $"{gameObject.name}_{lod}_collisionMesh.asset"));
            }
            
            

            // if (filter && filter.sharedMesh) {
            //     filter.sharedMesh.name = $"{gameObject.name}_{lod}_mesh";
            //     var mesh = EditorUtils.CreateOrUpdateMeshAsset(filter.sharedMesh, Path.Combine(digger.MeshesPathData, $"{gameObject.name}_{lod}_mesh.asset"));
            //     filter.sharedMesh = null;
            //     filter.sharedMesh = mesh;
            //     if (sameMeshes)
            //     {
            //         meshCollider.sharedMesh = null;
            //         meshCollider.sharedMesh = mesh;
            //         meshCollider.enabled = false;
            //         meshCollider.enabled = true;
            //     }
            // }
            //
            // if (meshCollider && meshCollider.sharedMesh && !sameMeshes)
            // {
            //     meshCollider.sharedMesh.name = $"{gameObject.name}_{lod}_collisionMesh";
            //     var mesh = EditorUtils.CreateOrUpdateMeshAsset(meshCollider.sharedMesh, Path.Combine(digger.MeshesPathData, $"{gameObject.name}_{lod}_collisionMesh.asset"));
            //     meshCollider.sharedMesh = null;
            //     meshCollider.sharedMesh = mesh;
            //     meshCollider.enabled = false;
            //     meshCollider.enabled = true;
            // }
        }
#endif
    }
}