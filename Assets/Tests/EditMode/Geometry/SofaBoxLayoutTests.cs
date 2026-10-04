using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Внутренний короб дивана: открытый сверху, с тремя отделениями
    /// (на фото 43 в нём лежит постельное бельё). Он живёт ВНУТРИ сиденья и виден
    /// только когда сиденье выдвинуто, поэтому главное требование к нему — не
    /// торчать наружу из сложенного дивана.</summary>
    public class SofaBoxLayoutTests
    {
        private const int Seat = SofaLayout.DefaultSeatHeightMM;
        private const int Radius = SofaLayout.DefaultCornerRadiusMM;
        private const float Eps = 1e-3f;

        private static Vector3Int Default() => new Vector3Int(
            SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM, SofaLayout.DefaultDepthMM);

        private static void Bounds(Cuboid[] panels, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var panel in panels)
            {
                min = Vector3.Min(min, panel.CentreMM - panel.SizeMM * 0.5f);
                max = Vector3.Max(max, panel.CentreMM + panel.SizeMM * 0.5f);
            }
        }

        [Test]
        public void Panels_AreTheFloor_FourWalls_AndOneDividerLessThanTheCompartments()
        {
            var panels = SofaBoxLayout.Panels(Default(), Seat, Radius);

            Assert.AreEqual(SofaBoxLayout.PanelCount, panels.Length,
                "дно, четыре стенки и перегородки: на одну меньше, чем отделений");
            Assert.AreEqual(3, SofaBoxLayout.CompartmentCount,
                "три отделения, как на фото 43");
            Assert.AreEqual(5 + SofaBoxLayout.CompartmentCount - 1, panels.Length,
                "число панелей выводится из числа отделений, а не записано отдельно");
        }

        [Test]
        public void Box_IsDepthOfTheSeatMinusTheFrontLip_AndStartsRightAfterTheBackrest()
        {
            var dims = Default();
            Bounds(SofaBoxLayout.Panels(dims, Seat, Radius), out var min, out var max);

            Assert.AreEqual(SofaLayout.BackrestFrontZMM(dims) + SofaBoxLayout.RearSetbackMM, min.z, Eps,
                "задняя стенка короба сразу за сложенной спинкой: на её верхней кромке "
                + "стоит петля");
            Assert.AreEqual(dims.z * 0.5f - SofaBoxLayout.FrontLipMM, max.z, Eps,
                "перед коробом остаётся губа сиденья в 100 мм: на фото 43 спереди короба "
                + "есть мягкий борт, и без него скруглённый угол сиденья обнажил бы короб");
            Assert.AreEqual(618f, max.z - min.z, Eps, "глубина короба по умолчанию 618 мм");
        }

        [Test]
        public void Box_NeverPokesAboveTheSeat_SoTheFoldedSofaHidesIt()
        {
            var dims = Default();
            Bounds(SofaBoxLayout.Panels(dims, Seat, Radius), out _, out var max);

            float seatTop = -dims.y * 0.5f + Seat;
            Assert.AreEqual(seatTop - SofaLayout.BackrestThicknessMM, max.y, Eps,
                "верх короба ниже верха сиденья ровно на толщину лежащей спинки: она "
                + "ложится на короб и оказывается вровень с сиденьем");
            Assert.Less(max.y, seatTop,
                "короб целиком внутри сиденья: сложенный диван выглядит сплошным");
        }

        [Test]
        public void Box_HasAnInteriorDeepEnoughForBedding_AtTheLowestSeat()
        {
            var dims = Default();
            Bounds(SofaBoxLayout.Panels(dims, SofaLayout.MinSeatHeightMM, Radius),
                out var min, out var max);

            Assert.GreaterOrEqual(max.y - min.y - SofaBoxLayout.WallMM, 80f,
                "на самом низком сиденье остаётся хотя бы 80 мм внутренней высоты: короб, "
                + "в который ничего не положить, — не короб");
        }

        [Test]
        public void Box_CornersStayInsideTheRoundedFrontOfTheSeat_AtEveryCornerRadius()
        {
            var dims = Default();
            int seatDepth = SofaLayout.SeatDepthFor(dims.z);
            float seatCentreZ = SofaLayout.BackrestFrontZMM(dims) + seatDepth * 0.5f;
            int checkedRadii = 0;

            for (int radius = 0; radius <= SofaLayout.MaxCornerRadiusMM(dims); radius += 20)
            {
                Bounds(SofaBoxLayout.Panels(dims, Seat, radius), out var min, out var max);
                var radii = new CornerRadii(0f, 0f, radius, radius);
                checkedRadii++;

                foreach (var x in new[] { min.x, max.x })
                {
                    var corner = new Vector2(x, max.z - seatCentreZ);
                    Assert.LessOrEqual(
                        RoundedRectProfile.SignedDistance(corner, dims.x, seatDepth, radii),
                        0f,
                        "передний угол короба вылез бы из скруглённого угла сиденья и был бы "
                        + "виден снаружи сложенного дивана. Радиус " + radius + ", x=" + x);
                }
            }

            Assert.GreaterOrEqual(checkedRadii, 15,
                "перебор радиусов не дошёл и до пятнадцати значений");
        }

        [Test]
        public void SideInset_GrowsWithTheCornerRadius_ButNeverBelowItsMinimum()
        {
            Assert.AreEqual(SofaBoxLayout.MinSideInsetMM, SofaBoxLayout.SideInsetMM(0f), Eps,
                "у прямого угла отступ минимальный");
            Assert.AreEqual(SofaBoxLayout.MinSideInsetMM, SofaBoxLayout.SideInsetMM(120f), Eps,
                "радиус по умолчанию уже влезает в минимальный отступ");
            Assert.Greater(SofaBoxLayout.SideInsetMM(360f), SofaBoxLayout.SideInsetMM(120f),
                "круглое сиденье требует большего отступа, иначе угол короба вылезет");
            Assert.AreEqual(SofaBoxLayout.MinSideInsetMM, SofaBoxLayout.SideInsetMM(-50f), Eps,
                "отрицательный радиус не даёт отрицательного отступа");
        }

        [Test]
        public void Dividers_SplitTheInteriorIntoEqualCompartments()
        {
            var panels = SofaBoxLayout.Panels(Default(), Seat, Radius);
            var left = panels[1];
            var right = panels[2];
            var first = panels[5];
            var second = panels[6];

            float innerLeft = left.CentreMM.x + left.SizeMM.x * 0.5f;
            float innerRight = right.CentreMM.x - right.SizeMM.x * 0.5f;
            float firstWidth = (first.CentreMM.x - first.SizeMM.x * 0.5f) - innerLeft;
            float middleWidth = (second.CentreMM.x - second.SizeMM.x * 0.5f)
                - (first.CentreMM.x + first.SizeMM.x * 0.5f);
            float lastWidth = innerRight - (second.CentreMM.x + second.SizeMM.x * 0.5f);

            Assert.AreEqual(firstWidth, middleWidth, 1f,
                "первое и среднее отделения одной ширины");
            Assert.AreEqual(middleWidth, lastWidth, 1f,
                "среднее и последнее отделения одной ширины");
        }

        [Test]
        public void Box_IsSymmetricAcrossTheWidth()
        {
            Bounds(SofaBoxLayout.Panels(Default(), Seat, Radius), out var min, out var max);

            Assert.AreEqual(0f, min.x + max.x, Eps,
                "короб стоит по центру: левая и правая границы зеркальны");
        }
    }
}
