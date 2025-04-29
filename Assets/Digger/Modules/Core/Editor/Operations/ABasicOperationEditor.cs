using System;
using System.Linq;
using Digger.Modules.Core.Sources;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Digger.Modules.Core.Editor.Operations
{
    public abstract class ABasicOperationEditor
    {
        protected DiggerSystem[] diggerSystems;
        protected bool keepingHeight;
        protected float keptHeight;

        private bool clicking;
        private bool warnedAboutPlayMode;

        private GameObject reticleSphere;
        private GameObject reticleHalfSphere;
        private GameObject reticleCube;
        private GameObject reticleCone;
        private GameObject reticleCustom;

        protected CustomBrush customBrush;

        protected BrushType brush
        {
            get => (BrushType)EditorPrefs.GetInt("BasicOperationEditor_brush", (int)BrushType.Sphere);
            set => EditorPrefs.SetInt("BasicOperationEditor_brush", (int)value);
        }

        protected float opacity
        {
            get => EditorPrefs.GetFloat("BasicOperationEditor_opacity", 0.3f);
            set => EditorPrefs.SetFloat("BasicOperationEditor_opacity", Mathf.Clamp01(value));
        }

        protected bool opacityIsTarget
        {
            get => EditorPrefs.GetBool("BasicOperationEditor_opacityIsTarget", false);
            set => EditorPrefs.SetBool("BasicOperationEditor_opacityIsTarget", value);
        }

        protected float3 size
        {
            get => new float3(EditorPrefs.GetFloat("BasicOperationEditor_size_x", 3f), EditorPrefs.GetFloat("BasicOperationEditor_size_y", 3f), EditorPrefs.GetFloat("BasicOperationEditor_size_z", 3f));
            set
            {
                EditorPrefs.SetFloat("BasicOperationEditor_size_x", value.x);
                EditorPrefs.SetFloat("BasicOperationEditor_size_y", value.y);
                EditorPrefs.SetFloat("BasicOperationEditor_size_z", value.z);
            }
        }

        protected bool sizeLinked
        {
            get => EditorPrefs.GetBool("BasicOperationEditor_sizeLinked", true);
            set => EditorPrefs.SetBool("BasicOperationEditor_sizeLinked", value);
        }

        protected float depth
        {
            get => EditorPrefs.GetFloat("BasicOperationEditor_depth", 0f);
            set => EditorPrefs.SetFloat("BasicOperationEditor_depth", value);
        }

        protected bool upsideDown
        {
            get => EditorPrefs.GetBool("BasicOperationEditor_upsideDown", false);
            set => EditorPrefs.SetBool("BasicOperationEditor_upsideDown", value);
        }

        protected int textureIndex
        {
            get => EditorPrefs.GetInt("BasicOperationEditor_textureIndex", 0);
            set => EditorPrefs.SetInt("BasicOperationEditor_textureIndex", value);
        }

        protected string customBrushId
        {
            get => EditorPrefs.GetString("BasicOperationEditor_customBrushId", "");
            set => EditorPrefs.SetString("BasicOperationEditor_customBrushId", value);
        }

        private GameObject ReticleSphere
        {
            get
            {
                if (!reticleSphere)
                {
                    var prefab = DiggerMasterEditor.LoadAssetWithLabel(DiggerMasterEditor.GetReticleLabel("Digger_SphereReticle"));
                    reticleSphere = Object.Instantiate(prefab);
                    reticleSphere.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleSphere;
            }
        }

        private GameObject ReticleCube
        {
            get
            {
                if (!reticleCube)
                {
                    var prefab = DiggerMasterEditor.LoadAssetWithLabel(DiggerMasterEditor.GetReticleLabel("Digger_CubeReticle"));
                    reticleCube = Object.Instantiate(prefab);
                    reticleCube.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleCube;
            }
        }

        private GameObject ReticleHalfSphere
        {
            get
            {
                if (!reticleHalfSphere)
                {
                    var prefab = DiggerMasterEditor.LoadAssetWithLabel(DiggerMasterEditor.GetReticleLabel("Digger_HalfSphereReticle"));
                    reticleHalfSphere = Object.Instantiate(prefab);
                    reticleHalfSphere.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleHalfSphere;
            }
        }

        private GameObject ReticleCone
        {
            get
            {
                if (!reticleCone)
                {
                    var prefab = DiggerMasterEditor.LoadAssetWithLabel(DiggerMasterEditor.GetReticleLabel("Digger_ConeReticle"));
                    reticleCone = Object.Instantiate(prefab);
                    reticleCone.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleCone;
            }
        }

        private GameObject ReticleCustom
        {
            get
            {
                if (!reticleCustom && customBrush)
                {
                    reticleCustom = Object.Instantiate(customBrush.gameObject);
                    reticleCustom.hideFlags = HideFlags.HideAndDontSave;
                }

                return reticleCustom;
            }
        }

        protected GameObject Reticle
        {
            get
            {
                switch (brush)
                {
                    case BrushType.Custom:
                        if (reticleSphere)
                            Object.DestroyImmediate(reticleSphere);
                        if (reticleHalfSphere)
                            Object.DestroyImmediate(reticleHalfSphere);
                        if (reticleCube)
                            Object.DestroyImmediate(reticleCube);
                        if (reticleCone)
                            Object.DestroyImmediate(reticleCone);
                        return ReticleCustom;
                    case BrushType.HalfSphere:
                        if (reticleSphere)
                            Object.DestroyImmediate(reticleSphere);
                        if (reticleCube)
                            Object.DestroyImmediate(reticleCube);
                        if (reticleCone)
                            Object.DestroyImmediate(reticleCone);
                        if (reticleCustom)
                            Object.DestroyImmediate(reticleCustom);
                        return ReticleHalfSphere;
                    case BrushType.RoundedCube:
                        if (reticleSphere)
                            Object.DestroyImmediate(reticleSphere);
                        if (reticleHalfSphere)
                            Object.DestroyImmediate(reticleHalfSphere);
                        if (reticleCone)
                            Object.DestroyImmediate(reticleCone);
                        if (reticleCustom)
                            Object.DestroyImmediate(reticleCustom);
                        return ReticleCube;
                    case BrushType.Stalagmite:
                        if (reticleSphere)
                            Object.DestroyImmediate(reticleSphere);
                        if (reticleHalfSphere)
                            Object.DestroyImmediate(reticleHalfSphere);
                        if (reticleCube)
                            Object.DestroyImmediate(reticleCube);
                        if (reticleCustom)
                            Object.DestroyImmediate(reticleCustom);
                        return ReticleCone;
                    case BrushType.Sphere:
                    default:
                        if (reticleHalfSphere)
                            Object.DestroyImmediate(reticleHalfSphere);
                        if (reticleCube)
                            Object.DestroyImmediate(reticleCube);
                        if (reticleCone)
                            Object.DestroyImmediate(reticleCone);
                        if (reticleCustom)
                            Object.DestroyImmediate(reticleCustom);
                        return ReticleSphere;
                }
            }
        }

        public void OnEnable()
        {
            diggerSystems = Object.FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
        }

        public void OnDisable()
        {
            if (reticleSphere)
                Object.DestroyImmediate(reticleSphere);
            if (reticleHalfSphere)
                Object.DestroyImmediate(reticleHalfSphere);
            if (reticleCube)
                Object.DestroyImmediate(reticleCube);
            if (reticleCone)
                Object.DestroyImmediate(reticleCone);
            if (reticleCustom)
                Object.DestroyImmediate(reticleCustom);
        }

        protected void BrushInspectorGUI()
        {
            brush = (BrushType)EditorGUILayout.EnumPopup(new GUIContent("Brush", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: B" : ""), brush);

            switch (brush)
            {
                case BrushType.Sphere:
                {
                    EditorGUILayout.BeginVertical("Box");
                    var x = EditorGUILayout.Slider(new GUIContent("Radius", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: keypad - or +" : ""), size.x, 0.5f, 20f);
                    var y = LockableSlider("Height", size.y, 0.5f, 20f);
                    EditorGUILayout.EndVertical();

                    if (sizeLinked) y = size.x;
                    size = new float3(x, y, x);
                    break;
                }
                case BrushType.RoundedCube:
                {
                    EditorGUILayout.BeginVertical("Box");
                    var x = EditorGUILayout.Slider("Size X", size.x, 0.5f, 20f);
                    var y = LockableSlider("Size Y", size.y, 0.5f, 20f);
                    var z = LockableSlider("Size Z", size.z, 0.5f, 20f);
                    EditorGUILayout.EndVertical();

                    if (sizeLinked) y = size.x;
                    if (sizeLinked) z = size.x;
                    size = new float3(x, y, z);
                    break;
                }
                case BrushType.Stalagmite:
                {
                    var x = EditorGUILayout.Slider(new GUIContent("Width", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: keypad - or +" : ""), size.x, 0.5f, 20f);
                    var y = EditorGUILayout.Slider("Height", size.y, 0.5f, 20f);
                    size = new float3(x, y, x);
                    upsideDown = EditorGUILayout.Toggle("Upside Down", upsideDown);
                    break;
                }
                case BrushType.Custom:
                {
                    EditorGUILayout.HelpBox("To turn an object into a custom brush, just attach " +
                                            "the CustomBrush component to it.\n\n" +
                                            "Scale and rotate your object as desired, then click on 'Refresh' in the CustomBrush " +
                                            "inspector. If 'Auto Refresh' is enabled, it will be automatically updated after you change " +
                                            "the rotation or scale of your object.\n\n" +
                                            "Note: the object must have a MeshFilter to " +
                                            "be turned into a custom brush.\n\n" +
                                            "Once your custom brush is ready, drag & drop it below to use it.", MessageType.Info);

                    if (customBrush == null && !string.IsNullOrEmpty(customBrushId))
                    {
                        customBrush = Object.FindObjectsByType<CustomBrush>(FindObjectsSortMode.None).FirstOrDefault(cb => cb.Id == customBrushId);
                    }

                    customBrush = (CustomBrush)EditorGUILayout.ObjectField("Custom brush object", customBrush, typeof(CustomBrush), true);
                    customBrushId = customBrush ? customBrush.Id : "";
                    if (customBrush && (customBrush.InputVoxels == null || customBrush.InputVoxels.Length == 0))
                        customBrush.ComputeVoxels();
                    var x = EditorGUILayout.Slider(new GUIContent("Size", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: keypad - or +" : ""), size.x, 0.1f, 1f);
                    size = new float3(x, x, x);
                    break;
                }
                default:
                    size = EditorGUILayout.Slider(new GUIContent("Size", DiggerMasterEditor.shortcutsEnabled ? "Shortcut: keypad - or +" : ""), size.x, 0.5f, 20f);
                    break;
            }
        }

        private float LockableSlider(string label, float value, float left, float right)
        {
            var hRect = EditorGUILayout.BeginHorizontal();
            if (GUI.Button(new Rect(hRect.x, hRect.y + 2, 34, 34), sizeLinked ? EditorGUIUtility.IconContent("LockIcon-On") : EditorGUIUtility.IconContent("LockIcon"), GUIStyle.none))
            {
                sizeLinked = !sizeLinked;
            }

            EditorGUI.BeginDisabledGroup(sizeLinked);
            var newValue = EditorGUILayout.Slider($"      {label}", value, left, right);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            return newValue;
        }

        public virtual async Awaitable OnScene(UnityEditor.Editor editor, SceneView sceneview)
        {
            var e = Event.current;
            HandleShortcuts(editor);

            if (!clicking && !e.alt && e.type == EventType.MouseDown && e.button == 0)
            {
                clicking = true;
                if (!Application.isPlaying)
                {
                    foreach (var diggerSystem in diggerSystems)
                    {
                        diggerSystem.PrepareModification();
                    }
                }
            }
            else if (clicking && (e.type == EventType.MouseUp || e.type == EventType.MouseLeaveWindow ||
                                  (e.isKey && !e.control && !e.shift) ||
                                  e.alt || EditorWindow.mouseOverWindow == null ||
                                  EditorWindow.mouseOverWindow.GetType() != typeof(SceneView)))
            {
                clicking = false;
                if (!Application.isPlaying)
                {
                    foreach (var diggerSystem in diggerSystems)
                    {
                        diggerSystem.PersistAndRecordUndo(false, false);
                    }
                }
            }

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            var hit = DiggerMasterEditor.GetIntersectionWithTerrainOrDigger(ray);
            if (!hit.HasValue)
                return;

            var p = hit.Value.point + depth * ray.direction.normalized;

            if (e.shift)
            {
                keptHeight = p.y;
                editor.Repaint();
            }

            if (keepingHeight)
            {
                p.y = keptHeight;
            }

            UpdateReticlePosition(p);

            if (clicking)
            {
                if (Application.isPlaying)
                {
                    if (!warnedAboutPlayMode)
                    {
                        warnedAboutPlayMode = true;
                        EditorUtility.DisplayDialog("Edit in play mode not allowed",
                            "Terrain cannot be edited by Digger while playing.\n\n" +
                            "Note for Digger Runtime: you *can* use DiggerMasterRuntime to edit the terrain while playing (so you can " +
                            "test your gameplay), but modifications made in play mode won't be persisted.", "Ok");
                    }
                }
                else
                {
                    warnedAboutPlayMode = false;
                    await PerformModification(p);
                }
            }
        }

        protected abstract Awaitable PerformModification(Vector3 p);

        protected void UpdateReticlePosition(Vector3 position)
        {
            if (!Reticle)
                return;

            var reticle = Reticle.transform;
            reticle.position = position;
            reticle.localScale = 1.95f * new Vector3(size.x, size.y, size.z);
            reticle.rotation = Quaternion.identity;
            if (brush == BrushType.Stalagmite)
            {
                reticle.localScale = 1.8f * new Vector3(size.x, size.y, size.z);
                if (upsideDown)
                {
                    reticle.rotation = Quaternion.AngleAxis(180f, Vector3.right);
                }
            }

            if (brush == BrushType.Custom && customBrush)
            {
                reticle.localScale = size * new float3(customBrush.transform.localScale);
                reticle.rotation = customBrush.transform.rotation;
            }
        }

        protected void HandleShortcuts(UnityEditor.Editor editor)
        {
            if (!DiggerMasterEditor.shortcutsEnabled)
                return;

            var current = Event.current;
            if (current.type != EventType.KeyDown)
                return;

            switch (current.keyCode)
            {
                case KeyCode.KeypadMinus:
                    this.size -= 0.5f;
                    current.Use();
                    editor.Repaint();
                    break;
                case KeyCode.KeypadPlus:
                    this.size += 0.5f;
                    current.Use();
                    editor.Repaint();
                    break;
                case KeyCode.KeypadDivide:
                    this.opacity -= 0.1f;
                    current.Use();
                    editor.Repaint();
                    break;
                case KeyCode.KeypadMultiply:
                    this.opacity += 0.1f;
                    current.Use();
                    editor.Repaint();
                    break;
                case KeyCode.B:
                    switch (this.brush)
                    {
                        case BrushType.Sphere:
                            this.brush = BrushType.RoundedCube;
                            break;
                        case BrushType.RoundedCube:
                            this.brush = BrushType.HalfSphere;
                            break;
                        case BrushType.HalfSphere:
                            this.brush = BrushType.Stalagmite;
                            break;
                        case BrushType.Stalagmite:
                            this.brush = BrushType.Sphere;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    current.Use();
                    editor.Repaint();
                    break;
            }
        }
    }
}