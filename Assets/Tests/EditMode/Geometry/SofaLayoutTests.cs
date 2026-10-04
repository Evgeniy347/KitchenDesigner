using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Раскладка дивана-книжки по фотографиям пользователя. Три части:
    /// СИДЕНЬЕ спереди, СПИНКА сзади и внутренний КОРОБ под сиденьем; подлокотников
    /// нет, вместо них четыре подушки — две стоят на сиденье у спинки, две лежат
    /// по бокам.
    ///
    /// Класс живёт на быстром пути (Core/Geometry) намеренно: раскладка — это
    /// арифметика в миллиметрах, сцена ей не нужна, и потому она проверяется за
    /// доли секунды вместо холодного запуска Unity. Ориентацию каждой части класс
    /// отдаёт данными (Vector3 углов Эйлера), а не Quaternion: Quaternion.Euler
    /// — это ECall, и под dotnet он падает SecurityException.
    ///
    /// Числа 100, 700, 180 и 720 заданы пользователем и не обсуждаются: низ
    /// спинки на 100 мм над полом, высота спинки 700, толщина 180, глубина
    /// сиденья 720 (вместе 900 — прежняя глубина дивана).</summary>
    public class SofaLayoutTests
    {
        private const int Seat = SofaLayout.DefaultSeatHeightMM;
        private const int Radius = SofaLayout.DefaultCornerRadiusMM;

        private static Vector3Int Default() => new Vector3Int(
            SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM, SofaLayout.DefaultDepthMM);

        private static FurniturePartBox Named(FurniturePartBox[] boxes, string name)
        {
            foreach (var box in boxes)
                if (box.Name == name) return box;
            Assert.Fail("в раскладке нет части " + name);
            return default;
        }

        private static float Min(FurniturePartBox part, int axis)
            => part.CentreMM[axis] - part.SizeMM[axis] * 0.5f;

        private static float Max(FurniturePartBox part, int axis)
            => part.CentreMM[axis] + part.SizeMM[axis] * 0.5f;

        private static void AssertPartInside(FurniturePartBox part, Vector3Int dims, string what)
        {
            var half = new Vector3(dims.x * 0.5f, dims.y * 0.5f, dims.z * 0.5f);
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.LessOrEqual(Max(part, axis), half[axis] + 1e-3f,
                    "часть вылезает за габаритную коробку: рамка изометрического снимка и "
                    + "AABB для снапа строятся по DimensionsMM, и всё, что торчит наружу, "
                    + "будет обрезано или не поймано — " + what + ", ось " + axis);
                Assert.GreaterOrEqual(Min(part, axis), -half[axis] - 1e-3f,
                    "часть вылезает за габаритную коробку снизу: " + what + ", ось " + axis);
            }
        }

        [Test]
        public void Defaults_AreTheNumbersTheUserFixed()
        {
            Assert.AreEqual(100, SofaLayout.BackrestBottomMM,
                "низ спинки на 100 мм над полом — зазор, на который она «не доходит» до пола");
            Assert.AreEqual(700, SofaLayout.BackrestHeightMM, "высота спинки 700 мм");
            Assert.AreEqual(180, SofaLayout.BackrestThicknessMM, "толщина спинки 180 мм");
            Assert.AreEqual(720, SofaLayout.DefaultSeatDepthMM, "глубина сиденья 720 мм");
            Assert.AreEqual(2000, SofaLayout.DefaultWidthMM, "длина по умолчанию 2000 мм");
            Assert.AreEqual(900, SofaLayout.DefaultDepthMM,
                "180 + 720 = 900: общая глубина осталась прежней");
            Assert.AreEqual(800, SofaLayout.DefaultHeightMM,
                "100 + 700 = 800: общая высота — это низ спинки плюс её высота, а не "
                + "отдельное число, которое можно растянуть");
        }

        [Test]
        public void Normalise_ForcesTheFixedHeight_AndKeepsWidthAndDepth()
        {
            var normal = SofaLayout.Normalise(new Vector3Int(1800, 1234, 950));

            Assert.AreEqual(new Vector3Int(1800, 800, 950), normal,
                "высота дивана не растягивается: любое запрошенное число сводится к 800, "
                + "а длина и глубина остаются как заказаны");
        }

        [Test]
        public void Normalise_RaisesATooShallowDepth_ToTheBackrestPlusTheMinimumSeat()
        {
            var normal = SofaLayout.Normalise(new Vector3Int(2000, 800, 100));

            Assert.AreEqual(SofaLayout.BackrestThicknessMM + SofaLayout.MinSeatDepthMM, normal.z,
                "глубина меньше спинки с минимальным сиденьем не имеет смысла: сиденье "
                + "ушло бы в ноль, а спинка вышла бы за габарит");
        }

        [Test]
        public void SeatDepth_IsTheDepthMinusTheBackrest_SoOnlyTheSeatChangesWithDepth()
        {
            Assert.AreEqual(720, SofaLayout.SeatDepthFor(900), "900 − 180 = 720");
            Assert.AreEqual(1020, SofaLayout.SeatDepthFor(1200),
                "глубину растянули на 300 — и все 300 достались сиденью");

            var shallow = SofaLayout.Backrest(new Vector3Int(2000, 800, 900));
            var deep = SofaLayout.Backrest(new Vector3Int(2000, 800, 1200));
            Assert.AreEqual(shallow.SizeMM.z, deep.SizeMM.z, 1e-3f,
                "спинка остаётся 180 мм при любой глубине дивана");
            Assert.AreEqual(180f, deep.SizeMM.z, 1e-3f, "и это ровно 180");
        }

        [Test]
        public void Backrest_Folded_GoesDownToTheFloorMinusTheClearance_AndUpToTheOverallHeight()
        {
            var dims = Default();
            var backrest = SofaLayout.Backrest(dims);

            Assert.AreEqual(-dims.y * 0.5f + 100f, Min(backrest, 1), 1e-3f,
                "главное изменение: сложенная спинка идёт вниз до пола минус зазор 100 мм, "
                + "а не обрывается у сиденья");
            Assert.AreEqual(dims.y * 0.5f, Max(backrest, 1), 1e-3f,
                "а верх спинки — это общая высота дивана");
            Assert.AreEqual(700f, backrest.SizeMM.y, 1e-3f, "высота спинки 700 мм");
        }

        [Test]
        public void Backrest_StandsAtTheBackWall_AcrossTheWholeWidth()
        {
            var dims = Default();
            var backrest = SofaLayout.Backrest(dims);

            Assert.AreEqual(-dims.z * 0.5f, Min(backrest, 2), 1e-3f,
                "спинка прижата к задней стенке габарита (-Z)");
            Assert.AreEqual(-dims.z * 0.5f + 180f, Max(backrest, 2), 1e-3f,
                "и её толщина 180 мм");
            Assert.AreEqual(dims.x, backrest.SizeMM.x, 1e-3f, "на всю ширину дивана");
        }

        [Test]
        public void Seat_StartsWhereTheBackrestEnds_StandsOnTheFloor_AndReachesTheFrontEdge()
        {
            var dims = Default();
            var seat = SofaLayout.Seat(dims, Seat, Radius);

            Assert.AreEqual(-dims.z * 0.5f + 180f, Min(seat, 2), 1e-3f,
                "сиденье начинается там, где кончается спинка: зазора между частями нет");
            Assert.AreEqual(dims.z * 0.5f, Max(seat, 2), 1e-3f,
                "и доходит до переднего края габарита");
            Assert.AreEqual(-dims.y * 0.5f, Min(seat, 1), 1e-3f, "сиденье стоит на полу");
            Assert.AreEqual(-dims.y * 0.5f + Seat, Max(seat, 1), 1e-3f,
                "его верх на высоте сиденья");
        }

        [Test]
        public void SeatAndBackrest_ShareOneCornerRadius_SoTheBedReadsAsTwoIdenticalMats()
        {
            var seat = SofaLayout.Seat(Default(), Seat, Radius);
            var backrest = SofaLayout.Backrest(Default(), Radius);

            Assert.AreEqual(Radius, seat.RadiusMM,
                "все четыре угла сиденья скруглены заказанным радиусом");
            Assert.AreEqual(Radius, seat.RearRadiusMM,
                "и задние тоже: в кровати сиденье и спинка читаются парой одинаковых матов "
                + "(фото 40), у обоих скругление одно");
            Assert.AreEqual(Radius, backrest.RadiusMM,
                "у спинки тот же радиус: она лежит большой гранью вверх, и углы этой грани "
                + "должны совпасть с углами сиденья");
            Assert.AreEqual(Radius, backrest.RearRadiusMM, "и у задних углов спинки он же");
            Assert.AreEqual(FurniturePartOrientation.Frontal, backrest.Orientation,
                "профиль скруглённого прямоугольника лежит в большой грани спинки (ширина на "
                + "высоту), а не в плане: иначе вертикальные рёбра скруглены, а углы листа нет");
            Assert.AreEqual(FurniturePartShape.Extruded, seat.Shape,
                "обе части — один вид выдавливания: одинаковые кромки");
            Assert.AreEqual(seat.Shape, backrest.Shape, "и у спинки тот же вид");
        }

        [Test]
        public void EveryPart_StaysInsideTheBoundingBox_WhenFolded()
        {
            var dims = Default();
            AssertPartInside(SofaLayout.Backrest(dims), dims, "спинка");
            AssertPartInside(SofaLayout.Seat(dims, Seat, Radius), dims, "сиденье");
            foreach (var cushion in SofaLayout.Cushions(dims, Seat))
                AssertPartInside(cushion, dims, "подушка " + cushion.Name);
        }

        [Test]
        public void Cushions_AreFour_TwoOnTheBackAndTwoWhereArmrestsWouldBe()
        {
            var cushions = SofaLayout.Cushions(Default(), Seat);

            Assert.AreEqual(SofaLayout.CushionCount, cushions.Length,
                "четыре подушки — это и есть форма дивана с фотографии; константа "
                + "CushionCount уезжает наружу через MCP (SofaInfo.cushionCount), и разойтись "
                + "с реальной раскладкой ей нельзя");
            Assert.AreEqual(FurniturePartOrientation.Frontal,
                Named(cushions, SofaLayout.BackCushionLeftName).Orientation,
                "спинная подушка стоит: её профиль развёрнут во фронтальной плоскости, и "
                + "скругления видны там, где на них смотрят");
        }

        [Test]
        public void ArmCushions_AreSlabsWithARoundedNose_LikeThePhotographOfTheSideCushion()
        {
            var cushions = SofaLayout.Cushions(Default(), Seat);

            foreach (var name in new[]
                     { SofaLayout.ArmCushionLeftName, SofaLayout.ArmCushionRightName })
            {
                var arm = Named(cushions, name);
                Assert.AreEqual(FurniturePartShape.SoftSlab, arm.Shape,
                    "подушка " + name + ": боковая подушка на фото — плоский валик с мягкой кромкой, а не "
                    + "раздутая со всех сторон подушка");
                Assert.AreEqual(FurniturePartOrientation.Horizontal, arm.Orientation,
                    "подушка " + name + ": валик лежит плашмя, его толщина — это высота");
                Assert.AreEqual(SofaLayout.ArmNoseRadiusMM, arm.RadiusMM,
                    "подушка " + name + ": передние углы скруглены крупно — нос валика, как на снимке");
                Assert.AreEqual(SofaLayout.ArmTailRadiusMM, arm.RearRadiusMM,
                    "подушка " + name + ": задние углы скруглены мало: они прячутся за спинной подушкой");
                Assert.Greater(arm.RadiusMM, arm.RearRadiusMM,
                    "подушка " + name + ": нос круглее хвоста — в этом и отличие от прежней подушки, у "
                    + "которой все углы были одинаковые");
            }
        }

        [Test]
        public void ArmCushions_RunFromTheBackrestToTheVeryFrontEdge()
        {
            var dims = Default();
            var arm = Named(SofaLayout.Cushions(dims, Seat), SofaLayout.ArmCushionRightName);

            Assert.AreEqual(dims.z * 0.5f, Max(arm, 2), 1e-3f,
                "на фотографии боковые подушки доходят до самого переднего края сиденья; "
                + "утопленный валик читался бы подлокотником, а его у этого дивана нет");
            Assert.AreEqual(-dims.z * 0.5f + SofaLayout.BackrestThicknessMM
                    + SofaLayout.CushionGapMM,
                Min(arm, 2), 1e-3f, "а сзади упирается в спинку через зазор");
        }

        [Test]
        public void BackCushions_StandOnTheSeat_AndTopOutAtTheTopOfTheBackrest()
        {
            var dims = Default();
            var cushion = Named(SofaLayout.Cushions(dims, Seat), SofaLayout.BackCushionRightName);

            Assert.AreEqual(-dims.y * 0.5f + Seat, Min(cushion, 1), 1e-3f,
                "подушка стоит НА сиденье, а не висит");
            Assert.AreEqual(dims.y * 0.5f, Max(cushion, 1), 1e-3f,
                "и её верх вровень с верхом спинки");
            Assert.AreEqual(-dims.z * 0.5f + SofaLayout.BackrestThicknessMM,
                Min(cushion, 2), 1e-3f, "а сзади она опирается на переднюю грань спинки");
        }

        [Test]
        public void TheWidth_IsSharedByTwoArmsAndTwoBackCushions_WithThreeGaps()
        {
            var dims = Default();
            float arm = SofaLayout.ArmCushionWidthFor(dims.x);
            float back = SofaLayout.BackCushionWidthFor(dims.x);

            Assert.AreEqual(dims.x, 2f * arm + 2f * back + 3f * SofaLayout.CushionGapMM, 1e-3f,
                "ширина расходится без остатка: две боковые, две спинные и три зазора между "
                + "ними. Иначе подушки съедут с центра и симметрия дивана сломается");
        }

        /// <summary>Пропорции сняты с фотографии, а не выбраны на глаз, и потому
        /// закреплены здесь: без этого теста числа 360, 240 и 320 неотличимы от
        /// произвольных, и следующий, кому «покажется мелко», подвинет их обратно.
        ///
        /// Мерено по правому краю снимка — он почти в профиль, и перспектива
        /// там врёт меньше всего. Два отношения, которые перспектива искажает
        /// слабее прочего, потому что оба берутся внутри одной вертикали на
        /// одной глубине. Допуск 10 процентов — это точность промера по
        /// фотографии, а не требование к мебели.</summary>
        [Test]
        public void TheProportions_MatchTheReferencePhotograph_WithinMeasurementError()
        {
            const float photoBaseToArm = 1.6f;
            const float photoBackCushionAspect = 1.38f;
            const float tolerance = 0.1f;

            var dims = Default();
            float armHeight = Mathf.Min(SofaLayout.ArmCushionHeightMM,
                SofaLayout.BackCushionHeightFor(Seat));

            Assert.AreEqual(photoBaseToArm, Seat / armHeight, photoBaseToArm * tolerance,
                "цоколь к боковой подушке: на снимке 160 к 100 пикселей у правого края. "
                + "Вдвое более тонкая подушка перестаёт работать вместо подлокотника — "
                + "а подлокотников у этого дивана нет, и заменяют их именно они");

            float cushionWidth = SofaLayout.BackCushionWidthFor(dims.x);
            float cushionHeight = SofaLayout.BackCushionHeightFor(Seat);

            Assert.AreEqual(photoBackCushionAspect, cushionWidth / cushionHeight,
                photoBackCushionAspect * tolerance,
                "спинная подушка почти квадратная: на снимке 400 к 290 пикселей. Растянутая "
                + "вдвое подушка читается полкой во всю длину, и стык между двумя пропадает");
        }

        [Test]
        public void ANarrowSofa_ShrinksTheArms_InsteadOfOverlappingTheCushions()
        {
            const int narrow = 600;

            float arm = SofaLayout.ArmCushionWidthFor(narrow);
            float back = SofaLayout.BackCushionWidthFor(narrow);

            Assert.Less(arm, SofaLayout.ArmCushionWidthMM,
                "на узком диване боковая подушка обязана ужаться: сохранить её 320 мм значит "
                + "наложить её на спинную");
            Assert.AreEqual(narrow, 2f * arm + 2f * back + 3f * SofaLayout.CushionGapMM, 1e-3f,
                "и ширина по-прежнему расходится без остатка");
        }

        [Test]
        public void EveryBackCushionRadius_FitsItsOwnProfile_SoTheContourNeverSelfIntersects()
        {
            var dims = SofaLayout.Normalise(new Vector3Int(700, 800, 400));

            foreach (var cushion in SofaLayout.Cushions(dims, 300))
            {
                if (cushion.Shape != FurniturePartShape.Cushion) continue;
                Assert.LessOrEqual(cushion.RadiusMM,
                    Mathf.Min(cushion.ProfileWidthMM, cushion.ProfileDepthMM) * 0.5f + 1e-3f,
                    "радиус больше половины меньшей стороны выворачивает контур наружу — "
                    + "RoundedRectProfile.Fit ужимает его по РЁБРАМ, а не по диагонали, и "
                    + "два противоположных угла всё равно пересекутся: " + cushion.Name);
            }
        }

        [Test]
        public void SeatHeight_IsClamped_ToTheRangeInWhichTheBackrestCanFoldDownOntoTheBox()
        {
            Assert.AreEqual(SofaLayout.MinSeatHeightMM, SofaLayout.ClampSeatHeightMM(10),
                "ниже минимума под сиденьем не помещается короб");
            Assert.AreEqual(SofaLayout.MaxSeatHeightMM, SofaLayout.ClampSeatHeightMM(5000),
                "выше максимума складывающаяся спинка ушла бы за заднюю стенку");
            Assert.AreEqual(SofaLayout.BackrestThicknessMM,
                SofaLayout.MaxSeatHeightMM - SofaLayout.BackrestBottomMM
                - SofaLayout.BackrestThicknessMM,
                "предел не выбран на глаз: часть спинки НИЖЕ петли при складывании уходит "
                + "назад, и она не должна быть длиннее толщины спинки, иначе лёжа она "
                + "окажется за стеной");
        }

        [Test]
        public void CornerRadius_IsClamped_BySeatDepth_NotByTheWholeDepth()
        {
            var dims = Default();

            Assert.AreEqual(360, SofaLayout.MaxCornerRadiusMM(dims),
                "радиус держится на половине меньшей стороны СИДЕНЬЯ: 720 / 2, а не 900 / 2 — "
                + "скругляется только оно, спинка прямая");
            Assert.AreEqual(360, SofaLayout.ClampCornerRadiusMM(dims, 5000),
                "всё, что больше, сводится к максимуму");
            Assert.AreEqual(0, SofaLayout.ClampCornerRadiusMM(dims, -5),
                "а отрицательное — к нулю");
        }

        [Test]
        public void SeatSurface_IsTheWidthBySeatDepth_NotTheWholeDepth()
        {
            Assert.AreEqual(new Vector2Int(2000, 720), SofaLayout.SeatSurfaceMM(Default()),
                "декор носит сиденье, а не весь габарит: поверхность мощения — его верх");
        }
    }
}
