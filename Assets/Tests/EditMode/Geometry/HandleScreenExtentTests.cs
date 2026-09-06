using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Отсутствовавший датчик: экранная протяжённость НАРИСОВАННОЙ стрелки
    /// в пикселях. Ручка рисуется в мировых единицах, а ловится радиусом в пикселях;
    /// без этой величины расхождение двух мер не даёт ни лога, ни красного теста —
    /// пользователь просто «иногда не попадает». Свип по дистанции камеры и высоте
    /// экрана — обязательное требование UI-GUIDELINES §12.</summary>
    public class HandleScreenExtentTests
    {
        private const float Radius = HandleScale.GrabRadiusPixels;

        private static readonly float[] Distances = { 0.5f, 1f, 2f, 3f, 5f, 10f, 20f };

        private static readonly int[] ScreenHeights = { 1080, 1440, 2160 };

        private static readonly float[] Along = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        private static readonly float[] Across = { -1f, 0f, 1f };

        private static PinholeView View(int height) =>
            PinholeView.Perspective(Vector3.zero, 60f, height * 16 / 9, height);

        /// <summary>Стрелка лежит ПОПЕРЁК экрана: нормаль грани перпендикулярна
        /// взгляду, ракурсного сокращения нет, и экранная длина сравнима с мировой.</summary>
        private static HandleScreenExtent ExtentAt(float distance, int height, out float scale)
        {
            var view = View(height);
            var basePoint = new Vector3(0f, 0f, distance);
            scale = HandleScale.ForScreen(view, basePoint, HandleMetrics.Resize);
            return HandleScreenExtent.Measure(view, basePoint, Vector3.right,
                HandleMetrics.Resize, scale);
        }

        private static string Row(float distance, int height, string what) =>
            $"H={height}, d={distance} м: {what}";

        [Test]
        public void Measure_ArrowAcrossTheScreen_LengthIsTheProjectionOfItsDrawnPart()
        {
            var view = View(1080);
            var basePoint = new Vector3(0f, 0f, 4f);
            var m = HandleMetrics.Resize;
            var extent = HandleScreenExtent.Measure(view, basePoint, Vector3.right, m, 1f);

            float expected = view.PixelsForWorldSize(basePoint, m.DrawnLen);
            Assert.AreEqual(expected, extent.LengthPixels, 1f,
                "силуэт стрелки тянется от начала штока (Gap) до конца наконечника "
                + "(ArrowLen) — это и есть DrawnLen; иначе датчик мерит не ту фигуру");
            Assert.IsTrue(extent.InFront, "стрелка перед камерой");
        }

        [Test]
        public void DistanceToSilhouette_IsZeroOnTheFigure_AndGrowsOutside()
        {
            var extent = ExtentAt(4f, 1080, out _);

            Assert.AreEqual(0f, extent.DistanceToSilhouettePixels(extent.SilhouettePoint(0.5f, 0f)),
                1e-3f, "середина стрелки лежит на самой фигуре");
            Assert.AreEqual(0f, extent.DistanceToSilhouettePixels(extent.SilhouettePoint(1f, 1f)),
                1e-3f, "угол наконечника — тоже точка фигуры");

            Vector2 off = extent.SilhouettePoint(0.5f, 3f);
            Assert.AreEqual(extent.HalfWidthPixels * 2f, extent.DistanceToSilhouettePixels(off), 1e-2f,
                "вне фигуры расстояние считается от её КРАЯ, а не от оси");
        }

        [Test]
        public void EveryDrawnPointOfTheArrow_IsInsideTheGrabRadius()
        {
            var failures = new List<string>();

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                var extent = ExtentAt(distance, height, out _);
                float worst = 0f;
                foreach (float along in Along)
                foreach (float across in Across)
                    worst = Mathf.Max(worst,
                        extent.DistanceToGrabPixels(extent.SilhouettePoint(along, across)));

                if (worst > Radius)
                    failures.Add(Row(distance, height,
                        $"дальняя точка силуэта в {worst:F1} px от точки захвата при радиусе "
                        + $"{Radius:F0} px; нарисованная стрелка {extent.LengthPixels:F1} px"));
            }

            Assert.IsEmpty(failures,
                "Зона захвата обязана накрывать НАРИСОВАННУЮ фигуру (UI-GUIDELINES §12). "
                + "Клик по видимой части штока дальше радиуса от точки захвата возвращает "
                + "null, выделение не подавляется, и луч проходит сквозь прозрачную стрелку "
                + "в деталь за ней. Лечится постоянным экранным размером ручки "
                + "(HandleScale.ForScreen). Промахи:\n" + string.Join("\n", failures));
        }

        [Test]
        public void TheGrabRadius_StaysCommensurateWithTheDrawnArrow()
        {
            var failures = new List<string>();

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                var extent = ExtentAt(distance, height, out _);
                float ratio = extent.GrabRadiusToArrowLength(Radius);
                if (ratio > HandleScale.MaxGrabRadiusToArrowLength)
                    failures.Add(Row(distance, height,
                        $"радиус захвата {Radius:F0} px против нарисованной стрелки "
                        + $"{extent.LengthPixels:F1} px — отношение {ratio:F2} при пределе "
                        + $"{HandleScale.MaxGrabRadiusToArrowLength:F2}"));
            }

            Assert.IsEmpty(failures,
                "Обратная половина того же дефекта: круг захвата постоянен в пикселях, а "
                + "стрелка при отдалении съёживается, и клик по СОСЕДНЕЙ детали в полуметре "
                + "от ручки подавляет выделение и начинает ресайз. Лечится тем же постоянным "
                + "экранным размером. Промахи:\n" + string.Join("\n", failures));
        }
    }
}
