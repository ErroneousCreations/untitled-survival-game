using UnityEditor;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Splines.Editor
{
    [CustomEditor(typeof(Line))]
    public class LineInspector : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var line = target as Line;
            var handleTransform = line.transform;
            var handleRotation = Tools.pivotRotation == PivotRotation.Local ? handleTransform.rotation : Quaternion.identity;
            var p0 = handleTransform.TransformPoint(line.p0);
            var p1 = handleTransform.TransformPoint(line.p1);

            Handles.color = Color.white;
            Handles.DrawLine(p0, p1);
            EditorGUI.BeginChangeCheck();
            p0 = Handles.DoPositionHandle(p0, handleRotation);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(line, "Move Point");
                EditorUtility.SetDirty(line);
                line.p0 = handleTransform.InverseTransformPoint(p0);
            }

            EditorGUI.BeginChangeCheck();
            p1 = Handles.DoPositionHandle(p1, handleRotation);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(line, "Move Point");
                EditorUtility.SetDirty(line);
                line.p1 = handleTransform.InverseTransformPoint(p1);
            }
        }
    }
}