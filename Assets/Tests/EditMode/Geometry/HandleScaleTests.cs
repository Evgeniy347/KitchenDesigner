using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Постоянный ЭКРАННЫЙ размер ручки — это и есть вариант B: 26 пикселей
    /// захвата становятся корректной мерой на любом зуме и любом разрешении, потому
    /// что нарисованная стрелка тоже всегда одной пиксельной длины.</summary>
    public class HandleScaleTests
    {
        private static readonly float[] Distances = { 0.5f, 1f, 2f, 3f, 5f, 10f, 20f };

        private static readonly int[] ScreenHeights = { 1080, 1440, 2160 };

        private static PinholeView View(int height) =>
            PinholeView.Perspective(Vector3.zero, 60f, height * 16 / 9, height);

        [Test]
        public void ForScreen_KeepsTheDrawnArrowAtTheSamePixelLength_AtEveryDistanceAndResolution()
        {
            var m = HandleMetrics.Resize;

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                var view = View(height);
                var basePoint = new Vector3(0f, 0f, distance);
                float scale = HandleScale.ForScreen(view, basePoint, m);
                var extent = HandleScreenExtent.Measure(view, basePoint, Vector3.right, m, scale);

                Assert.AreEqual(HandleScale.DrawnArrowPixels, extent.LengthPixels, 0.5f,
                    $"H={height}, d={distance} м: стрелка поперёк экрана обязана быть "
                    + "одной и той же пиксельной длины — иначе мера захвата и мера "
                    + "отрисовки снова расходятся");
            }
        }

        [Test]
        public void ForScreen_GrowsTheHandleWithDistance()
        {
            var m = HandleMetrics.Resize;
            var view = View(1080);

            float near = HandleScale.ForScreen(view, new Vector3(0f, 0f, 1f), m);
            float far = HandleScale.ForScreen(view, new Vector3(0f, 0f, 10f), m);

            Assert.Greater(far, near * 5f,
                "положительный контроль: если ForScreen вернуть константу, оба масштаба "
                + "совпадут и предыдущий тест перестанет что-либо значить");
        }

        [Test]
        public void ForScreen_DegenerateCamera_FallsBackToTheWorldSizedHandle()
        {
            var m = HandleMetrics.Resize;
            var view = new PinholeView(Vector3.zero, Vector3.forward, Vector3.right,
                Vector3.up, 0f, 0, 0);

            Assert.AreEqual(HandleScale.WorldSized,
                HandleScale.ForScreen(view, new Vector3(0f, 0f, 2f), m), 1e-4f,
                "камера без пикселей и без угла обзора даёт бесконечный масштаб; "
                + "ручка нулевого или бесконечного размера не видна и не ловится");
        }

        [Test]
        public void TheDrawnArrowLength_FitsBetweenBothHalvesOfTheDefect()
        {
            var m = HandleMetrics.Resize;
            float halfLength = HandleScale.DrawnArrowPixels * 0.5f;
            float halfWidth = HandleScale.DrawnArrowPixels * m.MaxRadius / m.DrawnLen;
            float worstCorner = Mathf.Sqrt(halfLength * halfLength + halfWidth * halfWidth);

            Assert.LessOrEqual(worstCorner, HandleScale.GrabRadiusPixels,
                $"верхняя граница: дальний угол силуэта ({worstCorner:F1} px) обязан "
                + $"влезать в круг захвата ({HandleScale.GrabRadiusPixels:F0} px), иначе "
                + "клик по видимой части стрелки снова проваливается сквозь неё");

            Assert.GreaterOrEqual(
                HandleScale.DrawnArrowPixels * HandleScale.MaxGrabRadiusToArrowLength,
                HandleScale.GrabRadiusPixels,
                "нижняя граница: круг захвата не вправе быть заметно больше самой "
                + "стрелки, иначе клик по соседней детали снова начинает ресайз. "
                + "Окно между границами узкое — менять DrawnArrowPixels можно только "
                + "вместе с этим тестом");
        }
    }
}
