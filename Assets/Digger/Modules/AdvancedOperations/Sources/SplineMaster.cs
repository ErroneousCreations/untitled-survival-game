using Digger.Modules.AdvancedOperations.Splines;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Sources
{
    public class SplineMaster : MonoBehaviour
    {
        [SerializeField] private BezierSpline spline;

        public BezierSpline Spline {
            get => spline;
            set => spline = value;
        }
    }
}