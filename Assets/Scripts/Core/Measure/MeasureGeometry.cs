using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Measure
{
    /// <summary>Вся математика рулетки чистыми функциями — без сцены и камеры,
    /// поэтому покрывается EditMode-тестами напрямую.</summary>
    public static class MeasureGeometry
    {
        /// <summary>Мировые юниты → миллиметры.</summary>
        public static float ToMm(float units) => units / AppConstants.MM_TO_UNITS;

        /// <summary>Проекция свободной точки на одну ось: из дельты берётся
        /// компонента с наибольшим модулем, остальные обнуляются. Так «отрезок
        /// строится ТОЛЬКО по осям» — X, Y или Z, в зависимости от того, куда
        /// пользователь увёл курсор дальше всего.</summary>
        public static Vector3 ProjectOnDominantAxis(Vector3 anchor, Vector3 target)
        {
            Vector3 d = target - anchor;
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            if (ax >= ay && ax >= az) return anchor + new Vector3(d.x, 0f, 0f);
            if (ay >= az) return anchor + new Vector3(0f, d.y, 0f);
            return anchor + new Vector3(0f, 0f, d.z);
        }

        /// <summary>Ось отрезка: 0=X, 1=Y, 2=Z, −1 — если он не лежит строго ни на
        /// одной оси (две координаты из трёх должны совпадать). Вырожденный
        /// отрезок нулевой длины оси не имеет.</summary>
        public static int AxisOf(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            bool sameX = Mathf.Abs(d.x) < Tolerance.EpsilonUnits;
            bool sameY = Mathf.Abs(d.y) < Tolerance.EpsilonUnits;
            bool sameZ = Mathf.Abs(d.z) < Tolerance.EpsilonUnits;

            if (sameY && sameZ && !sameX) return 0;
            if (sameX && sameZ && !sameY) return 1;
            if (sameX && sameY && !sameZ) return 2;
            return -1;
        }

        /// <summary>Индекс ближайшей к курсору точки в пределах радиуса (пиксели)
        /// или −1. Радиус — «помощь попадания»: точно навести на вершину мышью
        /// нереально.</summary>
        public static int NearestIndex(IReadOnlyList<Vector2> screenPoints, Vector2 mouse, float radiusPx)
        {
            int best = -1;
            float bestSqr = radiusPx * radiusPx;
            for (int i = 0; i < screenPoints.Count; i++)
            {
                float sqr = (screenPoints[i] - mouse).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Экранное расстояние от точки до отрезка (пиксели). Проекция
        /// зажимается концами, поэтому мимо торца отрезка попасть нельзя.</summary>
        public static float DistancePointToSegmentPx(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr < Tolerance.EpsilonSqr) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Подпись замера: целые миллиметры (правило 1 UI-GUIDELINES).
        /// У диагонального отрезка спереди глиф прямого угла — «замер не по оси».</summary>
        public static string FormatMm(float lengthUnits, bool axisAligned)
        {
            int mm = Mathf.RoundToInt(ToMm(lengthUnits));
            return axisAligned ? $"{mm} мм" : $"{UI.UIStyle.GlyphAngle} {mm} мм";
        }

        /// <summary>Мировой радиус, дающий на экране заданный размер в пикселях.
        /// Точка замера и штрих пунктира не должны «худеть» при отдалении.</summary>
        public static float WorldSizeForPixels(Camera camera, Vector3 worldPoint, float pixels)
        {
            if (camera == null) return 0f;
            if (camera.orthographic)
                return pixels * (2f * camera.orthographicSize / Mathf.Max(1, camera.pixelHeight));

            float dist = Vector3.Dot(worldPoint - camera.transform.position, camera.transform.forward);
            dist = Mathf.Max(dist, Tolerance.EpsilonUnits);
            float worldPerPixel = 2f * dist * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                                  / Mathf.Max(1, camera.pixelHeight);
            return pixels * worldPerPixel;
        }
    }
}
