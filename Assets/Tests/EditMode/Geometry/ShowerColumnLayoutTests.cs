using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Душевая стойка с тропическим душем: вертикальная штанга на
    /// двух настенных кронштейнах, изогнутый гусак с круглой лейкой Ø 250 мм
    /// сверху, подвижный держатель с ручной лейкой Ø 110 мм на штанге, блок
    /// переключателя внизу и гибкий шланг петлёй между ними. Своего смесителя
    /// у стойки НЕТ — нижний блок это дивертор, без вентилей.
    ///
    /// Штанга и гусак — ОДНА ломаная, а не три детали: прямой участок,
    /// четверть окружности и горизонтальный вынос идут в протяжку одним
    /// массивом точек, потому что стык двух отдельных труб виден кольцевым
    /// швом ровно там, где на настоящей стойке гладкий гиб.
    ///
    /// Отсюда ограничение на вынос: он обязан быть больше, чем вылет штанги
    /// от стены плюс радиус гиба, иначе четверть окружности заканчивается
    /// ДАЛЬШЕ точки, куда её ведут, и гусак загибается назад к стене.
    ///
    /// Габарит стойки НЕ равен её высоте: петля шланга свисает ниже нижнего
    /// штуцера, и коробка обязана её накрыть — иначе шланг торчит из
    /// выделения и не попадает в проверки пересечений.</summary>
    public class ShowerColumnLayoutTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void ShowerColumnLayout_RiserPath_RunsFromTheDiverterToTheArmEnd()
        {
            var spec = ShowerColumnSpec.Default;
            var path = ShowerColumnLayout.RiserPath(spec);

            Assert.AreEqual(spec.WallOffsetMM, path[0].z, Tol,
                "штанга начинается на своей оси, вынесенной от стены кронштейнами");
            Assert.AreEqual(0f, path[0].y, Tol,
                "нижний конец штанги сидит в диверторе, у самого низа стойки");
            Assert.AreEqual(spec.ArmReachMM, path[path.Length - 1].z, Tol,
                "гусак доводит трубу до точки подвеса тропической лейки");
            Assert.AreEqual(ShowerColumnLayout.ArmAxisYMM(spec), path[path.Length - 1].y, Tol,
                "и делает это на высоте оси горизонтального выноса");
        }

        [Test]
        public void ShowerColumnLayout_RiserPath_HasNoZeroLengthLink()
        {
            var path = ShowerColumnLayout.RiserPath(ShowerColumnSpec.Default);

            for (int i = 1; i < path.Length; i++)
                Assert.Greater((path[i] - path[i - 1]).magnitude, Tolerance.ContactMm,
                    "совпавшие точки на стыке прямой и дуги: направление между ними не "
                    + "определено, и кольцо протяжки в этом узле схлопнется");
        }

        [Test]
        public void ShowerColumnLayout_BoundsMM_StartsAtTheWallPlaneAndEndsAtTheColumnTop()
        {
            var spec = ShowerColumnSpec.Default;
            var bounds = ShowerColumnLayout.BoundsMM(spec);

            Assert.AreEqual(0f, bounds.min.z, Tol,
                "фланцы кронштейнов и задняя стенка дивертора лежат в плоскости стены: это "
                + "та грань, которой стойка садится на стену");
            Assert.AreEqual(spec.ColumnHeightMM, bounds.max.y, Tol,
                "верх габарита — это верх стойки: заявленная высота обязана быть той самой "
                + "высотой, а не приблизительной");
        }

        [Test]
        public void ShowerColumnLayout_DimensionsMM_AreAsWideAsTheRainHead()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.AreEqual(spec.HeadDiameterMM, ShowerColumnLayout.DimensionsMM(spec).x,
                "самая широкая деталь стойки — тропическая лейка: всё остальное узкое и "
                + "стоит в её проекции");
        }

        [Test]
        public void ShowerColumnLayout_DimensionsMM_CoverTheHoseHangingBelowTheDiverter()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.Greater(ShowerColumnLayout.DimensionsMM(spec).y, spec.ColumnHeightMM,
                "петля шланга свисает ниже дивертора, и габарит обязан её накрыть: иначе "
                + "шланг торчит из рамки выделения и не участвует в проверках пересечений");
        }

        [Test]
        public void ShowerColumnLayout_HosePath_HangsFromTheDiverterToTheHandShower()
        {
            var spec = ShowerColumnSpec.Default;
            var hose = ShowerColumnLayout.HosePath(spec);

            Assert.Less((hose[0] - ShowerColumnLayout.HoseOutletMM(spec)).magnitude, Tol,
                "шланг выходит из дивертора");
            Assert.Less((hose[hose.Length - 1] - ShowerColumnLayout.HoseInletMM(spec)).magnitude,
                Tol, "и приходит в нижний торец рукоятки ручной лейки");
            Assert.Less(PipePath.LowestPoint(hose).y, 0f,
                "и провисает петлёй НИЖЕ дивертора: натянутый по прямой шланг сразу выдаёт "
                + "нарисованную по двум точкам модель");
        }

        [Test]
        public void ShowerColumnLayout_Parts_PutBothBracketsOnTheWallAtDifferentHeights()
        {
            var spec = ShowerColumnSpec.Default;
            var parts = ShowerColumnLayout.Parts(spec);

            Assert.AreEqual(0f, parts[0].FromMM.z, Tol,
                "верхний кронштейн упирается фланцем в стену");
            Assert.AreEqual(0f, parts[2].FromMM.z, Tol,
                "нижний тоже: стойка держится на двух точках, а не висит на одной");
            Assert.Greater(parts[0].FromMM.y, parts[2].FromMM.y,
                "верхний кронштейн выше нижнего");
            Assert.AreEqual(spec.WallOffsetMM, parts[1].ToMM.z, Tol,
                "плечо кронштейна доходит ровно до оси штанги: короче — щель, длиннее — "
                + "труба протыкает кронштейн насквозь");
        }

        [Test]
        public void ShowerColumnLayout_Parts_HangTheRainHeadUnderTheArmEnd()
        {
            var spec = ShowerColumnSpec.Default;
            var head = ShowerColumnLayout.Parts(spec)[8];

            Assert.AreEqual(spec.ArmReachMM, head.FromMM.z, Tol,
                "лейка соосна концу выноса");
            Assert.AreEqual(spec.HeadDiameterMM * 0.5f, head.FromRadiusMM, Tol,
                "тропическая лейка Ø 250 мм с референса");
            Assert.AreEqual(spec.HeadThicknessMM, head.ToMM.y - head.FromMM.y, Tol,
                "и она плоская: 30 мм толщины, а не полусфера");
            Assert.Less(head.ToMM.y, ShowerColumnLayout.ArmAxisYMM(spec),
                "лейка подвешена ПОД выносом, а не надета на него сверху");
        }

        [Test]
        public void ShowerColumnLayout_Parts_PutTheHandShowerInFrontOfTheRiser()
        {
            var spec = ShowerColumnSpec.Default;
            var parts = ShowerColumnLayout.Parts(spec);
            var handle = parts[6];
            var handHead = parts[7];

            Assert.Greater(handle.FromMM.z, spec.WallOffsetMM,
                "ручная лейка висит ВПЕРЕДИ штанги, а не внутри неё");
            Assert.Greater(handle.ToRadiusMM, handle.FromRadiusMM,
                "рукоятка расширяется кверху: снизу на неё садится шланг, сверху — лейка");
            Assert.AreEqual(spec.HandShowerDiameterMM * 0.5f, handHead.FromRadiusMM, Tol,
                "головка ручной лейки Ø 110 мм с референса");
            Assert.AreEqual(handle.ToMM.y, handHead.FromMM.y, Tol,
                "головка сидит ровно на верхнем торце рукоятки");
        }

        [Test]
        public void ShowerColumnLayout_DiverterBox_SitsOnTheWallAtTheBottom()
        {
            var spec = ShowerColumnSpec.Default;
            var box = ShowerColumnLayout.DiverterBox(spec);

            Assert.AreEqual(0f, box.CentreMM.y - box.SizeMM.y * 0.5f, Tol,
                "низ дивертора — это низ стойки");
            Assert.AreEqual(0f, box.CentreMM.z - box.SizeMM.z * 0.5f, Tol,
                "задняя стенка дивертора лежит на стене");
            Assert.Greater(box.CentreMM.z + box.SizeMM.z * 0.5f,
                spec.WallOffsetMM + ShowerColumnLayout.RiserRadiusMM(spec),
                "и блок накрывает штангу целиком: труба, торчащая из передней стенки "
                + "переключателя, выглядит как ошибка сборки");
        }

        [Test]
        public void ShowerColumnSpec_Clamped_PushesTheArmOutPastTheBend()
        {
            var spec = ShowerColumnSpec.Clamped(1150, 32, 250, 30, 80, 60, 110, 1000);

            Assert.Greater(spec.ArmReachMM,
                spec.WallOffsetMM + ShowerColumnLayout.BendRadiusMM(spec.RiserDiameterMM),
                "вынос короче гиба невозможен: четверть окружности закончилась бы дальше "
                + "точки подвеса, и гусак загнулся бы назад к стене");
        }

        [Test]
        public void ShowerColumnSpec_Default_MatchesTheReferencePhoto()
        {
            var spec = ShowerColumnSpec.Default;

            Assert.AreEqual(250, spec.HeadDiameterMM, "тропическая лейка Ø 250 мм");
            Assert.AreEqual(30, spec.HeadThicknessMM, "лейка плоская, около 30 мм");
            Assert.AreEqual(110, spec.HandShowerDiameterMM, "ручная лейка Ø 110 мм");
            Assert.AreEqual(1150, spec.ColumnHeightMM,
                "высота стойки от дивертора до верха гусака");
        }
    }
}
