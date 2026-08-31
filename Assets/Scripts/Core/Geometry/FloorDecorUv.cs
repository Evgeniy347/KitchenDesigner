using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class FloorDecorUv
    {
        public static Vector2 TopFace(Vector2 localMm, Vector2 worldOriginMm, Vector2Int surfaceMm)
            => new Vector2((localMm.x + worldOriginMm.x) / Span(surfaceMm.x),
                (localMm.y + worldOriginMm.y) / Span(surfaceMm.y));

        public static Vector2 SideFace(float alongPerimeterMm, float aboveBottomMm, Vector2Int surfaceMm)
            => new Vector2(alongPerimeterMm / Span(surfaceMm.x), aboveBottomMm / Span(surfaceMm.y));

        private static float Span(int mm) => Mathf.Max(1, Mathf.Abs(mm));
    }
}
