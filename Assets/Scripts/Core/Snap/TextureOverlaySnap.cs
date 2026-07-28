using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Прилипание области накладки к областям СОСЕДНИХ накладок той же
    /// грани — та же «привязка к деталям» (<see cref="KitchenSettings.SnapEnabled"/>
    /// и её порог), только в плоскости грани и в миллиметрах области, а не в
    /// мировых юнитах.
    ///
    /// Почему отдельно от <see cref="ResizeSnap"/>: там снэп ищет встречные грани
    /// коробок в мире, здесь — рёбра плоских прямоугольников на ОДНОЙ и той же
    /// грани. Общая у них только идея порога; вся геометрия разная, и попытка
    /// свести их к одному коду дала бы условный код на каждом шаге.
    ///
    /// Перекрытия по поперечной оси НЕ требуем: две накладки на стене прилипают и
    /// встык (общее ребро), и по одной линии (выравнивание краёв) — на плоскости
    /// оба жеста одинаково осмысленны, в отличие от контакта двух коробок.</summary>
    public static class TextureOverlaySnap
    {
        /// <summary>Координаты рёбер соседних накладок на грани faceIndex вдоль
        /// оси U (alongU) или V, мм от левого нижнего угла грани.
        ///
        /// Накладка «(все)» тоже участвует: она лежит и на этой грани, просто
        /// сразу на шести.</summary>
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
                if (r.width <= 0 || r.height <= 0) continue; // накладка уехала с грани

                if (alongU) { coords.Add(r.xMin); coords.Add(r.xMax); }
                else { coords.Add(r.yMin); coords.Add(r.yMax); }
            }
            return coords;
        }

        /// <summary>Ближайшая координата из набора в пределах порога (инклюзивно,
        /// как в <see cref="ResizeSnap"/>). false — прилипать не к чему.</summary>
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

        /// <summary>Прилипание при ПЕРЕНОСЕ: область едет целиком, поэтому
        /// кандидатов два — переднее и заднее ребро вдоль оси движения. Побеждает
        /// то, что ближе к соседскому ребру; размер области не меняется, а сдвиг
        /// упирается в края грани, чтобы прилипание не вытолкнуло её за грань.</summary>
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

            int x0 = alongU
                ? Mathf.Clamp(rect.xMin + shift, 0, Mathf.Max(0, faceMM.x - rect.width))
                : rect.xMin;
            int y0 = alongU
                ? rect.yMin
                : Mathf.Clamp(rect.yMin + shift, 0, Mathf.Max(0, faceMM.y - rect.height));
            return new RectInt(x0, y0, rect.width, rect.height);
        }

        /// <summary>Порог привязки в мм, 0 — привязка выключена в настройках.
        /// Порог тот же, что у деталей: пользователь настраивает его один раз.</summary>
        public static float ThresholdMM()
        {
            var settings = KitchenSettings.Instance;
            return settings != null && settings.SnapEnabled ? settings.SnapThreshold : 0f;
        }
    }
}
