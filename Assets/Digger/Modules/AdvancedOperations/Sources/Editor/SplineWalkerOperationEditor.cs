using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Digger.Modules.AdvancedOperations.Splines;
using Digger.Modules.AdvancedOperations.Splines.Editor;
using Digger.Modules.Core.Editor;
using Digger.Modules.Core.Editor.Operations;
using Digger.Modules.Core.Sources;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using SplineWalker = Digger.Modules.AdvancedOperations.Sources.ModificationJobs.SplineWalker.SplineWalker;

namespace Digger.Modules.AdvancedOperations.Sources.Editor
{
    [OperationAttr("Spline", 100)]
    public class SplineWalkerOperationEditor : IOperationEditor
    {
        private SplineMaster splineMaster;
        private DiggerSystem[] diggerSystems;
        private SplineWalker splineWalker;
        private BezierSplineEditor splineEditor;

        private IScriptableOperationEditor operationEditor;
        private List<Type> operationEditors;

        private int selectedOperationEditorIndex {
            get => EditorPrefs.GetInt("SplineWalkerOperationEditor_selectedOperationEditorIndex", 0);
            set => EditorPrefs.SetInt("SplineWalkerOperationEditor_selectedOperationEditorIndex", value);
        }

        private float step {
            get => EditorPrefs.GetFloat("SplineWalkerOperationEditor_step", 2f);
            set => EditorPrefs.SetFloat("SplineWalkerOperationEditor_step", value);
        }

        public void OnEnable()
        {
            splineMaster = Object.FindFirstObjectByType<SplineMaster>();
            if (!splineMaster) {
                var diggerMaster = Object.FindFirstObjectByType<DiggerMaster>();
                if (!diggerMaster) {
                    throw new Exception("DiggerMaster must be present in the scene to use SplineWalkerOperationEditor");
                }

                splineMaster = diggerMaster.gameObject.AddComponent<SplineMaster>();
            }

            diggerSystems = Object.FindObjectsByType<DiggerSystem>(FindObjectsSortMode.None);
            splineWalker = new SplineWalker(diggerSystems);
            splineEditor = new BezierSplineEditor();

            var type = typeof(IScriptableOperationEditor);
            operationEditors = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => type.IsAssignableFrom(p) && !p.IsAbstract && !p.IsInterface && p != typeof(SplineWalkerOperationEditor))
                .ToList();
            operationEditors.Sort(new OperationAttr.Comparer());

            if (selectedOperationEditorIndex < 0 || selectedOperationEditorIndex >= operationEditors.Count) {
                selectedOperationEditorIndex = 0;
            }

            if (operationEditor == null && operationEditors.Count > 0) {
                operationEditor = (IScriptableOperationEditor)Activator.CreateInstance(operationEditors[selectedOperationEditorIndex]);
            }

            operationEditor?.OnDisable();
            operationEditor?.OnEnable();
        }

        public void OnDisable()
        {
            operationEditor?.OnDisable();
        }

        public void OnInspectorGUI()
        {
            if (!splineMaster.Spline && GUILayout.Button("Create new spline")) {
                splineMaster.Spline = BezierSplineInspector.NewSpline();
            }
            
            var newSpline = (BezierSpline)EditorGUILayout.ObjectField("Spline", splineMaster.Spline, typeof(BezierSpline), true);
            if (newSpline != splineMaster.Spline) {
                splineMaster.Spline = newSpline;
                SceneView.RepaintAll();
            }

            step = EditorGUILayout.FloatField(new GUIContent("Step", "Distance between two operations performed along the spline"), step);

            var idx = EditorGUILayout.Popup("Operation", selectedOperationEditorIndex,
                operationEditors.Select(type => type.GetCustomAttribute(typeof(OperationAttr)) is OperationAttr attr ? attr.Name : type.Name).ToArray());
            if (idx != selectedOperationEditorIndex) {
                ChangeSelectedOperation(idx);
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            operationEditor?.OnInspectorGUI();
            EditorGUILayout.EndVertical();

            if (splineMaster.Spline && operationEditor != null && GUILayout.Button("Perform operation along the spline", GUILayout.Height(40))) {
                Walk().GetAwaiter().GetResult();
            }
        }

        private async Awaitable Walk()
        {
            if (!Application.isPlaying) {
                foreach (var diggerSystem in diggerSystems) {
                    diggerSystem.PrepareModification();
                }
            }
            
            switch (operationEditor) {
                case AddOperationEditor editor:
                    await splineWalker.WalkAlongSpline(splineMaster.Spline, step, editor.OperationAt);
                    break;
                case DigOperationEditor editor:
                    await splineWalker.WalkAlongSpline(splineMaster.Spline, step, editor.OperationAt);
                    break;
            }
            
            if (!Application.isPlaying) {
                foreach (var diggerSystem in diggerSystems) {
                    diggerSystem.PersistAndRecordUndo(false, false);
                }
            }
        }

        private void ChangeSelectedOperation(int newIndex)
        {
            selectedOperationEditorIndex = newIndex;
            operationEditor?.OnDisable();
            operationEditor = (IScriptableOperationEditor)Activator.CreateInstance(operationEditors[selectedOperationEditorIndex]);
            operationEditor.OnEnable();
            SceneView.RepaintAll();
        }

        public void OnSceneGUI()
        {
            if (splineMaster.Spline) {
                splineEditor.OnSceneGUI(splineMaster.Spline);
            }
        }

        public async Awaitable OnScene(UnityEditor.Editor editor, SceneView sceneview)
        {
        }
    }
}