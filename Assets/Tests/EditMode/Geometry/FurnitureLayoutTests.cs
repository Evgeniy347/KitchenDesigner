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
        public void TopSurfaceMM_IsWidthByDepth_NotWidthByHeight()
        {
            var surface = FurnitureLayout.TopSurfaceMM(new Vector3Int(360, 900, 450));

            Assert.AreEqual(new Vector2Int(360, 450), surface,
                "вторая ось горизонтальной крышки — ГЛУБИНА, а не высота: "
                + "с высотой декор растягивался бы по столешнице (CONVENTIONS.md, "
                + "«декор тайлится, а не растягивается»)");
        }
    }
}
