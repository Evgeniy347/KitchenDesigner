using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Хватает ли месту перед диваном на раскладку. Положение «кровать» занимает
    /// объём, которого у сложенного дивана нет: сиденье выезжает вперёд на ход
    /// <see cref="SofaUnfold.SeatSlideTravelMM"/>, и всё, что стоит на этом пути, раскладке
    /// мешает. Ответ не зависит от того, в каком этапе диван сейчас: пользователь должен
    /// узнать про нехватку места ДО того, как нажмёт «разложить».
    ///
    /// Диван во всех сценах стоит на полу (низ на нулевой отметке), лицом к +Z: передняя
    /// грань на z = 450 мм, выдвинутое сиденье доходит до 1070 мм.</summary>
    public class SofaBedRoomTests
    {
        private const float MM = ValidationTestScene.MM;
        private const int Seat = SofaLayout.DefaultSeatHeightMM;
        private const float FrontMM = SofaLayout.DefaultDepthMM * 0.5f;

        private static readonly Quaternion Yaw45 = new Quaternion(0f, 0.38268343f, 0f, 0.9238795f);

        private static Vector3Int DefaultDims() => new Vector3Int(
            SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM, SofaLayout.DefaultDepthMM);

        private static float Travel(int seatHeight = Seat)
            => SofaUnfold.SeatSlideTravelMM(SofaLayout.DefaultDepthMM, seatHeight);

        private static ValidationElement Sofa(Vector3 centreMm, Quaternion? rotation = null,
            bool withReach = true, int seatHeight = Seat)
        {
            var dims = DefaultDims();
            var turn = rotation ?? Quaternion.identity;
            var centre = centreMm * MM;
            var size = new Vector3(dims.x, dims.y, dims.z) * MM;
            var geometry = ElementGeometry.Box("Sofa", centre, size, turn);

            var corners = new List<Vector3>();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        corners.Add(centre + turn * new Vector3(size.x * 0.5f * sx,
                            size.y * 0.5f * sy, size.z * 0.5f * sz));

            return new ValidationElement(geometry, corners.ToArray(), ElementKind.None, 0, null,
                Span.FromCenter(centre.y, size.y), ValidationElement.NoIndex,
                bedReach: withReach
                    ? SofaBedReach.Of(dims, seatHeight, centre, turn)
                    : null);
        }

        private static Vector3 SofaCentreMM() => new Vector3(0f, DefaultDims().y * 0.5f, 0f);

        private static ValidationElement TableAhead(float gapFromSofaFrontMM,
            ElementKind kind = ElementKind.None)
        {
            const float depth = 800f;
            return ValidationTestScene.Part("Table",
                new Vector3(0f, 375f, FrontMM + gapFromSofaFrontMM + depth * 0.5f),
                new Vector3(800f, 750f, depth), kind);
        }

        private static ValidationElement Floor() =>
            ValidationTestScene.Part("Floor", new Vector3(0f, -9f, 500f),
                new Vector3(6000f, 18f, 4000f), ElementKind.Anchor | ElementKind.FloorAnchor);

        private static List<SofaBedBlock> Blocks(params ValidationElement[] scene) =>
            SofaBedRoom.Blocks(scene);

        [Test]
        public void Blocks_TableThreeHundredMillimetresInFront_BlocksTheSofaAndNamesTheTable()
        {
            var blocks = Blocks(Sofa(SofaCentreMM()), TableAhead(300f));

            Assert.AreEqual(1, blocks.Count,
                "стол в 300 мм от переда дивана стоит на пути выезжающего сиденья (ход 620 мм)");
            Assert.AreEqual(0, blocks[0].Sofa, "помеха у дивана, а не наоборот");
            Assert.AreEqual(1, blocks[0].Blocker, "и мешает ему именно стол");
            Assert.AreEqual(Travel(), blocks[0].TravelMM, 1e-3f,
                "в находке лежит ход сиденья: по нему сообщение называет, сколько места нужно");
        }

        [Test]
        public void Blocks_TheSameTableFifteenHundredMillimetresAway_LeavesTheSofaAlone()
        {
            var blocks = Blocks(Sofa(SofaCentreMM()), TableAhead(1500f));

            Assert.IsEmpty(blocks,
                "в 1500 мм от дивана стол не достаёт до выдвинутого сиденья: предупреждение "
                + "про нехватку места здесь было бы ложным");
        }

        [TestCase(600f, true)]
        [TestCase(640f, false)]
        public void Blocks_EdgeOfTheReach_IsTheSeatTravel(float gapMM, bool expectedBlocked)
        {
            Assume.That(Travel(), Is.InRange(600f, 640f),
                "граница теста обязана лежать по обе стороны хода сиденья");

            var blocks = Blocks(Sofa(SofaCentreMM()), TableAhead(gapMM));

            Assert.AreEqual(expectedBlocked, blocks.Count == 1,
                "зазор " + gapMM + " мм при ходе " + Travel() + " мм: граница объёма раскладки "
                + "проходит ровно по концу хода сиденья, а не по глубине дивана или чему-то ещё");
        }

        [Test]
        public void Blocks_TableBehindTheSofa_DoesNotBlock_BecauseTheSeatSlidesForwardOnly()
        {
            var behind = ValidationTestScene.Part("Table",
                new Vector3(0f, 375f, -FrontMM - 300f - 400f), new Vector3(800f, 750f, 800f));

            Assert.IsEmpty(Blocks(Sofa(SofaCentreMM()), behind),
                "сиденье выезжает вперёд: то, что стоит за спинкой, раскладке не мешает");
        }

        [Test]
        public void Blocks_TableTouchingTheSideOfTheSofa_DoesNotBlock()
        {
            var beside = ValidationTestScene.Part("Table",
                new Vector3(1000f + 400f, 375f, FrontMM + 400f), new Vector3(800f, 750f, 800f));

            Assert.IsEmpty(Blocks(Sofa(SofaCentreMM()), beside),
                "вплотную сбоку — касание, а не пересечение: выезжающее сиденье той же ширины, что "
                + "и диван, сбоку не задевает ничего");
        }

        [TestCase(ElementKind.None, true)]
        [TestCase(ElementKind.Anchor, true)]
        [TestCase(ElementKind.Decor, false)]
        [TestCase(ElementKind.Recessed, false)]
        [TestCase(ElementKind.Opening, false)]
        [TestCase(ElementKind.Anchor | ElementKind.FloorAnchor, false)]
        [TestCase(ElementKind.Anchor | ElementKind.Foundation, false)]
        public void Blocks_ByKindOfTheThingInTheWay_OnlyRealObstaclesCount(ElementKind kind,
            bool expectedBlocked)
        {
            var blocks = Blocks(Sofa(SofaCentreMM()), TableAhead(300f, kind));

            Assert.AreEqual(expectedBlocked, blocks.Count == 1,
                $"{kind}: стена и обычная деталь мешают раскладке; светильник, врезная деталь, "
                + "проём (о нём скажет стена, в которой он сидит) и пол с фундаментом — нет");
        }

        [Test]
        public void Blocks_SofaYawedFortyFiveDegrees_UsesTheTurnedBoxNotItsBoundingBox()
        {
            var sofa = Sofa(SofaCentreMM(), Yaw45);
            var inTheCorner = ValidationTestScene.Part("Cube",
                new Vector3(1400f, 100f, -300f), new Vector3(100f, 200f, 100f));
            var onTheSeatPath = ValidationTestScene.Part("Cube",
                new Vector3(700f, 100f, 700f), new Vector3(100f, 200f, 100f));

            Assert.IsEmpty(Blocks(sofa, inTheCorner),
                "куб лежит в габарите (AABB) повёрнутого объёма раскладки, но мимо самого "
                + "объёма: перекос на 45° не должен давать ложное «нет места»");
            Assert.AreEqual(1, Blocks(sofa, onTheSeatPath).Count,
                "контроль: тот же куб на оси выезда блокирует — иначе предыдущая проверка "
                + "зелена только потому, что поворотный объём вообще ничего не ловит");
        }

        [Test]
        public void Blocks_TwoSofas_EachIsJudgedByItsOwnReach()
        {
            var first = Sofa(SofaCentreMM());
            var second = Sofa(new Vector3(0f, SofaCentreMM().y, SofaLayout.DefaultDepthMM + 300f));

            var blocks = Blocks(first, second);

            Assert.AreEqual(1, blocks.Count,
                "второй диван стоит на пути сиденья первого; сиденье второго едет от первого прочь");
            Assert.AreEqual(0, blocks[0].Sofa, "помеха принадлежит первому дивану");
            Assert.AreEqual(1, blocks[0].Blocker, "а мешает ему второй диван");
        }

        [Test]
        public void Blocks_NoSofaInTheScene_ReportsNothingAndNeverTouchesTheBroadPhase()
        {
            Assert.IsEmpty(Blocks(Floor(), TableAhead(300f)),
                "без дивана раскладывать нечего");
        }

        [Test]
        public void ReachingPairs_FindTheTableThatTheOrdinaryBroadPhaseNeverPairsWithTheSofa()
        {
            var scene = new List<ValidationElement> { Sofa(SofaCentreMM()), TableAhead(590f) };

            ValidationBroadPhase.Clear();
            var ordinary = ValidationBroadPhase.CandidatePairsInNestedLoopOrder(scene,
                ValidationCore.ContactDistUnits).ToList();
            ValidationBroadPhase.Clear();
            var reaching = ValidationBroadPhase.ReachingPairsInNestedLoopOrder(scene,
                ValidationCore.ContactDistUnits).ToList();
            ValidationBroadPhase.Clear();

            Assert.IsEmpty(ordinary,
                "условие теста: диван и стол в разных ячейках сетки, обычная широкая фаза их "
                + "не сводит — иначе проверка ниже доказывает не то");
            CollectionAssert.AreEqual(new[] { (0, 1) }, reaching,
                "габарит для широкой фазы обязан включать объём раскладки, иначе пара не дойдёт "
                + "до проверки и стол на пути сиденья останется незамеченным");
            Assert.AreEqual(1, Blocks(scene.ToArray()).Count,
                "и вся цепочка: пара найдена — помеха названа");
        }

        [Test]
        public void ValidationCore_WithAndWithoutTheBedReach_GivesTheSameAnswer_SoTheWarningNeverBecomesAnError()
        {
            List<ValidationElement> Scene(bool withReach) => new List<ValidationElement>
            {
                Floor(), Sofa(SofaCentreMM(), withReach: withReach), TableAhead(300f),
            };

            var armed = Scene(true);
            var plain = Scene(false);

            Assert.AreEqual(1, SofaBedRoom.Blocks(armed).Count,
                "контроль: в сцене с объёмом раскладки помеха есть");
            Assert.IsEmpty(SofaBedRoom.Blocks(plain),
                "контроль: без объёма раскладки помех нет — разница только в нём");
            Assert.AreEqual(ValidationTestScene.FullFingerprint(plain),
                ValidationTestScene.FullFingerprint(armed),
                "объём раскладки не участвует в COL-01/COL-02/COL-03: нехватка места — "
                + "предупреждение анализа, а не нарушение, которое красит сцену и блокирует правку");
            Assert.IsTrue(ValidationCore.Validate(armed).IsValid,
                "сцена, где диван не раскладывается, остаётся допустимой");
        }

        [Test]
        public void LocalBox_StartsAtTheSeatFrontEdge_AndEndsWhereTheSeatStopsSliding()
        {
            var dims = DefaultDims();
            var box = SofaBedReach.LocalBoxMM(dims, Seat);
            var seat = SofaLayout.Seat(dims, Seat, SofaLayout.DefaultCornerRadiusMM);
            float seatFront = seat.CentreMM.z + seat.ProfileDepthMM * 0.5f;
            float bedFront = seatFront + SofaUnfold.PoseAt(SofaUnfold.ProgressOf(SofaStage.Bed),
                dims.z, Seat).SeatSlideMM;

            Assert.AreEqual(seatFront, box.CentreMM.z - box.SizeMM.z * 0.5f, 1e-3f,
                "объём раскладки начинается у переда сложенного сиденья");
            Assert.AreEqual(bedFront, box.CentreMM.z + box.SizeMM.z * 0.5f, 1e-3f,
                "и кончается там, где сиденье останавливается в положении «кровать»");
        }

        [Test]
        public void LocalBox_RisesToTheSeatTop_AndCoversTheFlatBackrest()
        {
            var dims = DefaultDims();
            var box = SofaBedReach.LocalBoxMM(dims, Seat);
            var seat = SofaLayout.Seat(dims, Seat, SofaLayout.DefaultCornerRadiusMM);
            float seatTop = seat.CentreMM.y + seat.ThicknessMM * 0.5f;
            float flatBackrestTop = SofaUnfold.BackrestCentreMM(dims, Seat,
                SofaUnfold.BackrestFlatAngleDeg).y + SofaLayout.BackrestThicknessMM * 0.5f;

            Assert.AreEqual(seatTop, box.CentreMM.y + box.SizeMM.y * 0.5f, 1e-3f,
                "верх объёма раскладки — верх сиденья");
            Assert.AreEqual(SofaLayout.FloorYMM(dims), box.CentreMM.y - box.SizeMM.y * 0.5f, 1e-3f,
                "низ — пол: сиденье выезжает на всю свою высоту");
            Assert.LessOrEqual(flatBackrestTop, seatTop + 1e-3f,
                "лежащая спинка не выше сиденья, значит выше объёма раскладки диван в этом "
                + "положении не поднимается");
            Assert.AreEqual(SofaLayout.DefaultWidthMM, box.SizeMM.x, 1e-3f,
                "по ширине объём равен дивану");
        }

        [Test]
        public void LocalBox_LowerSeat_LongerTravel_BecauseTheBackrestHasMoreToCover()
        {
            var low = SofaBedReach.LocalBoxMM(DefaultDims(), SofaLayout.MinSeatHeightMM);
            var normal = SofaBedReach.LocalBoxMM(DefaultDims(), Seat);

            Assert.Greater(low.SizeMM.z, normal.SizeMM.z,
                "ход сиденья зависит от высоты сиденья (панель спинки длиннее при низком "
                + "сиденье), и объём раскладки обязан его повторять, а не держать константу");
        }
    }
}
