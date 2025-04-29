using Digger.Modules.AdvancedOperations.Sources.ModificationJobs.EasyOverhangs;
using Digger.Modules.Core.Editor;
using Digger.Modules.Core.Sources;
using UnityEditor;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Sources.Editor
{
    [OperationAttr("Easy Overhangs", 101)]
    public class EasyOverhangsOperationEditor : IOperationEditor
    {
        private DiggerSystem[] diggerSystems;
        private readonly EasyOverhangsOperation operation = new EasyOverhangsOperation();

        private GameObject reticleSphere;

        private GameObject ReticleSphere {
            get {
                if (!reticleSphere) {
                    var prefab = DiggerMasterEditor.LoadAssetWithLabel(DiggerMasterEditor.GetReticleLabel("Digger_SphereReticle"));
                    reticleSphere = Object.Instantiate(prefab);
                    reticleSphere.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleSphere;
            }
        }

        private float size {
            get => EditorPrefs.GetFloat("EasyOverhangsOperationEditor_size", 20f);
            set => EditorPrefs.SetFloat("EasyOverhangsOperationEditor_size", value);
        }
        
        private float opacity {
            get => EditorPrefs.GetFloat("EasyOverhangsOperationEditor_opacity", 0.95f);
            set => EditorPrefs.SetFloat("EasyOverhangsOperationEditor_opacity", value);
        }
        
        private int textureIndex {
            get => EditorPrefs.GetInt("EasyOverhangsOperationEditor_textureIndex", 0);
            set => EditorPrefs.SetInt("EasyOverhangsOperationEditor_textureIndex", value);
        }

        private bool clicking;

        public void OnEnable()
        {
            diggerSystems = Object.FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
        }

        public void OnDisable()
        {
            if (reticleSphere)
                Object.DestroyImmediate(reticleSphere);
        }

        public void OnInspectorGUI()
        {
            var diggerSystem = Object.FindFirstObjectByType<DiggerSystem>();
            if (!diggerSystem)
                return;
            
            EditorGUILayout.HelpBox("This operation lets you create (eventually big) overhangs quickly. " +
                                    "It is designed to be used on cliffs.", MessageType.Info);

            EditorGUILayout.Space();
            
            size = EditorGUILayout.Slider(new GUIContent("Brush Size", ""), size, 4f, 50f);
            opacity = EditorGUILayout.Slider(new GUIContent("Opacity", ""), opacity, 0f, 1f);
            textureIndex = DiggerMasterEditor.TextureSelector(textureIndex, diggerSystem);
        }
        
        public void OnSceneGUI()
        {
        }

        public async Awaitable OnScene(UnityEditor.Editor editor, SceneView sceneview)
        {
            var e = Event.current;
            
            if (!clicking && !e.alt && e.type == EventType.MouseDown && e.button == 0) {
                clicking = true;
                if (!Application.isPlaying) {
                    foreach (var diggerSystem in diggerSystems) {
                        diggerSystem.PrepareModification();
                    }
                }
            } else if (clicking && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow ||
                                    (e.isKey && !e.control && !e.shift) ||
                                    e.alt || EditorWindow.mouseOverWindow == null ||
                                    EditorWindow.mouseOverWindow.GetType() != typeof(SceneView))) {
                clicking = false;
                if (!Application.isPlaying) {
                    foreach (var diggerSystem in diggerSystems) {
                        diggerSystem.PersistAndRecordUndo(false, false);
                    }
                }
            }

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var hit = DiggerMasterEditor.GetIntersectionWithTerrainOrDigger(ray);
            if (!hit.HasValue)
                return;

            var p = hit.Value.point;
            p.y += size;
            UpdateReticlePosition(p);

            if (clicking) {
                operation.Position = p;
                operation.Size = size;
                operation.TextureIndex = (uint)textureIndex;
                operation.Opacity = opacity;

                foreach (var diggerSystem in diggerSystems) {
                    await diggerSystem.ModifyAsync(operation);
                }
            }
        }

        private void UpdateReticlePosition(Vector3 position)
        {
            var reticle = ReticleSphere.transform;
            reticle.position = position;
            reticle.localScale = 1.9f * size * Vector3.one;
            reticle.rotation = Quaternion.identity;
        }
    }
}