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

                var faces = FaceCache.GetFaces(o);
                for (int j = 0; j < faces.Length; j++)
                {
                    var g = faces[j];
                    // Для РЕСАЙЗА плоскость соседа двусторонняя: растягиваемая грань
                    // встаёт либо встык к ближней грани (нормали противоположны),
                    // либо ЗАПОДЛИЦО с дальней (нормали со-направлены). У панели
                    // толщиной 18 мм это два детента подряд, и второй был недоступен:
                    // стойка, доведённая под низ панели, дальше тянулась вверх без
                    // единого прилипания, хотя верх панели — очевидная кромка.
                    // Тот же принцип уже применён к стенкам пазов ниже.
                    if (Mathf.Abs(Vector3.Dot(g.normal, normal)) < Tolerance.ParallelDot) continue;

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

                // Дно паза как поверхность посадки — только для вкладной панели:
                // растягивая ДВП, её грань доводят до дна паза (номиналом), а не до
                // пласти детали. Дно встречное грани панели, проверяется как обычная
                // грань. Толстой детали дно паза не предлагаем.
                if (self is PanelElement)
                {
                    foreach (var seat in o.GetGrooveSeatFaces())
                    {
                        if (Vector3.Dot(seat.normal, normal) > -Tolerance.ParallelDot) continue;
                        float d = Vector3.Dot(seat.center - faceCenter, normal);
                        if (Mathf.Abs(d) > threshold + ThresholdEpsilon) continue;

                        Rect sRect = RectFor(seat.center, uAxis, vAxis, seat.rightAxis, seat.upAxis,
                            seat.size.x, seat.size.y);
                        if (!Overlap(mRect, sRect)) continue;

                        if (Mathf.Abs(d) < bestAbs)
                        {
                            bestAbs = Mathf.Abs(d);
                            gap = d;
                            found = true;
                        }
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
            Vector2 faceSize, Face wall)
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

        /// <summary>Соприкасаются ли грани в плоскости. Касание РОВНО ПО РЕБРУ
        /// (нулевая площадь пересечения) — тоже контакт: у вертикальной стойки,
        /// приставленной торцом к кромке горизонтальной панели, footprint'ы делят
        /// ровно ребро. Перемещение такой контакт принимает давно
        /// (SnapSystem.FacesOverlap, hasLineContact), а ресайз требовал строго
        /// положительной площади — и растягиваемая деталь проезжала мимо кромки
        /// соседа, не прилипая ни на одном миллиметре. Порог и знак сравнения
        /// те же, что в FacesOverlap, чтобы обе системы видели контакт одинаково.</summary>
        private static bool Overlap(Rect a, Rect b)
        {
            float left = Mathf.Max(a.xMin, b.xMin);
            float right = Mathf.Min(a.xMax, b.xMax);
            float bottom = Mathf.Max(a.yMin, b.yMin);
            float top = Mathf.Min(a.yMax, b.yMax);
            return left <= right + Tolerance.SnapEpsilon
                && bottom <= top + Tolerance.SnapEpsilon;
        }
    }
}
