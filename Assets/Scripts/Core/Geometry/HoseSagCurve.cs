using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class HoseSagCurve
    {
        public const int DefaultSamples = 48;

        public const int SolverIterations = 28;

        public static Vector3 Point(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0
                + 3f * u * u * t * p1
                + 3f * u * t * t * p2
                + t * t * t * p3;
        }

        public static Vector3[] Sample(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples)
        {
            int count = Mathf.Max(1, samples);
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++) points[i] = Point(p0, p1, p2, p3, i / (float)count);
            points[0] = p0;
            points[count] = p3;
            return points;
        }

        public static float HandleLengthMM(Vector3 fromMM, Vector3 fromDirection, Vector3 toMM,
            Vector3 toDirection, float hoseLengthMM, int samples)
        {
            float chordMM = (toMM - fromMM).magnitude;
            if (hoseLengthMM <= chordMM) return 0f;

            var start = Normalized(fromDirection);
            var end = Normalized(toDirection);
            float low = 0f;
            float high = hoseLengthMM;

            if (LengthAt(fromMM, start, toMM, end, high, samples) < hoseLengthMM) return high;

            for (int i = 0; i < SolverIterations; i++)
            {
                float middle = (low + high) * 0.5f;
                if (LengthAt(fromMM, start, toMM, end, middle, samples) < hoseLengthMM)
                    low = middle;
                else
                    high = middle;
            }

            return (low + high) * 0.5f;
        }

        public static Vector3[] Hanging(Vector3 fromMM, Vector3 fromDirection, Vector3 toMM,
            Vector3 toDirection, float hoseLengthMM, int samples)
        {
            float handle = HandleLengthMM(fromMM, fromDirection, toMM, toDirection,
                hoseLengthMM, samples);
            if (handle <= 0f) return new[] { fromMM, toMM };

            return Sample(fromMM, fromMM + Normalized(fromDirection) * handle,
                toMM + Normalized(toDirection) * handle, toMM, samples);
        }

        private static float LengthAt(Vector3 fromMM, Vector3 startDirection, Vector3 toMM,
            Vector3 endDirection, float handleMM, int samples) =>
            PipePath.LengthMM(Sample(fromMM, fromMM + startDirection * handleMM,
                toMM + endDirection * handleMM, toMM, samples));

        private static Vector3 Normalized(Vector3 direction)
        {
            float length = direction.magnitude;
            return length > Tolerance.EpsilonUnits ? direction / length : Vector3.down;
        }
    }
}
