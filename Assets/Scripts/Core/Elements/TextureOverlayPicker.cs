using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>В какую накладку текстуры попала точка на поверхности элемента.
    ///
    /// Нужно пипетке: у стены есть и базовый декор («Текстура»), и стопка
    /// накладок («Текстуры»), и клик обязан попасть в ту из них, которую человек
    /// видит под курсором. Физика тут не помощник — у накладочных квадов
    /// (см. <see cref="TextureOverlayRenderer"/>) коллайдеров нет, луч всегда
    /// приходит в саму стену, — поэтому слой разбирается геометрически.</summary>
    public static class TextureOverlayPicker
    {
        /// <summary>Накладка под точкой <paramref name="worldPoint"/> на грани с
        /// нормалью <paramref name="worldNormal"/>. Список обходится С КОНЦА:
        /// индекс в нём и есть порядок слоёв (последняя лежит выше всех — она же
        /// приподнята над гранью сильнее, см. TextureOverlayRenderer.LayerLiftMM),
        /// значит на перекрытии выигрывает последняя подходящая.</summary>
        public static bool TryPick(KitchenElement? element, Vector3 worldPoint, Vector3 worldNormal,
            out int overlayIndex, out string materialId)
        {
            overlayIndex = -1;
            materialId = MaterialCatalog.DefaultId;
            if (element == null) return false;

            var overlays = element.TextureOverlays;
            if (overlays.Count == 0) return false;

            int faceIndex = NearestFaceIndex(element, worldNormal);
            if (faceIndex < 0) return false;
            if (!TryFacePointMM(element, faceIndex, worldPoint, out Vector2Int uv)) return false;

            var faceMM = TextureOverlayGeometry.FaceSizeMM(element.DimensionsMM, faceIndex);
            for (int i = overlays.Count - 1; i >= 0; i--)
            {
                var spec = overlays[i];
                if (!CoversFace(spec.side, faceIndex)) continue;
                if (!spec.Resolve(faceMM).Contains(uv)) continue;

                overlayIndex = i;
                materialId = spec.MaterialId;
                return true;
            }
            return false;
        }

        /// <summary>Грань, на которую смотрит нормаль попадания. Именно по
        /// нормали, а не по расстоянию до центров: у тонкой стены центры широких
        /// граней стоят в 50 мм друг от друга, и точка у самого края торца
        /// оказалась бы ближе к чужой грани.</summary>
        public static int NearestFaceIndex(KitchenElement element, Vector3 worldNormal)
        {
            if (element == null || worldNormal.sqrMagnitude < Mathf.Epsilon) return -1;

            var faces = element.GetFaces();
            int best = -1;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < faces.Length; i++)
            {
                float dot = Vector3.Dot(faces[i].normal.normalized, worldNormal.normalized);
                if (dot <= bestDot) continue;
                bestDot = dot;
                best = i;
            }
            return best;
        }

        /// <summary>Точка в координатах грани (мм от её левого нижнего угла — те
        /// же оси, в которых задана область накладки). false, если точка вышла за
        /// границы грани: в накладку она в любом случае не попала.</summary>
        public static bool TryFacePointMM(KitchenElement element, int faceIndex,
            Vector3 worldPoint, out Vector2Int uv)
        {
            uv = Vector2Int.zero;
            var faces = element.GetFaces();
            if (faceIndex < 0 || faceIndex >= faces.Length) return false;

            var face = faces[faceIndex];
            var faceMM = TextureOverlayGeometry.FaceSizeMM(element.DimensionsMM, faceIndex);
            var delta = worldPoint - face.center;

            float u = Vector3.Dot(delta, face.rightAxis) / AppConstants.MM_TO_UNITS + faceMM.x * 0.5f;
            float v = Vector3.Dot(delta, face.upAxis) / AppConstants.MM_TO_UNITS + faceMM.y * 0.5f;

            // Округление, а не отбрасывание дробной части: точка приходит из
            // физического луча в метрах, и на 1200 мм ошибка float легко даёт
            // 1199,9999 — целая часть промахнулась бы на миллиметр.
            uv = new Vector2Int(Mathf.RoundToInt(u), Mathf.RoundToInt(v));
            return uv.x >= 0 && uv.y >= 0 && uv.x <= faceMM.x && uv.y <= faceMM.y;
        }

        private static bool CoversFace(OverlaySide side, int faceIndex)
        {
            if (side == OverlaySide.All) return true;
            return (int)side == faceIndex;
        }
    }
}
