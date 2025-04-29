using System;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Splines
{
    public class BezierSpline : MonoBehaviour
    {
        [SerializeField] private Vector3[] points;

        [SerializeField] private BezierControlPointMode[] modes;

        [SerializeField] private bool loop;

        // Procedural generation fields
        public float minY = -20f;
        public float maxY = 20f;
        public float altitudeVariationFrequency = 0.03f;
        public float horizontalVariationFrequency = 0.05f;
        public float step = 4f;
        public int stepCount = 100;
        public int seed1 = 1337, seed2 = 13, seed3 = 17;

        public bool Loop {
            get => loop;
            set {
                loop = value;
                if (value) {
                    modes[modes.Length - 1] = modes[0];
                    SetControlPoint(0, points[0]);
                }
            }
        }

        public int ControlPointCount => points.Length;

        public int CurveCount => (points.Length - 1) / 3;

        public void Reset()
        {
            ForceReset(null);
        }

        public void ForceReset(Vector3? firstPoint)
        {
            loop = false;

            var p1 = firstPoint.HasValue ? transform.InverseTransformPoint(firstPoint.Value) : new Vector3(4f, 0f, 0f);
            points = new[]
            {
                Vector3.zero,
                (Vector3.zero + p1) * 0.5f - (p1 - Vector3.zero) * 0.01f,
                (Vector3.zero + p1) * 0.5f + (p1 - Vector3.zero) * 0.01f,
                p1
            };
            modes = new[]
            {
                BezierControlPointMode.Free,
                BezierControlPointMode.Free
            };
        }

        public Vector3 GetControlPoint(int index)
        {
            return points[index];
        }
        
        public int GetControlPointIndex(int index)
        {
            return ((index + 1) / 3) * 3;
        }

        public float GetApproxLength()
        {
            var len = 0f;
            for (var i = 0; i < points.Length - 3; i += 3) {
                len += Vector3.Distance(points[i], points[i + 3]);
            }

            return len;
        }

        public void SetControlPoint(int index, Vector3 point)
        {
            if (index % 3 == 0) {
                var delta = point - points[index];
                if (loop) {
                    if (index == 0) {
                        points[1] += delta;
                        points[points.Length - 2] += delta;
                        points[points.Length - 1] = point;
                    } else if (index == points.Length - 1) {
                        points[0] = point;
                        points[1] += delta;
                        points[index - 1] += delta;
                    } else {
                        points[index - 1] += delta;
                        points[index + 1] += delta;
                    }
                } else {
                    if (index > 0) points[index - 1] += delta;
                    if (index + 1 < points.Length) points[index + 1] += delta;
                }
            }

            points[index] = point;
            EnforceMode(index);
        }

        public BezierControlPointMode GetControlPointMode(int index)
        {
            return modes[(index + 1) / 3];
        }

        public void SetControlPointMode(int index, BezierControlPointMode mode)
        {
            var modeIndex = (index + 1) / 3;
            modes[modeIndex] = mode;
            if (loop) {
                if (modeIndex == 0)
                    modes[modes.Length - 1] = mode;
                else if (modeIndex == modes.Length - 1) modes[0] = mode;
            }

            EnforceMode(index);
        }

        private void EnforceMode(int index)
        {
            var modeIndex = (index + 1) / 3;
            var mode = modes[modeIndex];
            if (mode == BezierControlPointMode.Free || !loop && (modeIndex == 0 || modeIndex == modes.Length - 1)) return;

            var middleIndex = modeIndex * 3;
            int fixedIndex, enforcedIndex;
            if (index <= middleIndex) {
                fixedIndex = middleIndex - 1;
                if (fixedIndex < 0) fixedIndex = points.Length - 2;
                enforcedIndex = middleIndex + 1;
                if (enforcedIndex >= points.Length) enforcedIndex = 1;
            } else {
                fixedIndex = middleIndex + 1;
                if (fixedIndex >= points.Length) fixedIndex = 1;
                enforcedIndex = middleIndex - 1;
                if (enforcedIndex < 0) enforcedIndex = points.Length - 2;
            }

            var middle = points[middleIndex];
            var enforcedTangent = middle - points[fixedIndex];
            if (mode == BezierControlPointMode.Aligned) enforcedTangent = enforcedTangent.normalized * Vector3.Distance(middle, points[enforcedIndex]);
            points[enforcedIndex] = middle + enforcedTangent;
        }

        public Vector3 GetPoint(float t)
        {
            int i;
            if (t >= 1f) {
                t = 1f;
                i = points.Length - 4;
            } else {
                t = Mathf.Clamp01(t) * CurveCount;
                i = (int)t;
                t -= i;
                i *= 3;
            }

            return transform.TransformPoint(Bezier.GetPoint(points[i], points[i + 1], points[i + 2], points[i + 3], t));
        }

        public Vector3 GetVelocity(float t)
        {
            int i;
            if (t >= 1f) {
                t = 1f;
                i = points.Length - 4;
            } else {
                t = Mathf.Clamp01(t) * CurveCount;
                i = (int)t;
                t -= i;
                i *= 3;
            }

            return transform.TransformPoint(Bezier.GetFirstDerivative(points[i], points[i + 1], points[i + 2], points[i + 3], t)) - transform.position;
        }

        public Vector3 GetDirection(float t)
        {
            return GetVelocity(t).normalized;
        }

        public void AddCurve(BezierControlPointMode? controlPointMode = null, Vector3? position = null)
        {
            var previousPoint = points[points.Length - 1];
            var point = position.HasValue ? transform.InverseTransformPoint(position.Value) : points[points.Length - 1] + new Vector3(2f, 0, 0);
            Array.Resize(ref points, points.Length + 3);
            points[points.Length - 3] = (point + previousPoint) * 0.5f + (point - previousPoint) * 0.01f;
            points[points.Length - 2] = (point + previousPoint) * 0.5f - (point - previousPoint) * 0.01f;
            points[points.Length - 1] = point;

            Array.Resize(ref modes, modes.Length + 1);
            modes[modes.Length - 1] = controlPointMode ?? modes[modes.Length - 2];
            EnforceMode(points.Length - 4);

            if (loop) {
                points[points.Length - 1] = points[0];
                modes[modes.Length - 1] = modes[0];
                EnforceMode(0);
            }
        }
        
        public void RemoveCurve(int controlPointIndex)
        {
            if (controlPointIndex == 0 || points.Length <= 4)
                return;
            
            var removeIndex = controlPointIndex - 1;
            if (controlPointIndex == points.Length - 1)
                removeIndex = controlPointIndex - 2;
            
            var newPoints = new Vector3[points.Length - 3];
            var ni = 0;
            for (var i = 0; i < points.Length; i++) {
                if (i == removeIndex) {
                    i += 2;
                    continue;
                }
                newPoints[ni] = points[i];
                ni++;
            }

            var modeIndex = (controlPointIndex+1) / 3;
            var newModes = new BezierControlPointMode[modes.Length - 1];
            ni = 0;
            for (var i = 0; i < modes.Length; i++) {
                if (i == modeIndex) {
                    continue;
                }
                newModes[ni] = modes[i];
                ni++;
            }

            points = newPoints;
            modes = newModes;
        }
    }
}