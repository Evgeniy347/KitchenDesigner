using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Раскладка дивана по фотографии: у него НЕТ подлокотников, вместо
    /// них подушки. Всего подушек четыре — две стоят на спинке, две лежат по
    /// бокам сиденья там, где у обычного дивана были бы подлокотники.
    ///
    /// Класс живёт на быстром пути (Core/Geometry) намеренно: раскладка — это
    /// арифметика в миллиметрах, сцена ей не нужна, и потому она проверяется за
    /// 0,3 с вместо холодного запуска Unity. Ориентацию каждой части класс
    /// отдаёт данными (Vector3 углов Эйлера), а не Quaternion: Quaternion.Euler
    /// — это ECall, и под dotnet он падает SecurityException.</summary>
    public class SofaLayoutTests
    {
        private const int Seat = SofaLayout.DefaultSeatHeightMM;

        private static Vector3Int Default() => new Vector3Int(
            SofaLayout.DefaultWidthMM, SofaLayout.DefaultHeightMM, SofaLayout.DefaultDepthMM);

        private static FurniturePartBox Named(FurniturePartBox[] boxes, string name)
        {
            foreach (var box in boxes)
                if (box.Name == name) return box;
            Assert.Fail("в раскладке нет части " + name);
            return default;
        }

        private static void AssertInsideBox(FurniturePartBox part, Vector3Int dims, string what)
        {
            var half = new Vector3(dims.x * 0.5f, dims.y * 0.5f, dims.z * 0.5f);
            var size = part.SizeMM;
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.LessOrEqual(part.CentreMM[axis] + size[axis] * 0.5f, half[axis] + 1e-3f,
                    "часть вылезает за габаритную коробку: рамка изометрического снимка и "
                    + "AABB для снапа строятся по DimensionsMM, и всё, что торчит наружу, "
                    + "будет обрезано или не поймано — " + what + ", ось " + axis);
                Assert.GreaterOrEqual(part.CentreMM[axis] - size[axis] * 0.5f, -half[axis] - 1e-3f,
                    "часть вылезает за габаритную коробку снизу: " + what + ", ось " + axis);
            }
        }

        [Test]
        public void Cushions_AreFour_TwoOnTheBackAndTwoWhereArmrestsWouldBe()
        {
            var cushions = SofaLayout.Cushions(Default(), Seat);

            Assert.AreEqual(SofaLayout.CushionCount, cushions.Length,
                "четыре подушки — это и есть форма дивана с фотографии; константа "
                + "CushionCount уезжает наружу через MCP (SofaInfo.cushionCount), и разойтись "
                + "с реальной раскладкой ей нельзя");
            Assert.AreEqual(FurniturePartOrientation.Side,
                Named(cushions, SofaLayout.ArmCushionLeftName).Orientation,
                "боковая подушка лежит вдоль глубины: её профиль развёрнут в плоскости "
                + "(длина, высота), поэтому валик выходит скруглённым и спереди, и сверху. "
                + "Вертикальная выдавка дала бы доску с острой верхней кромкой");
            Assert.AreEqual(FurniturePartOrientation.Frontal,
                Named(cushions, SofaLayout.BackCushionLeftName).Orientation,
                "спинная подушка стоит: её профиль развёрнут во фронтальной плоскости, и "
                + "скругления видны там, где на них смотрят");
        }

        [Test]
        public void EveryPart_StaysInsideTheBoundingBox()
        {
            var dims = Default();
            AssertInsideBox(SofaLayout.BackRail(dims, Seat), dims, "спинка");
            foreach (var cushion in SofaLayout.Cushions(dims, Seat))
                AssertInsideBox(cushion, dims, "подушка " + cushion.Name);
        }

        [Test]
        public void ArmCushions_RunFromTheBackRailToTheVeryFrontEdge()
        {
            var dims = Default();
            var arm = Named(SofaLayout.Cushions(dims, Seat), SofaLayout.ArmCushionRightName);

            float front = arm.CentreMM.z + arm.SizeMM.z * 0.5f;
            float back = arm.CentreMM.z - arm.SizeMM.z * 0.5f;

            Assert.AreEqual(dims.z * 0.5f, front, 1e-3f,
                "на фотографии боковые подушки доходят до самого переднего края сиденья; "
                + "утопленный валик читался бы подлокотником, а его у этого дивана нет");
            Assert.AreEqual(
                -dims.z * 0.5f + SofaLayout.BackDepthFor(dims.z) + SofaLayout.CushionGapMM,
                back, 1e-3f, "а сзади упирается в спинку через зазор");
        }

        [Test]
        public void BackCushions_StandOnTheSeat_AndTopOutAtTheOverallHeight()
        {
            var dims = Default();
            var cushion = Named(SofaLayout.Cushions(dims, Seat), SofaLayout.BackCushionRightName);

            Assert.AreEqual(-dims.y * 0.5f + Seat,
                cushion.CentreMM.y - cushion.SizeMM.y * 0.5f, 1e-3f,
                "подушка стоит НА сиденье, а не висит");
            Assert.AreEqual(dims.y * 0.5f, cushion.CentreMM.y + cushion.SizeMM.y * 0.5f, 1e-3f,
                "и её верх — это общая высота дивана: спинка-полка ниже подушек, как на "
                + "фотографии, поэтому габарит задают именно они");
        }

        [Test]
        public void BackRail_IsLowerThanTheCushions_ByTheDropAmount()
        {
            var dims = Default();
            var rail = SofaLayout.BackRail(dims, Seat);

            Assert.AreEqual(dims.y * 0.5f - SofaLayout.BackRailDropMM,
                rail.CentreMM.y + rail.SizeMM.y * 0.5f, 1e-3f,
                "на фотографии верх спинки чуть ниже верха подушек — именно поэтому подушки "
                + "видно целиком, а не срезанными полкой");
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
        /// одной глубине.
        ///
        /// Первая версия промахнулась по обоим сразу и в одну сторону: подушки
        /// вышли мельче натуры, боковые читались бугорками у передних углов, а
        /// спинные сливались в низкую гряду вместо двух отдельных подушек.
        /// Допуск 10 процентов — это точность промера по фотографии, а не
        /// требование к мебели.</summary>
        [Test]
        public void TheProportions_MatchTheReferencePhotograph_WithinMeasurementError()
        {
            const float photoBaseToArm = 1.6f;
            const float photoBackCushionAspect = 1.38f;
            const float tolerance = 0.1f;

            var dims = Default();
            float armHeight = Mathf.Min(SofaLayout.ArmCushionHeightMM,
                SofaLayout.BackrestHeightMM(dims.y, Seat));

            Assert.AreEqual(photoBaseToArm, Seat / armHeight, photoBaseToArm * tolerance,
                "цоколь к боковой подушке: на снимке 160 к 100 пикселей у правого края. "
                + "Вдвое более тонкая подушка перестаёт работать вместо подлокотника — "
                + "а подлокотников у этого дивана нет, и заменяют их именно они");

            float cushionWidth = SofaLayout.BackCushionWidthFor(dims.x);
            float cushionHeight = SofaLayout.BackrestHeightMM(dims.y, Seat);

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
                "на узком диване боковая подушка обязана ужаться: сохранить её 280 мм значит "
                + "наложить её на спинную");
            Assert.AreEqual(narrow, 2f * arm + 2f * back + 3f * SofaLayout.CushionGapMM, 1e-3f,
                "и ширина по-прежнему расходится без остатка");
        }

        [Test]
        public void BaseCentreYMM_PutsTheBlockOnTheFloor_NotOnTheCentreOfTheBox()
        {
            Assert.AreEqual((Seat - SofaLayout.DefaultHeightMM) * 0.5f,
                SofaLayout.BaseCentreYMM(SofaLayout.DefaultHeightMM, Seat), 1e-3f,
                "основание стоит на полу: его центр смещён вниз ровно настолько, чтобы низ "
                + "совпал с низом габаритной коробки");
        }

        [Test]
        public void EveryCushionRadius_FitsItsOwnProfile_SoTheContourNeverSelfIntersects()
        {
            var dims = new Vector3Int(700, 500, 400);

            foreach (var cushion in SofaLayout.Cushions(dims, 250))
                Assert.LessOrEqual(cushion.RadiusMM,
                    Mathf.Min(cushion.ProfileWidthMM, cushion.ProfileDepthMM) * 0.5f + 1e-3f,
                    "радиус больше половины меньшей стороны выворачивает контур наружу — "
                    + "RoundedRectProfile.Fit ужимает его по РЁБРАМ, а не по диагонали, и "
                    + "два противоположных угла всё равно пересекутся: " + cushion.Name);
        }
    }
}
