using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.Handles;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Тот же дефект, что у ручек трансформации, на второй поверхности:
    /// ручки области накладки рисуются в МИРОВЫХ единицах, а ловятся кругом
    /// постоянного радиуса в пикселях (HandleScreenPick). Фигур здесь две —
    /// стрелка в режиме «перенос» и кубик в режиме «растяжение», — и мерить надо
    /// обе: у кубика точка захвата в центре, у стрелки сдвинута вдоль оси, так что
    /// окна допустимой длины у них разные. Свип по дистанции камеры и высоте
    /// экрана обязателен по UI-GUIDELINES §12.</summary>
    public class TextureOverlayHandleScreenExtentTests
    {
        private const float Radius = HandleScale.GrabRadiusPixels;

        private static readonly HandleMetrics Metrics = HandleMetrics.Overlay;

        private static readonly float[] Distances = { 0.5f, 1f, 2f, 3f, 5f, 10f, 20f };

        private static readonly int[] ScreenHeights = { 1080, 1440, 2160 };

        private static readonly float[] Along = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        private static readonly float[] Across = { -1f, 0f, 1f };

        private static PinholeView View(int height) =>
            PinholeView.Perspective(Vector3.zero, 60f, height * 16 / 9, height);

        /// <summary>Стрелка накладки лежит В ПЛОСКОСТИ грани и поперёк взгляда:
        /// ракурсного сокращения нет, экранная длина сравнима с мировой. Масштаб и
        /// точку захвата берём из тех же функций, что и раскладка ручек, — иначе
        /// датчик мерит фигуру, которой на экране нет.</summary>
        private static HandleScreenExtent ArrowAt(float distance, int height)
        {
            var view = View(height);
            var basePoint = new Vector3(0f, 0f, distance);
            float scale = OverlayHandleScale.ArrowScale(view, basePoint, Metrics);
            return HandleScreenExtent.Measure(view, basePoint, Vector3.right, Metrics, scale,
                OverlayHandleScale.ArrowGrabAlongAxis(Metrics, scale));
        }

        private static float CubeEdgePixelsAt(float distance, int height)
        {
            var view = View(height);
            var basePoint = new Vector3(0f, 0f, distance);
            float scale = OverlayHandleScale.CubeScale(view, basePoint);
            return view.PixelsForWorldSize(basePoint, OverlayHandleScale.CubeEdgeUnits * scale);
        }

        private static string Row(float distance, int height, string what) =>
            $"H={height}, d={distance} м: {what}";

        [Test]
        public void Measure_TheOverlayArrowAcrossTheScreen_LengthIsTheProjectionOfItsDrawnPart()
        {
            var view = View(1080);
            var basePoint = new Vector3(0f, 0f, 4f);
            var extent = HandleScreenExtent.Measure(view, basePoint, Vector3.right, Metrics, 1f);

            float expected = view.PixelsForWorldSize(basePoint, Metrics.DrawnLen);
            Assert.AreEqual(expected, extent.LengthPixels, 1f,
                "проверка самого стенда: силуэт стрелки тянется от начала штока до конца "
                + "наконечника — это DrawnLen; иначе датчик мерит не ту фигуру");
            Assert.IsTrue(extent.InFront,
                "стрелка перед камерой — иначе свип ничего не измерит");
        }

        [Test]
        public void TheOverlayArrow_StartsAtTheAreaEdge_WithNoGapBeforeItsShaft()
        {
            Assert.AreEqual(0f, Metrics.Gap, 1e-5f,
                "у накладки Gap = 0: шток начинается прямо на границе области, поэтому "
                + "нарисованная длина равна полной (DrawnLen == ArrowLen), а середина "
                + "силуэта приходится ровно на половину стрелки. На этом стоит вывод "
                + "окна ниже; появится отступ — окно надо выводить заново");
            Assert.AreEqual(Metrics.ArrowLen, Metrics.DrawnLen, 1e-5f,
                "то же равенство с другой стороны — обе величины читает вывод окна");
        }

        [Test]
        public void EveryDrawnPointOfTheOverlayArrow_IsInsideTheGrabRadius()
        {
            var failures = new List<string>();

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                var extent = ArrowAt(distance, height);
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
                + "Клик по видимой части стрелки области дальше радиуса от точки захвата "
                + "возвращает null, и луч проходит сквозь ручку в деталь за ней — правка "
                + "области срывается в выделение соседа. Лечится постоянным экранным "
                + "размером ручки (OverlayHandleScale.ArrowScale). Промахи:\n"
                + string.Join("\n", failures));
        }

        [Test]
        public void TheGrabRadius_StaysCommensurateWithTheOverlayArrow()
        {
            var failures = new List<string>();

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                var extent = ArrowAt(distance, height);
                float ratio = extent.GrabRadiusToArrowLength(Radius);
                if (ratio > HandleScale.MaxGrabRadiusToArrowLength)
                    failures.Add(Row(distance, height,
                        $"радиус захвата {Radius:F0} px против нарисованной стрелки "
                        + $"{extent.LengthPixels:F1} px — отношение {ratio:F2} при пределе "
                        + $"{HandleScale.MaxGrabRadiusToArrowLength:F2}"));
            }

            Assert.IsEmpty(failures,
                "Обратная половина того же дефекта: круг захвата постоянен в пикселях, а "
                + "стрелка при отдалении съёживается, и клик по стене в полуметре от "
                + "области перехватывается ручкой накладки. Лечится тем же постоянным "
                + "экранным размером. Промахи:\n" + string.Join("\n", failures));
        }

        [Test]
        public void EveryCornerOfTheOverlayCube_IsInsideTheGrabRadius()
        {
            var failures = new List<string>();

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                float edge = CubeEdgePixelsAt(distance, height);
                float worst = edge * OverlayHandleScale.CubeWorstCornerToEdge;
                if (worst > Radius)
                    failures.Add(Row(distance, height,
                        $"дальний угол кубика в {worst:F1} px от его центра при радиусе "
                        + $"{Radius:F0} px; нарисованное ребро {edge:F1} px"));
            }

            Assert.IsEmpty(failures,
                "Кубик режима «растяжение» болеет тем же, что и стрелка: он мировой "
                + "(OverlayHandleScale.CubeEdgeUnits), а ловится кругом в пикселях. "
                + "Точка захвата у него в центре, поэтому граница одна — половина "
                + "пространственной диагонали обязана влезать в круг при ЛЮБОМ ракурсе. "
                + "Лечится постоянным экранным размером (OverlayHandleScale.CubeScale). "
                + "Промахи:\n" + string.Join("\n", failures));
        }

        [Test]
        public void TheGrabRadius_StaysCommensurateWithTheOverlayCube()
        {
            var failures = new List<string>();

            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
            {
                float edge = CubeEdgePixelsAt(distance, height);
                float figure = edge * OverlayHandleScale.CubeDrawnDiagonalToEdge;
                float ratio = figure > 0f ? Radius / figure : float.PositiveInfinity;
                if (ratio > HandleScale.MaxGrabRadiusToArrowLength)
                    failures.Add(Row(distance, height,
                        $"радиус захвата {Radius:F0} px против видимой диагонали кубика "
                        + $"{figure:F1} px (ребро {edge:F1} px) — отношение {ratio:F2} при "
                        + $"пределе {HandleScale.MaxGrabRadiusToArrowLength:F2}"));
            }

            Assert.IsEmpty(failures,
                "Обратная половина у кубика: он съёживается с расстоянием, круг захвата "
                + "нет, и клик по стене рядом с областью начинает тянуть её границу. "
                + "Сравниваем с диагональю ГРАНИ, а не с пространственной: анфас "
                + "пользователь видит квадрат, и это самая тесная мера фигуры на экране. "
                + "Промахи:\n" + string.Join("\n", failures));
        }

        [Test]
        public void TheOverlayArrowLength_FitsBetweenBothHalvesOfTheDefect()
        {
            float halfLength = HandleScale.DrawnArrowPixels * 0.5f;
            float halfWidth = HandleScale.DrawnArrowPixels * Metrics.MaxRadius / Metrics.DrawnLen;
            float worstCorner = Mathf.Sqrt(halfLength * halfLength + halfWidth * halfWidth);

            Assert.LessOrEqual(worstCorner, Radius,
                $"верхняя граница: дальний угол силуэта ({worstCorner:F1} px) обязан влезать "
                + $"в круг захвата ({Radius:F0} px). У накладки конус шире относительно "
                + "более короткой стрелки, чем у ручек трансформации (0,20 против 0,177 "
                + "полуширины на длину), поэтому потолок здесь 48 px, а не 49");

            Assert.GreaterOrEqual(
                HandleScale.DrawnArrowPixels * HandleScale.MaxGrabRadiusToArrowLength, Radius,
                "нижняя граница та же, что и у ручек трансформации, — она зависит только "
                + "от радиуса: стрелка не короче 35 px. Окно 35…48 px, выбранные 42 в него "
                + "попадают; менять DrawnArrowPixels можно только вместе с этим тестом");
        }

        [Test]
        public void TheOverlayCubeEdge_FitsBetweenBothHalvesOfTheDefect()
        {
            float worstCorner =
                OverlayHandleScale.DrawnCubePixels * OverlayHandleScale.CubeWorstCornerToEdge;
            float figure =
                OverlayHandleScale.DrawnCubePixels * OverlayHandleScale.CubeDrawnDiagonalToEdge;

            Assert.LessOrEqual(worstCorner, Radius,
                $"верхняя граница кубика: половина его пространственной диагонали "
                + $"({worstCorner:F1} px) обязана влезать в круг захвата ({Radius:F0} px) — "
                + "это худший ракурс, при любом другом угол ближе. Отсюда ребро не длиннее "
                + "30 px");

            Assert.GreaterOrEqual(figure * HandleScale.MaxGrabRadiusToArrowLength, Radius,
                "нижняя граница кубика: круг захвата не вправе быть заметно больше видимой "
                + "фигуры, иначе клик по стене рядом начинает тянуть границу области. "
                + "Отсюда ребро не короче 25 px. Окно 25…30 px, выбранные "
                + "DrawnCubePixels = 28 в него попадают");
        }

        [Test]
        public void ArrowScale_KeepsTheDrawnOverlayArrow_AtOnePixelLength_EverywhereInTheSweep()
        {
            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
                Assert.AreEqual(HandleScale.DrawnArrowPixels,
                    ArrowAt(distance, height).LengthPixels, 0.5f,
                    $"H={height}, d={distance} м: стрелка накладки поперёк экрана обязана "
                    + "быть одной и той же пиксельной длины — иначе мера захвата и мера "
                    + "отрисовки снова расходятся");
        }

        [Test]
        public void CubeScale_KeepsTheDrawnCube_AtOnePixelEdge_EverywhereInTheSweep()
        {
            foreach (int height in ScreenHeights)
            foreach (float distance in Distances)
                Assert.AreEqual(OverlayHandleScale.DrawnCubePixels,
                    CubeEdgePixelsAt(distance, height), 0.5f,
                    $"H={height}, d={distance} м: ребро кубика обязано быть одной и той же "
                    + "пиксельной длины по той же причине, что и стрелка");
        }

        [Test]
        public void BothOverlayScales_GrowWithDistance()
        {
            var view = View(1080);
            var near = new Vector3(0f, 0f, 1f);
            var far = new Vector3(0f, 0f, 10f);

            Assert.Greater(OverlayHandleScale.ArrowScale(view, far, Metrics),
                OverlayHandleScale.ArrowScale(view, near, Metrics) * 5f,
                "положительный контроль: верни ArrowScale константу — и оба предыдущих "
                + "теста станут зелёными на мировой ручке, ради которой всё и затевалось");
            Assert.Greater(OverlayHandleScale.CubeScale(view, far),
                OverlayHandleScale.CubeScale(view, near) * 5f,
                "тот же контроль для кубика: у него своя функция масштаба, и сломать её "
                + "можно отдельно");
        }

        [Test]
        public void CubeScale_DegenerateCamera_FallsBackToTheWorldSizedHandle()
        {
            var view = new PinholeView(Vector3.zero, Vector3.forward, Vector3.right,
                Vector3.up, 0f, 0, 0);

            Assert.AreEqual(HandleScale.WorldSized,
                OverlayHandleScale.CubeScale(view, new Vector3(0f, 0f, 2f)), 1e-4f,
                "камера без пикселей и без угла обзора даёт нулевой мировой размер; "
                + "кубик нулевого размера не видно и не схватить");
        }

        [Test]
        public void TheArrowGrabPoint_SitsAtTheMiddleOfTheDrawnSilhouette()
        {
            var extent = ArrowAt(3f, 1080);
            float toRoot = extent.DistanceToGrabPixels(extent.SilhouettePoint(0f, 0f));
            float toTip = extent.DistanceToGrabPixels(extent.SilhouettePoint(1f, 0f));

            Assert.AreEqual(toRoot, toTip, 0.5f,
                "точка захвата — СЕРЕДИНА силуэта, а не 0,6 его длины, как было раньше: "
                + "круг радиуса 26 px, снятый с середины, накрывает фигуру до 48 px, "
                + "а снятый с 0,6 — только до 41 px, и выбранные 42 в него уже не влезают");
        }

        [Test]
        public void TheCubeShapeFactors_AreItsOwnDiagonals_NotRoundNumbers()
        {
            Assert.AreEqual(Mathf.Sqrt(3f) * 0.5f, OverlayHandleScale.CubeWorstCornerToEdge,
                1e-5f, "положительный контроль вывода окна: верхняя граница держится на "
                + "половине ПРОСТРАНСТВЕННОЙ диагонали куба, √3/2; подменить её на √2/2 "
                + "значит молча разрешить ребро на 15% длиннее");
            Assert.AreEqual(Mathf.Sqrt(2f), OverlayHandleScale.CubeDrawnDiagonalToEdge,
                1e-5f, "а нижняя — на диагонали ГРАНИ, √2: это то, что видно анфас");
        }
    }
}
