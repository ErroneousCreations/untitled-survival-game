using Digger.Modules.AdvancedOperations.Splines.ProceduralGeneration;
using UnityEditor;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Splines.Editor
{
    [CustomEditor(typeof(BezierSpline))]
    public class BezierSplineInspector : UnityEditor.Editor
    {
        private BezierSplineEditor bezierSplineEditor;
        private int selectedIndex;

        [MenuItem("Tools/Digger/Create Bezier Spline", false, 50)]
        public static void CreateSpline()
        {
            var spline = NewSpline();
            Selection.activeGameObject = spline.gameObject;
        }

        public static BezierSpline NewSpline()
        {
            var go = new GameObject("Spline");
            var spline = go.AddComponent<BezierSpline>();
            go.transform.position = SceneView.lastActiveSceneView.pivot;
            return spline;
        }

        private void OnSceneGUI()
        {
            var spline = target as BezierSpline;
            if (bezierSplineEditor == null) {
                bezierSplineEditor = new BezierSplineEditor();
            }

            UpdateSelectedIndex();
            bezierSplineEditor.OnSceneGUI(spline);
        }

        private void UpdateSelectedIndex()
        {
            if (selectedIndex != bezierSplineEditor.SelectedIndex) {
                selectedIndex = bezierSplineEditor.SelectedIndex;
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            var spline = target as BezierSpline;
            if (spline == null) return;
            if (bezierSplineEditor == null) {
                bezierSplineEditor = new BezierSplineEditor();
            }

            UpdateSelectedIndex();

            EditorGUILayout.HelpBox("A Bezier Spline is composed of multiple points that are connected together. " +
                                    "You can add more points using the button below.\n\n" +
                                    "To change the position of a point, just click on the point, and then use the usual Unity's Move Tool to move it.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Once you are happy with the shape of the spline, " +
                                    "select the Digger Master object to show its inspector, and then " +
                                    "chose 'Spline' operation to perform an operation along the spline.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (selectedIndex >= 0 && selectedIndex < spline.ControlPointCount) 
                DrawSelectedPointInspector(spline);
            
            if (GUILayout.Button("Add Point")) {
                Undo.RecordObject(spline, "Add Point");
                spline.AddCurve();
                EditorUtility.SetDirty(spline);
            }
            
            if (selectedIndex == spline.GetControlPointIndex(selectedIndex) && GUILayout.Button("Remove Point")) {
                Undo.RecordObject(spline, "Remove Point");
                bezierSplineEditor.Unselect();
                spline.RemoveCurve(selectedIndex);
                UpdateSelectedIndex();
                EditorUtility.SetDirty(spline);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Procedural Generation", EditorStyles.boldLabel);
            EditorGUILayout.MinMaxSlider("Min/Max altitude variation", ref spline.minY, ref spline.maxY, -200f, 200f);
            spline.altitudeVariationFrequency = EditorGUILayout.FloatField("Altitude variation frequency", spline.altitudeVariationFrequency);
            spline.horizontalVariationFrequency = EditorGUILayout.FloatField("Horizontal variation frequency", spline.horizontalVariationFrequency);
            spline.step = EditorGUILayout.FloatField(new GUIContent("Step", "Distance between 2 points of the curve"), spline.step);
            spline.stepCount = EditorGUILayout.IntField(new GUIContent("Step count", "Number of points in the curve (ie. curve length)"), spline.stepCount);
            spline.seed1 = EditorGUILayout.IntField(new GUIContent("Seed 1", "Seed for the noise controlling x coordinate of points forming the cave"), spline.seed1);
            spline.seed2 = EditorGUILayout.IntField(new GUIContent("Seed 2", "Seed for the noise controlling z coordinate of points forming the cave"), spline.seed2);
            spline.seed3 = EditorGUILayout.IntField(new GUIContent("Seed 3", "Seed for the noise controlling y coordinate of points forming the cave"), spline.seed3);

            if (GUILayout.Button("Generate Cave")) {
                Undo.RecordObject(spline, "Generate Cave");
                var generator = new CaveGenerator(spline.step, spline.stepCount, spline.minY, spline.maxY, spline.altitudeVariationFrequency, spline.horizontalVariationFrequency, spline.seed1, spline.seed2, spline.seed3);
                generator.GeneratePoints(spline.transform.position, spline);
                EditorUtility.SetDirty(spline);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedPointInspector(BezierSpline spline)
        {
            GUILayout.Label("Selected Point");
            EditorGUI.BeginChangeCheck();
            var point = EditorGUILayout.Vector3Field("Position", spline.GetControlPoint(selectedIndex));
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(spline, "Move Point");
                EditorUtility.SetDirty(spline);
                spline.SetControlPoint(selectedIndex, point);
            }

            EditorGUI.BeginChangeCheck();
            var mode = (BezierControlPointMode)EditorGUILayout.EnumPopup("Mode", spline.GetControlPointMode(selectedIndex));
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(spline, "Change Point Mode");
                spline.SetControlPointMode(selectedIndex, mode);
                EditorUtility.SetDirty(spline);
            }
        }
    }
}