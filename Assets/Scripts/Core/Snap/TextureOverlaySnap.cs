using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class TextureOverlaySnap
    {
        public static List<int> NeighbourEdges(
            IReadOnlyList<TextureOverlaySpec>? overlays, int selfIndex,
            int faceIndex, Vector2Int faceMM, bool alongU)
        {
            var coords = new List<int>();
            if (overlays == null) return coords;

            for (int i = 0; i < overlays.Count; i++)
            {
                if (i == selfIndex) continue;
                if (System.Array.IndexOf(
                        TextureOverlayGeometry.FaceIndices(overlays[i].side), faceIndex) < 0) continue;

                var r = overlays[i].Resolve(faceMM);
                bool resolvedOffTheFace = r.width <= 0 || r.height <= 0;
                if (resolvedOffTheFace) continue;

                if (alongU) { coords.Add(r.xMin); coords.Add(r.xMax); }
                else { coords.Add(r.yMin); coords.Add(r.yMax); }
            }
            return coords;
        }

        public static bool Nearest(IReadOnlyList<int>? coords, float value,
            float thresholdMM, out int snapped)
        {
            snapped = 0;
            if (coords == null || thresholdMM <= 0f) return false;

            float bestDist = thresholdMM + Tolerance.SnapEpsilon;
            bool found = false;
            for (int i = 0; i < coords.Count; i++)
            {
                float d = Mathf.Abs(coords[i] - value);
                if (d > bestDist) continue;
                bestDist = d;
                snapped = coords[i];
                found = true;
            }
            return found;
        }

        public static RectInt SnapMoved(RectInt rect, bool alongU,
            IReadOnlyList<int>? coords, float thresholdMM, Vector2Int faceMM)
        {
            int lo = alongU ? rect.xMin : rect.yMin;
            int hi = alongU ? rect.xMax : rect.yMax;

            bool okLo = Nearest(coords, lo, thresholdMM, out int sLo);
            bool okHi = Nearest(coords, hi, thresholdMM, out int sHi);

            int shift;
            if (okLo && okHi) shift = Mathf.Abs(sLo - lo) <= Mathf.Abs(sHi - hi) ? sLo - lo : sHi - hi;
            else if (okLo) shift = sLo - lo;
            else if (okHi) shift = sHi - hi;
            else return rect;
            if (shift == 0) return rect;

            int maxX0 = Mathf.Max(0, faceMM.x - rect.width);
            int maxY0 = Mathf.Max(0, faceMM.y - rect.height);
            int x0 = alongU ? Mathf.Clamp(rect.xMin + shift, 0, maxX0) : rect.xMin;
            int y0 = alongU ? rect.yMin : Mathf.Clamp(rect.yMin + shift, 0, maxY0);
            return new RectInt(x0, y0, rect.width, rect.height);
        }

        public static float ThresholdMM()
        {
            var settings = KitchenSettings.Instance;
            return settings != null && settings.SnapEnabled ? settings.SnapThreshold : 0f;
        }
    }
}
