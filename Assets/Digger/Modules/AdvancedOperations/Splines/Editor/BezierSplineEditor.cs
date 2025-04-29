using UnityEditor;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Splines.Editor
{
    public class BezierSplineEditor
    {
        private const int stepsPerCurve = 10;
        private const float directionScale = 0.1f;
        private const float handleSize = 0.1f;
        private const float pickSize = 0.1f;

        private static readonly Color[] modeColors =
        {
            Color.grey,
            Color.green,
            Color.cyan
        };

        private Quaternion handleRotation;
        private Transform handleTransform;
        private int selectedIndex = -1;
        private int selectedControlPointIndex = -1;

        public int SelectedIndex => selectedIndex;

        public void OnSceneGUI(BezierSpline spline)
        {
            handleTransform = spline.transform;
            handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;

            var p0 = ShowPoint(0, spline);
            for (var i = 1; i < spline.ControlPointCount; i += 3) {
                var p1 = ShowPoint(i, spline);
                var p2 = ShowPoint(i + 1, spline);
                var p3 = ShowPoint(i + 2, spline);

                Handles.color = Color.grey;
                if (((i + 1) / 3) * 3 == selectedControlPointIndex) {
                    Handles.DrawLine(p0, p1);
                }

                if (((i + 4) / 3) * 3 == selectedControlPointIndex) {
                    Handles.DrawLine(p2, p3);
                }

                Handles.DrawBezier(p0, p3, p1, p2, Color.white, null, 3f);
                p0 = p3;
            }

            ShowDirections(spline);
        }

        public void Unselect()
        {
            selectedIndex = selectedControlPointIndex = -1;
        }

        private void ShowDirections(BezierSpline spline)
        {
            Handles.color = Color.green;
            var point = spline.GetPoint(0f);
            Handles.DrawLine(point, point + spline.GetDirection(0f) * directionScale);
            var steps = stepsPerCurve * spline.CurveCount;
            for (var i = 1; i <= steps; i++) {
                point = spline.GetPoint(i / (float)steps);
                Handles.DrawLine(point, point + spline.GetDirection(i / (float)steps) * directionScale);
            }
        }

        private Camera GetSceneCamera()
        {
            if (SceneView.currentDrawingSceneView)
                return SceneView.currentDrawingSceneView.camera;
            if (SceneView.lastActiveSceneView)
                return SceneView.lastActiveSceneView.camera;
            return null;
        }

        private Vector3 ShowPoint(int index, BezierSpline spline)
        {
            var point = handleTransform.TransformPoint(spline.GetControlPoint(index));
            var size = HandleUtility.GetHandleSize(point);


            Handles.color = modeColors[(int)spline.GetControlPointMode(index)];
            Handles.CapFunction capFunction = Handles.SphereHandleCap;
            if (index == 0) {
                Handles.color = Color.red;
            } else if (index == ((index + 1) / 3) * 3) {
                capFunction = Handles.CubeHandleCap;
                Handles.color = Color.white;
                var cam = GetSceneCamera();
                if (cam && Physics.Raycast(cam.transform.position, point - cam.transform.position, (point - cam.transform.position).magnitude)) {
                    Handles.color = new Color(0.39f, 0.39f, 0.39f);
                }
            }

            if (index == ((index + 1) / 3) * 3 || ((index + 1) / 3) * 3 == selectedControlPointIndex) {
                if (Handles.Button(point, handleRotation, size * handleSize, size * pickSize, capFunction)) {
                    selectedIndex = index;
                    if (index == ((index + 1) / 3) * 3) {
                        selectedControlPointIndex = index;
                    }

                    HandleUtility.Repaint();
                }
            }

            if (selectedIndex == index) {
                EditorGUI.BeginChangeCheck();
                point = Handles.DoPositionHandle(point, handleRotation);
                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(spline, "Move Point");
                    EditorUtility.SetDirty(spline);
                    spline.SetControlPoint(index, handleTransform.InverseTransformPoint(point));
                }
            }

            return point;
        }
    }
}