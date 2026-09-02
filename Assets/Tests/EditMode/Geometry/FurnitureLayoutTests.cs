using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Три выражения, которые до этого были написаны РУКОЙ в трёх
    /// элементах мебели по два раза каждое: высота ножки, центр крышки и центр
    /// ножки в системе координат самого элемента (начало — центр габарита,
    /// ось Y вверх, единицы Unity).
    ///
    /// Шесть копий одной арифметики — это шесть мест, где знак или половина
    /// разъедутся молча: столешница уйдёт под пол, ножки повиснут в воздухе, и
    /// компилятор не скажет ни слова. Числа здесь намеренно НЕсимметричные
    /// (900 не делится на 25 пополам одинаково), иначе перепутанные местами
    /// слагаемые дали бы тот же ответ.</summary>
    public class FurnitureLayoutTests
    {
        private const int Height = 900;
        private const int Thickness = 25;

        [Test]
        public void LegHeightMM_IsTheOverallHeightMinusTheTop()
        {
            Assert.AreEqual(875, FurnitureLayout.LegHeightMM(Height, Thickness),
                "ножка кончается там, где начинается крышка");
        }

        [Test]
        public void LegHeightMM_NeverDropsBelowOne_EvenWhenTheTopIsThickerThanTheFurniture()
        {
            Assert.AreEqual(1, FurnitureLayout.LegHeightMM(20, 25),
                "нулевая или отрицательная высота ножки даёт вырожденный масштаб "
                + "у примитива и NaN в трансформе; пользователь вправе задать любую "
                + "высоту, и обрезать её обязан код, а не он");
        }

        [Test]
        public void TopCentreY_PutsTheTopFlushWithTheUpperEdgeOfTheFurniture()
        {
            Assert.AreEqual(0.4375f, FurnitureLayout.TopCentreY(Height, Thickness), 1e-6f,
                "верх крышки совпадает с верхом габарита: 900/2 - 25/2 = 437,5 мм");
            Assert.AreEqual(Height * 0.5f * AppConstants.MM_TO_UNITS,
                FurnitureLayout.TopCentreY(Height, Thickness)
                + Thickness * 0.5f * AppConstants.MM_TO_UNITS, 1e-6f,
                "то же самое другими словами: центр плюс половина толщины = половина "
                + "габарита. Если крышка вылезет выше, деталь перестанет помещаться "
                + "в собственный габарит и разъедется с привязкой");
        }

        [Test]
        public void LegCentreY_StandsTheLegOnTheFloorOfTheFurniture()
        {
            Assert.AreEqual(-0.0125f, FurnitureLayout.LegCentreY(Height, Thickness), 1e-6f,
                "низ ножки совпадает с низом габарита: 875/2 - 900/2 = -12,5 мм");
            Assert.AreEqual(-Height * 0.5f * AppConstants.MM_TO_UNITS,
                FurnitureLayout.LegCentreY(Height, Thickness)
                - FurnitureLayout.LegHeightMM(Height, Thickness) * 0.5f
                    * AppConstants.MM_TO_UNITS, 1e-6f,
                "центр минус половина высоты ножки = низ габарита: ножка обязана "
                + "стоять на полу, а не висеть над ним");
        }

        [Test]
        public void LegCentreY_AndTopCentreY_LeaveNoGapBetweenTheLegAndTheTop()
        {
            float legTop = FurnitureLayout.LegCentreY(Height, Thickness)
                + FurnitureLayout.LegHeightMM(Height, Thickness) * 0.5f
                    * AppConstants.MM_TO_UNITS;
            float topBottom = FurnitureLayout.TopCentreY(Height, Thickness)
                - Thickness * 0.5f * AppConstants.MM_TO_UNITS;

            Assert.AreEqual(topBottom, legTop, 1e-6f,
                "два выражения описывают ОДНУ мебель и обязаны сходиться в стыке: "
                + "щель между ножкой и крышкой или их взаимное проникновение видны "
                + "только на рендере, а тестами не ловились ни разу");
        }

        [Test]
        public void PhysicalScale_ConvertsEveryAxisSeparately_KeepingItsOwnOrder()
        {
            var scale = FurnitureLayout.PhysicalScale(new Vector3Int(360, 900, 450));

            Assert.AreEqual(0.36f, scale.x, 1e-6f, "ширина остаётся шириной");
            Assert.AreEqual(0.9f, scale.y, 1e-6f, "высота остаётся высотой");
            Assert.AreEqual(0.45f, scale.z, 1e-6f,
                "глубина остаётся глубиной: три РАЗНЫХ числа взяты нарочно — на "
                + "кубе перепутанные местами оси дали бы тот же ответ");
        }

        [Test]
        public void MaxCornerRadiusMM_IsHalfOfTheSHORTERHorizontalSide_NotOfTheWidth()
        {
            Assert.AreEqual(175, FurnitureLayout.MaxCornerRadiusMM(
                new Vector3Int(600, 400, 350)),
                "скругление съедает обе горизонтальные стороны сразу, поэтому его "
                + "потолок задаёт МЕНЬШАЯ из них: 350/2 = 175. Габарит взят "
                + "несимметричным нарочно — на квадрате перепутанные местами ширина "
                + "и глубина дали бы тот же ответ");
        }

        [Test]
        public void MaxCornerRadiusMM_IgnoresHeight_EvenWhenHeightIsTheSmallestOfTheThree()
        {
            Assert.AreEqual(200, FurnitureLayout.MaxCornerRadiusMM(
                new Vector3Int(400, 30, 900)),
                "скругляется контур в плане, а не бок: высота 30 мм не имеет к "
                + "потолку радиуса никакого отношения");
        }

        [Test]
        public void MaxCornerRadiusMM_NeverGoesNegative_OnADegenerateFootprint()
        {
            Assert.AreEqual(0, FurnitureLayout.MaxCornerRadiusMM(
                new Vector3Int(-10, 450, 360)),
                "отрицательный потолок сделал бы Clamp невозможным (min > max) и "
                + "вернул бы саму отрицательную границу — то есть радиус наружу");
        }

        [Test]
        public void ClampCornerRadiusMM_HoldsBothEnds_ZeroAndTheHalfSide()
        {
            var dims = new Vector3Int(600, 400, 350);

            Assert.AreEqual(0, FurnitureLayout.ClampCornerRadiusMM(dims, -50),
                "ноль — законное значение: это прямоугольная мебель без скругления");
            Assert.AreEqual(175, FurnitureLayout.ClampCornerRadiusMM(dims, 10000),
                "выше половины меньшей стороны контур вывернулся бы наружу");
            Assert.AreEqual(120, FurnitureLayout.ClampCornerRadiusMM(dims, 120),
                "значение внутри диапазона обязано пройти насквозь неизменным");
        }

        [Test]
        public void TopSurfaceMM_IsWidthByDepth_NotWidthByHeight()
        {
            var surface = FurnitureLayout.TopSurfaceMM(new Vector3Int(360, 900, 450));

            Assert.AreEqual(new Vector2Int(360, 450), surface,
                "вторая ось горизонтальной крышки — ГЛУБИНА, а не высота: "
                + "с высотой декор растягивался бы по столешнице (CONVENTIONS.md, "
                + "«декор тайлится, а не растягивается»)");
        }

        [Test]
        public void EulerAnglesFor_TurnTheProfilePlane_TowardsTheAxisEachPartIsSeenFrom()
        {
            Assert.AreEqual(Vector3.zero,
                FurnitureLayout.EulerAnglesFor(FurniturePartOrientation.Horizontal),
                "горизонтальная часть строится как есть: ProfileExtrusionMesh выдавливает "
                + "профиль (x, z) вверх");
            Assert.AreEqual(new Vector3(-90f, 0f, 0f),
                FurnitureLayout.EulerAnglesFor(FurniturePartOrientation.Frontal),
                "поворот -90 вокруг X ставит вторую ось профиля вверх, а толщину — вдоль Z: "
                + "скругления оказываются во фронтальной плоскости");
            Assert.AreEqual(new Vector3(-90f, 90f, 0f),
                FurnitureLayout.EulerAnglesFor(FurniturePartOrientation.Side),
                "плюс поворот на 90 вокруг Y кладёт первую ось профиля вдоль Z, а толщину — "
                + "вдоль X: получается валик вдоль глубины дивана");
        }

        [Test]
        public void SizeMM_ReportsTheWorldAxes_NotTheProfileAxes()
        {
            var frontal = new FurniturePartBox("f", Vector3.zero, 700f, 400f, 200f, 90f,
                FurniturePartOrientation.Frontal);
            var side = new FurniturePartBox("s", Vector3.zero, 700f, 400f, 200f, 90f,
                FurniturePartOrientation.Side);
            var horizontal = new FurniturePartBox("h", Vector3.zero, 700f, 400f, 200f, 90f,
                FurniturePartOrientation.Horizontal);

            Assert.AreEqual(new Vector3(700f, 400f, 200f), frontal.SizeMM,
                "у стоячей подушки ширина профиля идёт по X, вторая ось профиля — по Y, "
                + "толщина — по Z");
            Assert.AreEqual(new Vector3(200f, 400f, 700f), side.SizeMM,
                "у бокового валика первая ось профиля лежит вдоль Z, а толщина — вдоль X; "
                + "перепутать их значит проверять габарит по чужой оси и не заметить вылет");
            Assert.AreEqual(new Vector3(700f, 200f, 400f), horizontal.SizeMM,
                "у горизонтальной части толщина идёт вверх, а вторая ось профиля — в глубину: "
                + "это исходная система ProfileExtrusionMesh");
        }
    }
}
