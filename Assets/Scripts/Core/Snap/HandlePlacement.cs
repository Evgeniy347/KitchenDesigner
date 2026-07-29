using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Геометрия размещения ручек ресайза/перемещения на ПЛОСКОЙ детали
    /// (стена, полка, боковина, фасад, столешница).
    ///
    /// У плиты центры боковых граней лежат в её толще: ручка тонет в геометрии,
    /// а стрелка уходит по оси плиты и попадает внутрь соседней детали — торец
    /// стены приходится в толщу примыкающей, конец полки упирается в боковину.
    /// Здесь считается сдвиг ручек из плоскости детали к камере и, когда стрелке
    /// некуда выйти, откат её назад на саму деталь.
    ///
    /// Чистая геометрия, без Camera.main и синглтонов — покрыта EditMode-тестами.</summary>
    public static class HandlePlacement
    {
        /// <summary>Во сколько раз тонкая ось должна быть меньше остальных, чтобы
        /// деталь считалась плитой. 3 — столешница 38 мм при глубине 600 ещё плита,
        /// а опора 50×50×100 и корпус ящика уже нет: у них ручки и так снаружи.</summary>
        private const float PlateRatio = 3f;

        /// <summary>Ориентированный габаритный ящик детали в мировых осях.
        /// Собирается из её граней, поэтому учитывает и переопределённый габарит
        /// составных элементов, и полную высоту опущенной стены.</summary>
        public readonly struct Box
        {
            public readonly Vector3 Center;
            /// <summary>Единичные оси X/Y/Z детали в мире.</summary>
            public readonly Vector3 AxisX, AxisY, AxisZ;
            /// <summary>Полуразмеры по этим осям (юниты).</summary>
            public readonly Vector3 Half;

            public Box(Vector3 center, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 half)
            {
                Center = center; AxisX = axisX; AxisY = axisY; AxisZ = axisZ; Half = half;
            }

            public Vector3 Axis(int index) => index == 0 ? AxisX : (index == 1 ? AxisY : AxisZ);

            /// <summary>Расстояние от точки до ящика (0 — точка внутри).
            /// Для дешёвого отсева соседей, до которых стрелка заведомо не достаёт.</summary>
            public float DistanceTo(Vector3 world)
            {
                Vector3 d = world - Center;
                float sqr = 0f;
                for (int a = 0; a < 3; a++)
                {
                    float outside = Mathf.Abs(Vector3.Dot(d, Axis(a))) - Half[a];
                    if (outside > 0f) sqr += outside * outside;
                }
                return Mathf.Sqrt(sqr);
            }

            /// <summary>Отрезок [origin, origin + dir*length] заходит внутрь ящика?
            /// Метод плит (slab): точный, в отличие от выборки точек вдоль отрезка —
            /// та проскакивала мимо 18-мм боковины между пробами. Касание грани
            /// (нулевая длина пересечения) не считается: стрелка, упирающаяся
            /// кончиком в соседа, стоит правильно.</summary>
            public bool IntersectsSegment(Vector3 origin, Vector3 dir, float length)
            {
                Vector3 d = origin - Center;
                float tMin = 0f, tMax = length;
                for (int a = 0; a < 3; a++)
                {
                    Vector3 axis = Axis(a);
                    float o = Vector3.Dot(d, axis);
                    float slope = Vector3.Dot(dir, axis);
                    float h = Half[a];
                    if (Mathf.Abs(slope) < Tolerance.EpsilonUnits)
                    {
                        if (Mathf.Abs(o) > h) return false; // идёт вдоль плиты и мимо неё
                        continue;
                    }
                    float t1 = (-h - o) / slope, t2 = (h - o) / slope;
                    if (t1 > t2) (t1, t2) = (t2, t1);
                    tMin = Mathf.Max(tMin, t1);
                    tMax = Mathf.Min(tMax, t2);
                    if (tMin > tMax) return false;
                }
                return tMax - tMin > Tolerance.EpsilonUnits;
            }
        }

        /// <summary>Собирает ящик из граней. Опирается на контракт порядка граней:
        /// index/2 = ось (0=X, 1=Y, 2=Z), чётный индекс — положительное направление.</summary>
        public static Box BoxOf(Face[] faces)
        {
            Vector3 ax = Normal(faces, 0, Vector3.right);
            Vector3 ay = Normal(faces, 2, Vector3.up);
            Vector3 az = Normal(faces, 4, Vector3.forward);
            Vector3 center = (faces[0].center + faces[1].center) * 0.5f;
            var half = new Vector3(
                Mathf.Abs(Vector3.Dot(faces[0].center - center, ax)),
                Mathf.Abs(Vector3.Dot(faces[2].center - center, ay)),
                Mathf.Abs(Vector3.Dot(faces[4].center - center, az)));
            return new Box(center, ax, ay, az, half);
        }

        private static Vector3 Normal(Face[] faces, int index, Vector3 fallback) =>
            faces[index].normal.sqrMagnitude > Tolerance.EpsilonSqr
                ? faces[index].normal.normalized
                : fallback;

        /// <summary>Ось наименьшего габарита, если деталь плоская; -1 — деталь
        /// объёмная, выносить ручки не нужно.</summary>
        public static int ThinAxis(in Box box)
        {
            Vector3 h = box.Half;
            int thin = 0;
            if (h.y < h[thin]) thin = 1;
            if (h.z < h[thin]) thin = 2;
            float other = Mathf.Min(h[(thin + 1) % 3], h[(thin + 2) % 3]);
            return h[thin] * PlateRatio <= other ? thin : -1;
        }

        /// <summary>Сдвиг ручек из толщи детали к камере: за плоскость плюс зазор.
        /// Сторона выбирается по камере, поэтому ручки видны с любого ракурса.</summary>
        public static Vector3 CameraOffset(in Box box, int thinAxis, Vector3 camPos, float gap)
        {
            Vector3 n = box.Axis(thinAxis);
            float side = Vector3.Dot(camPos - box.Center, n) < 0f ? -1f : 1f;
            return n * (side * (box.Half[thinAxis] + gap));
        }

        /// <summary>Стрелка длиной <paramref name="arrowLen"/>, выпущенная из
        /// <paramref name="origin"/> вдоль <paramref name="normal"/>, задевает соседа?</summary>
        public static bool Blocked(
            Vector3 origin, Vector3 normal, float arrowLen, IReadOnlyList<Box>? neighbours)
        {
            if (neighbours == null || neighbours.Count == 0) return false;
            for (int i = 0; i < neighbours.Count; i++)
                if (neighbours[i].IntersectsSegment(origin, normal, arrowLen)) return true;
            return false;
        }

        /// <summary>Насколько отвести ручку НАЗАД, на саму деталь, чтобы стрелка не
        /// сидела внутри соседа. 0 — путь свободен, отводить не нужно. Дальше
        /// <paramref name="maxShift"/> (полугабарита детали) не отходим — ручка
        /// должна остаться у своей грани.</summary>
        public static float PullBack(
            Vector3 origin, Vector3 normal, float arrowLen, float maxShift,
            IReadOnlyList<Box>? neighbours)
        {
            if (!Blocked(origin, normal, arrowLen, neighbours)) return 0f;
            float step = arrowLen * 0.5f;
            for (float d = arrowLen; d <= maxShift; d += step)
                if (!Blocked(origin - normal * d, normal, arrowLen, neighbours)) return d;
            // Чистого места нет (короткая деталь, зажатая с двух сторон) — всё равно
            // убираем стрелку с продолжения оси: над своей деталью она хотя бы
            // не прячется внутри соседней.
            return arrowLen;
        }
    }
}
