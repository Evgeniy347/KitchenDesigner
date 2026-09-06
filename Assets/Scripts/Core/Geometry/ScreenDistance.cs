using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ScreenDistance
    {
        public static float PointToSegmentPixels(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr < Tolerance.EpsilonSqr) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
