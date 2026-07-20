using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Прилипание движущейся грани (ресайз ручками) к встречной грани
    /// другого объекта: возвращает доп. сдвиг вдоль нормали, чтобы плоскости стали
    /// заподлицо. Чистая функция — покрывается юнит-тестами.</summary>
    public static class ResizeSnap
    {
        // Инклюзивный порог: снэп срабатывает и ровно на границе (как в SnapSystem).
        private const float ThresholdEpsilon = Tolerance.SnapEpsilon;

        /// <summary>Ищет ближайшую встречную грань (нормаль противоположна) в пределах
        /// порога вдоль нормали и с перекрытием в плоскости. Возвращает зазор
        /// (units, со знаком) для выравнивания плоскостей заподлицо. false — снэпа нет.</summary>
        public static bool SnapDelta(
            Vector3 faceCenter, Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector2 faceSize,
            IList<KitchenElement> others, KitchenElement self, float threshold, out float gap)
        {
            gap = 0f;
            if (others == null) return false;

            Rect mRect = RectFor(faceCenter, uAxis, vAxis, uAxis, vAxis, faceSize.x, faceSize.y);
            float bestAbs = float.MaxValue;
            bool found = false;

            foreach (var o in others)
            {
                if (o == null || o == self) continue;
                if (!o.gameObject.activeInHierarchy) continue;

                var faces = o.GetFaces();
                for (int j = 0; j < faces.Length; j++)
                {
                    var g = faces[j];
                    if (Vector3.Dot(g.normal, normal) > -Tolerance.ParallelDot) continue; // только встречные

                    float d = Vector3.Dot(g.center - faceCenter, normal); // вдоль нормали до плоскости g
                    if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;

                    // Достаточно любого положительного перекрытия в плоскости: при
                    // ресайзе грань часто стыкуется с соседом в углу (буква «Г»),
                    // где перекрытие частичное — жёсткий порог его бы отбросил.
                    Rect oRect = RectFor(g.center, uAxis, vAxis, g.rightAxis, g.upAxis, g.size.x, g.size.y);
                    if (!Overlap(mRect, oRect)) continue;

                    if (Mathf.Abs(d) < bestAbs)
                    {
                        bestAbs = Mathf.Abs(d);
                        gap = d;
                        found = true;
                    }
                }

                // Стенки пазов — разметочные плоскости, а не поверхности материала:
                // растягиваемая деталь прилегает к пласти СНАРУЖИ и в паз не заходит.
                // Отсюда два послабления против обычной грани:
                //  • нормаль не обязана быть встречной — плоскость двусторонняя;
                //  • перекрытие проверяем только ВДОЛЬ ДЛИНЫ паза, по глубине его нет.
                foreach (var wall in o.GetGrooveWallFaces())
                {
                    if (Mathf.Abs(Vector3.Dot(wall.normal, normal)) < Tolerance.ParallelDot) continue;

                    float d = Vector3.Dot(wall.center - faceCenter, normal);
                    if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;
                    if (!OverlapsAlongGroove(faceCenter, uAxis, vAxis, faceSize, wall)) continue;

                    if (Mathf.Abs(d) < bestAbs)
                    {
                        bestAbs = Mathf.Abs(d);
                        gap = d;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>Пересекается ли растягиваемая грань со стенкой паза ВДОЛЬ ДЛИНЫ
        /// паза. Только по длине: по глубине деталь с пазом не перекрывается, она
        /// стоит у пласти снаружи.</summary>
        private static bool OverlapsAlongGroove(Vector3 faceCenter, Vector3 uAxis, Vector3 vAxis,
            Vector2 faceSize, KitchenElement.Face wall)
        {
            Vector3 lengthAxis = wall.rightAxis; // у стенки паза rightAxis — вдоль длины
            float faceHalf = Mathf.Abs(Vector3.Dot(uAxis, lengthAxis)) * faceSize.x * 0.5f
                           + Mathf.Abs(Vector3.Dot(vAxis, lengthAxis)) * faceSize.y * 0.5f;
            float wallHalf = wall.size.x * 0.5f;
            float centreGap = Mathf.Abs(Vector3.Dot(wall.center - faceCenter, lengthAxis));
            return centreGap < faceHalf + wallHalf;
        }

        // Прямоугольник грани в координатах (u,v). Полуразмеры — проекции собственных
        // осей грани (right/up) на u,v, как в SnapSystem.
        private static Rect RectFor(Vector3 center, Vector3 u, Vector3 v,
            Vector3 rightAxis, Vector3 upAxis, float sizeX, float sizeY)
        {
            float cu = Vector3.Dot(center, u);
            float cv = Vector3.Dot(center, v);
            float halfU = Mathf.Abs(Vector3.Dot(rightAxis, u)) * sizeX * 0.5f
                        + Mathf.Abs(Vector3.Dot(upAxis, u)) * sizeY * 0.5f;
            float halfV = Mathf.Abs(Vector3.Dot(rightAxis, v)) * sizeX * 0.5f
                        + Mathf.Abs(Vector3.Dot(upAxis, v)) * sizeY * 0.5f;
            return new Rect(cu - halfU, cv - halfV, halfU * 2f, halfV * 2f);
        }

        private static bool Overlap(Rect a, Rect b)
        {
            float left = Mathf.Max(a.xMin, b.xMin);
            float right = Mathf.Min(a.xMax, b.xMax);
            float bottom = Mathf.Max(a.yMin, b.yMin);
            float top = Mathf.Min(a.yMax, b.yMax);
            return left < right && bottom < top; // строго положительная площадь
        }
    }
}
