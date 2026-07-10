using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Прилипание движущейся грани (ресайз ручками) к встречной грани
    /// другого объекта: возвращает доп. сдвиг вдоль нормали, чтобы плоскости стали
    /// заподлицо. Чистая функция — покрывается юнит-тестами.</summary>
    public static class ResizeSnap
    {
        private const float OverlapMin = 0.3f;
        // Инклюзивный порог: снэп срабатывает и ровно на границе (как в SnapSystem).
        private const float ThresholdEpsilon = 1e-5f;

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
                    if (Vector3.Dot(g.normal, normal) > -0.999f) continue; // только встречные

                    float d = Vector3.Dot(g.center - faceCenter, normal); // вдоль нормали до плоскости g
                    if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;

                    Rect oRect = RectFor(g.center, uAxis, vAxis, g.rightAxis, g.upAxis, g.size.x, g.size.y);
                    if (!Overlap(mRect, oRect, out float ratio) || ratio < OverlapMin) continue;

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

        private static bool Overlap(Rect a, Rect b, out float ratio)
        {
            float left = Mathf.Max(a.xMin, b.xMin);
            float right = Mathf.Min(a.xMax, b.xMax);
            float bottom = Mathf.Max(a.yMin, b.yMin);
            float top = Mathf.Min(a.yMax, b.yMax);
            if (left >= right || bottom >= top) { ratio = 0f; return false; }
            float inter = (right - left) * (top - bottom);
            float minArea = Mathf.Min(a.width * a.height, b.width * b.height);
            ratio = minArea > 0 ? inter / minArea : 0f;
            return true;
        }
    }
}
