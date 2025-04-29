using UnityEditor;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Splines.Editor
{
    [CustomEditor(typeof(BezierCurve))]
    public class BezierCurveInspector : UnityEditor.Editor
    {
        private const int lineSteps = 10;
        private const float directionScale = 0.5f;

        private BezierCurve curve;
        private Quaternion handleRotation;
        private Transform handleTransform;

        private void OnSceneGUI()
        {
            curve = target as BezierCurve;
            handleTransform = curve.transform;
            handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;

            var p0 = ShowPoint(0);
            var p1 = ShowPoint(1);
            var p2 = ShowPoint(2);
            var p3 = ShowPoint(3);

            Handles.color = Color.gray;
            Handles.DrawLine(p0, p1);
            Handles.DrawLine(p2, p3);

            ShowDirections();
            Handles.DrawBezier(p0, p3, p1, p2, Color.white, null, 2f);
        }

        private void ShowDirections()
        {
            Handles.color = Color.green;
            var point = curve.GetPoint(0f);
            Handles.DrawLine(point, point + curve.GetDirection(0f) * directionScale);
            for (var i = 1; i <= lineSteps; i++) {
                point = curve.GetPoint(i / (float)lineSteps);
                Handles.DrawLine(point, point + curve.GetDirection(i / (float)lineSteps) * directionScale);
            }
        }

        private Vector3 ShowPoint(int index)
        {
            var point = handleTransform.TransformPoint(curve.points[index]);
            EditorGUI.BeginChangeCheck();
            point = Handles.DoPositionHandle(point, handleRotation);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(curve, "Move Point");
                EditorUtility.SetDirty(curve);
                curve.points[index] = handleTransform.InverseTransformPoint(point);
            }

            return point;
        }
    }
}