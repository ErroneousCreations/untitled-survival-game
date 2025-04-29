using Digger.Modules.Core.Sources;
using UnityEditor;
using UnityEngine;


namespace Digger.Modules.Core.Editor
{
    [CustomEditor(typeof(CustomBrush))]
    public class CustomBrushEditor : UnityEditor.Editor
    {

        private CustomBrush customBrush;

        public void OnEnable()
        {
            customBrush = (CustomBrush) target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("To scale the maximum brush size, scale this object itself through its Transform component.",
                                    MessageType.Info);
            EditorGUILayout.HelpBox("The object MUST have a MeshFilter component attached.",
                                    MessageType.Warning);
            
            customBrush.autoRefresh = EditorGUILayout.Toggle("Auto refresh", customBrush.autoRefresh);
            if (GUILayout.Button("Refresh"))
            {
                customBrush.ComputeVoxels();
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField("Voxel count", customBrush && customBrush.InputVoxels != null ? customBrush.InputVoxels.Length : 0);
            EditorGUI.EndDisabledGroup();
        }
    }
}