using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Арифметика дверного проёма. Дверь — не окно: у неё нет
    /// подоконной части стены, проём начинается от нулевой отметки пола.
    ///
    /// Соглашение, которое проверяют эти тесты (и от которого зависят
    /// <c>Wall.RebuildMesh</c>, <c>DoorElement.AlignToWall</c> и
    /// <c>add_opening</c>):
    ///
    /// 1. <c>DimensionsMM.y</c> двери — это высота ПРОЁМА, дырки в стене, а не
    ///    высота полотна. Именно её пишет пользователь в панели и агент в
    ///    <c>add_opening height</c>, именно её меряет правило
    ///    <c>OutOfWallBounds</c> в <c>ValidationCore</c>.
    /// 2. Низ проёма прибит к низу стены, поэтому центр двери всегда стоит на
    ///    половине её высоты над базой стены — вертикальной степени свободы у
    ///    двери нет.
    /// 3. Полотно приподнято над полом на <see cref="DoorOpeningLayout.LeafFloorGapMM"/>,
    ///    и зазор съедает ВЫСОТУ ПОЛОТНА, а не растит проём: иначе дверь
    ///    высотой 2100 давала бы дырку 2110 и число в панели врало бы.</summary>
    public class DoorOpeningLayoutTests
    {
        private const int OpeningH = 2100;

        [Test]
        public void DoorCentre_SitsAtHalfHeight_SoOpeningBottomLandsOnWallBase()
        {
            float centre = DoorOpeningLayout.CentreAboveWallBaseMM(OpeningH);

            Assert.AreEqual(1050f, centre, 1e-4f,
                "центр двери обязан стоять на половине высоты над базой стены");
            Assert.AreEqual(0f, centre - OpeningH * 0.5f, 1e-4f,
                "низ проёма ушёл от нулевой отметки — под дверью останется порог");
        }

        [Test]
        public void Leaf_IsShorterThanOpening_ByTheFloorGap()
        {
            Assert.AreEqual(OpeningH - DoorOpeningLayout.LeafFloorGapMM,
                DoorOpeningLayout.LeafHeightMM(OpeningH),
                "зазор под дверью обязан съедать высоту полотна, а не растить проём");
        }

        [Test]
        public void LeafBottom_StandsExactlyTheFloorGapAboveTheFloor()
        {
            float leafBottom = DoorOpeningLayout.LeafCentreOffsetMM(OpeningH)
                - DoorOpeningLayout.LeafHeightMM(OpeningH) * 0.5f;
            float openingBottom = -OpeningH * 0.5f;

            Assert.AreEqual(DoorOpeningLayout.LeafFloorGapMM, leafBottom - openingBottom, 1e-4f,
                "полотно обязано висеть ровно на 10 мм над полом");
        }

        [Test]
        public void LeafTop_StaysFlushWithTheOpeningTop()
        {
            float leafTop = DoorOpeningLayout.LeafCentreOffsetMM(OpeningH)
                + DoorOpeningLayout.LeafHeightMM(OpeningH) * 0.5f;

            Assert.AreEqual(OpeningH * 0.5f, leafTop, 1e-4f,
                "верх полотна обязан совпадать с верхом проёма — зазор только снизу");
        }

        [Test]
        public void OpeningShorterThanTheGap_GivesNoNegativeLeaf()
        {
            Assert.AreEqual(0, DoorOpeningLayout.LeafHeightMM(DoorOpeningLayout.LeafFloorGapMM),
                "полотно отрицательной высоты вывернуло бы меш наизнанку");
            Assert.AreEqual(0f, DoorOpeningLayout.LeafCentreOffsetMM(4), 1e-4f,
                "нечего поднимать — смещение обязано обнулиться вместе с высотой");
        }

        [Test]
        public void GroundedSpan_DoorLiftedAboveTheFloor_OpeningStillReachesTheWallBase()
        {
            var span = DoorOpeningLayout.GroundedSpanNorm(0.1f, 0.2f);

            Assert.AreEqual(DoorOpeningLayout.WallBaseNorm, span.Min, 1e-6f,
                "проём поднятой двери обязан тянуться до пола — иначе внизу простенок");
            Assert.AreEqual(0.3f, span.Max, 1e-6f,
                "верх проёма задаётся дверью и подниматься вместе с низом не должен");
        }

        [Test]
        public void GroundedSpan_DoorAlreadyOnTheFloor_KeepsItsOwnSpan()
        {
            var span = DoorOpeningLayout.GroundedSpanNorm(-0.3f, 0.2f);

            Assert.AreEqual(DoorOpeningLayout.WallBaseNorm, span.Min, 1e-6f,
                "низ проёма стоящей на полу двери и так на базе стены");
            Assert.AreEqual(-0.1f, span.Max, 1e-6f,
                "дверь на полу — привязка низа не имеет права двигать верх");
        }

        [Test]
        public void GroundedSpan_OpeningEntirelyBelowTheWall_CollapsesInsteadOfInverting()
        {
            var span = DoorOpeningLayout.GroundedSpanNorm(-0.9f, 0.2f);

            Assert.AreEqual(0f, span.Size, 1e-6f,
                "проём ниже базы стены обязан схлопнуться, а не стать отрицательным");
        }
    }
}
