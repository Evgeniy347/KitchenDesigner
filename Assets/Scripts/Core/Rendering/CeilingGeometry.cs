using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Чистый расчёт габаритов потолка по стенам (режим «габаритный бокс»).
    /// Берётся общий AABB всех стен, потолок кладётся плоской плитой на высоте верха
    /// стен, охватывая их XZ-контур с небольшим запасом. Отделено от рендера, чтобы
    /// покрываться юнит-тестами.</summary>
    public static class CeilingGeometry
    {
        /// <summary>Запас по горизонтали от внешнего контура стен (юниты), чтобы
        /// потолок гарантированно перекрывал толщину стен и не оставлял щель.</summary>
        public const float MarginUnits = 0.02f;

        /// <summary>Толщина плиты потолка (юниты).</summary>
        public const float ThicknessUnits = 0.018f;

        /// <summary>Рассчитать положение/размер потолка по мировым AABB стен.
        /// Возвращает false, если стен нет или их суммарный контур вырожден.</summary>
        public static bool TryCompute(IReadOnlyList<Bounds> wallBounds, out Bounds ceiling)
        {
            ceiling = default;
            if (wallBounds == null || wallBounds.Count == 0) return false;

            bool any = false;
            Bounds total = default;
            foreach (var b in wallBounds)
            {
                // Вырожденные (нулевые) стены пропускаем — они не задают контур.
                if (b.size.x <= 1e-4f && b.size.y <= 1e-4f && b.size.z <= 1e-4f) continue;
                if (!any) { total = b; any = true; }
                else total.Encapsulate(b);
            }
            if (!any) return false;

            float topY = total.max.y;
            float sizeX = total.size.x + MarginUnits * 2f;
            float sizeZ = total.size.z + MarginUnits * 2f;
            if (sizeX <= 1e-4f || sizeZ <= 1e-4f) return false;

            var center = new Vector3(
                total.center.x,
                topY + ThicknessUnits * 0.5f,
                total.center.z);
            ceiling = new Bounds(center, new Vector3(sizeX, ThicknessUnits, sizeZ));
            return true;
        }
    }
}
