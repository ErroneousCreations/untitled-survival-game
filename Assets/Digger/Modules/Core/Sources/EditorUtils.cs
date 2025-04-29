#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Object = UnityEngine.Object;
#if __MICROSPLAT_DIGGER__
using JBooth.MicroSplat;
#endif


namespace Digger.Modules.Core.Sources
{
    public static class EditorUtils
    {
        public static bool MicroSplatExists(Terrain terrain)
        {
#if __MICROSPLAT_DIGGER__
            return terrain.GetComponent<MicroSplatTerrain>();
#else
            return false;
#endif
        }

        public static T CreateOrReplaceAsset<T>(T asset, string path) where T : Object
        {
            if (!AssetDatabase.Contains(asset))
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        public static Mesh CreateOrUpdateMeshAsset(Mesh asset, string path)
        {
            if (!AssetDatabase.Contains(asset))
            {
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static bool AreMeshesEqual(Mesh mesh1, Mesh mesh2)
        {
            return mesh1 != null && mesh2 != null &&
                mesh1.vertexCount == mesh2.vertexCount && 
                mesh1.subMeshCount == mesh2.subMeshCount &&
                Enumerable.SequenceEqual(mesh1.vertices, mesh2.vertices) && 
                Enumerable.SequenceEqual(mesh1.colors, mesh2.colors) &&
                Enumerable.SequenceEqual(mesh1.uv, mesh2.uv) &&
                Enumerable.SequenceEqual(mesh1.uv2, mesh2.uv2) &&
                Enumerable.SequenceEqual(mesh1.uv3, mesh2.uv3) &&
                Enumerable.SequenceEqual(mesh1.uv4, mesh2.uv4) &&
                Enumerable.SequenceEqual(mesh1.normals, mesh2.normals) &&
                Enumerable.SequenceEqual(mesh1.tangents, mesh2.tangents);
        }

        public static int AspectSelectionGrid(int selected, Texture[] textures, int approxSize, GUIStyle style,
            GUIContent errorMessage)
        {
            GUILayout.BeginVertical("box", GUILayout.MinHeight(approxSize));
            var newSelected = 0;

            if (textures != null && textures.Length != 0) {
                var columns = Mathf.Max((int)(EditorGUIUtility.currentViewWidth - 150) / approxSize, 1);
                // ReSharper disable once PossibleLossOfFraction
                var rows = Mathf.Max((int)Mathf.Ceil((textures.Length + columns - 1) / columns), 1);
                var r = GUILayoutUtility.GetAspectRect(columns / (float)rows);

                var texturesPreview = new Texture[textures.Length];
                for (var i = 0; i < textures.Length; ++i) {
                    texturesPreview[i] = textures[i]
                        ? (AssetPreview.GetAssetPreview(textures[i]) ?? textures[i])
                        : EditorGUIUtility.whiteTexture;
                }

                newSelected = GUI.SelectionGrid(r, Math.Min(selected, texturesPreview.Length - 1), texturesPreview,
                    columns, style);
            } else {
                GUILayout.Label(errorMessage);
            }

            GUILayout.EndVertical();
            return newSelected;
        }
    }
}

#endif