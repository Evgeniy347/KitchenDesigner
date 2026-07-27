using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Геометрия размещения ручек ресайза/перемещения на СТЕНЕ.
    ///
    /// Стена — плоская плита: центры всех её граней лежат в толще, поэтому ручка
    /// тонет в геометрии, а торцевые стрелки уходят по оси стены. Стены стыкуются
    /// по осевым линиям, и торец одной приходится на середину примыкающей — стрелка
    /// начинается ВНУТРИ соседней стены. Здесь считается сдвиг ручек из плоскости
    /// стены к камере и, для «угла», откат стрелки назад на саму стену.
    ///
    /// Чистая геометрия, без Camera.main и синглтонов — покрыта EditMode-тестами.</summary>
    public static class WallHandlePlacement
    {
        /// <summary>Сколько точек стрелки проверяем на попадание в соседнюю стену.
        /// 8 отрезков — первая проба в ~21 мм от грани, ловит и тонкую перегородку.</summary>
        private const int Samples = 8;

        /// <summary>Толщина стены идёт по локальной оси X? (иначе по Z).
        /// Тот же критерий, что при сборке меша стены и привязке окон.</summary>
        public static bool ThickAlongX(Vector3 scale) => scale.x <= scale.z;

        /// <summary>Индекс оси ДЛИНЫ стены в порядке граней (0 = X, 2 = Z).
        /// Грани этой оси — торцы, именно они упираются в соседнюю стену.</summary>
        public static int LengthAxis(Vector3 scale) => ThickAlongX(scale) ? 2 : 0;

        /// <summary>Длина стены в юнитах (по оси длины).</summary>
        public static float Length(Vector3 scale) => ThickAlongX(scale) ? scale.z : scale.x;

        /// <summary>Сдвиг ручек из толщи стены к камере: за плоскость стены плюс зазор.
        /// Сторона выбирается по камере, поэтому ручки видны с любого ракурса.</summary>
        public static Vector3 CameraOffset(
            Vector3 wallPos, Quaternion rot, Vector3 scale, Vector3 camPos, float gap)
        {
            bool thickX = ThickAlongX(scale);
            Vector3 thin = rot * (thickX ? Vector3.right : Vector3.forward);
            float halfThick = (thickX ? scale.x : scale.z) * 0.5f;
            float side = Vector3.Dot(camPos - wallPos, thin) < 0f ? -1f : 1f;
            return thin * (side * (halfThick + gap));
        }

        /// <summary>Точка внутри горизонтального сечения элемента? По Y не проверяем:
        /// в режиме обзора стена опущена (localScale.y уменьшен), но её след на плане
        /// не меняется — иначе «угол» переставал определяться при повороте камеры.</summary>
        public static bool ContainsHorizontally(Transform t, Vector3 world)
        {
            Vector3 l = t.InverseTransformPoint(world); // масштаб учтён: куб = ±0.5
            return Mathf.Abs(l.x) <= 0.5f && Mathf.Abs(l.z) <= 0.5f;
        }

        /// <summary>Стрелка длиной <paramref name="arrowLen"/>, выпущенная из
        /// <paramref name="origin"/> вдоль <paramref name="normal"/>, задевает чужую стену?</summary>
        public static bool Blocked(
            Vector3 origin, Vector3 normal, float arrowLen, IReadOnlyList<KitchenElement>? walls)
        {
            if (walls == null || walls.Count == 0) return false;
            for (int s = 0; s <= Samples; s++)
            {
                Vector3 p = origin + normal * (arrowLen * s / Samples);
                for (int i = 0; i < walls.Count; i++)
                {
                    var w = walls[i];
                    if (w == null) continue;
                    if (ContainsHorizontally(w.transform, p)) return true;
                }
            }
            return false;
        }

        /// <summary>Насколько отвести торцевую ручку НАЗАД, на саму стену, чтобы стрелка
        /// не сидела в примыкающей стене. 0 — торец свободен, отводить не нужно.
        /// Дальше половины длины стены не отходим — ручка должна остаться у своего торца.</summary>
        public static float PullBack(
            Vector3 origin, Vector3 normal, float arrowLen, float maxShift,
            IReadOnlyList<KitchenElement>? walls)
        {
            if (!Blocked(origin, normal, arrowLen, walls)) return 0f;
            float step = arrowLen * 0.5f;
            for (float d = arrowLen; d <= maxShift; d += step)
                if (!Blocked(origin - normal * d, normal, arrowLen, walls)) return d;
            // Чистого места нет (короткая стена в стыке с двух сторон) — всё равно
            // убираем стрелку с продолжения оси: внутри своей стены она хотя бы
            // не прячется за соседнюю.
            return arrowLen;
        }
    }
}
