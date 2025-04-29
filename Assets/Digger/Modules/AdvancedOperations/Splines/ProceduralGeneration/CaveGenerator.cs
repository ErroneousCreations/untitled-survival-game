using Unity.Mathematics;
using UnityEngine;

namespace Digger.Modules.AdvancedOperations.Splines.ProceduralGeneration
{
    public class CaveGenerator
    {
        private float minY;
        private float maxY;
        private float altitudeVariationFrequency;
        private float horizontalVariationFrequency;
        private float step;
        private int stepCount;
        private int seed1, seed2, seed3;

        public CaveGenerator(float step = 4f,
            int stepCount = 100,
            float minY = -20f,
            float maxY = 20f,
            float altitudeVariationFrequency = 0.03f,
            float horizontalVariationFrequency = 0.05f,
            int seed1 = 1337,
            int seed2 = 13,
            int seed3 = 17)
        {
            this.stepCount = stepCount;
            this.step = step;
            this.minY = minY;
            this.maxY = maxY;
            this.altitudeVariationFrequency = altitudeVariationFrequency;
            this.horizontalVariationFrequency = horizontalVariationFrequency;
            this.seed1 = seed1;
            this.seed2 = seed2;
            this.seed3 = seed3;
        }

        public void GeneratePoints(Vector3 startPosition, BezierSpline spline)
        {
            var noiseX = new FastNoise(seed1, horizontalVariationFrequency);
            var noiseZ = new FastNoise(seed2, horizontalVariationFrequency);
            var noiseY = new FastNoise(seed3, altitudeVariationFrequency);
            var pos = new float3(startPosition);

            spline.transform.position = pos;
            var startAltitude = math.lerp(minY, maxY, math.clamp(noiseY.GetSimplex(pos.x, pos.z) * 0.5f + 0.5f, 0, 1));
            startPosition.y -= startAltitude;

            for (var i = 0; i < stepCount; i++) {
                pos.x += noiseX.GetSimplex(i, 0) * step;
                pos.z += noiseZ.GetSimplex(i, 0) * step;
                pos.y = startPosition.y + math.lerp(minY, maxY, math.clamp(noiseY.GetSimplex(pos.x, pos.z) * 0.5f + 0.5f, 0, 1));
                if (i == 0) {
                    spline.ForceReset(pos);
                } else {
                    spline.AddCurve(BezierControlPointMode.Aligned, pos);
                }
            }
        }
    }
}