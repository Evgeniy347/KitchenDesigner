using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Раскладка кровати: ножки 100 мм, царга, матрас и подушки, а над
    /// изголовьем — необязательная спинка.
    ///
    /// Габарит задан пользователем как «длина 2000, ширина 1800». Длина у этой
    /// семьи мебели живёт по оси Z (глубина), потому что спинка по соглашению
    /// проекта смотрит в -Z — там же, где спинка стула и дивана, и оттуда же
    /// снимает камера изометрии. Значит ширина — это X, и переключатель
    /// «односпальная/двуспальная» меняет именно X: у односпальной и двуспальной
    /// кровати длина одинаковая, отличается ширина.
    ///
    /// Класс на быстром пути (Core/Geometry) намеренно: это арифметика в
    /// миллиметрах, сцена ей не нужна, и она проверяется за 0,3 с вместо
    /// холодного Unity. Углы Эйлера отдаются Vector3, а не Quaternion:
    /// Quaternion.Euler — ECall и под dotnet падает SecurityException.</summary>
    public class BedLayoutTests
    {
        private const float Tol = 0.01f;

        private static Vector3Int Double() => BedLayout.DefaultDimensions(true, true);

        private static Vector3Int Single() => BedLayout.DefaultDimensions(false, true);

        [Test]
        public void DefaultDimensions_OfADoubleBed_AreTwoThousandLongAndEighteenHundredWide()
        {
            Assert.AreEqual(new Vector3Int(1800, 900, 2000), Double(),
                "двуспальная кровать 2000x1800 задана пользователем: 2000 — длина (ось Z), "
                + "1800 — ширина (ось X). Высота 900 — верх спинки");
        }

        [Test]
        public void DefaultDimensions_OfASingleBed_KeepTheSameLength_AndHalveTheWidth()
        {
            Assert.AreEqual(2000, Single().z,
                "односпальная и двуспальная кровати одной длины — отличается только ширина; "
                + "иначе переключатель типа менял бы не тот габарит");
            Assert.AreEqual(900, Single().x,
                "900 мм — обычный российский стандарт односпальной кровати");
        }

        [Test]
        public void VerticalStack_AddsUpToTheDeclaredHeight_WithoutAHeadboard()
        {
            Assert.AreEqual(480, BedLayout.DeckTopMM,
                "ножки 100 (задано пользователем) + царга 200 + матрас 180 = верх матраса "
                + "480 мм над полом — рабочая высота настоящей кровати");
            Assert.AreEqual(600, BedLayout.TopWithoutHeadboardMM,
                "подушка лежит НА матрасе, значит её верх и есть силуэт кровати без спинки; "
                + "габаритная коробка обязана совпадать с силуэтом, её читают снап и "
                + "проверка коллизий");
            Assert.AreEqual(BedLayout.TopWithoutHeadboardMM, BedLayout.HeightFor(false),
                "высота кровати без спинки — это и есть верх подушки, другого потолка у неё нет");
            Assert.AreEqual(900, BedLayout.HeightFor(true),
                "со спинкой высота по умолчанию 900 — спинка поднимается на 300 мм над "
                + "подушками");
        }

        [Test]
        public void PillowTop_OfABedWithoutAHeadboard_LandsExactlyOnTheTopOfTheBox()
        {
            int height = BedLayout.HeightFor(false);
            float top = BedLayout.PillowCentreYMM(height) + BedLayout.PillowThicknessMM * 0.5f;

            Assert.AreEqual(height * 0.5f, top, Tol,
                "без спинки верх подушки — это и есть потолок габаритной коробки: если он "
                + "уедет, коробка начнёт врать про силуэт");
        }

        [Test]
        public void MinHeight_LeavesRoomForTheHeadboard_AndNoneWithoutIt()
        {
            Assert.AreEqual(600, BedLayout.MinHeightMM(false),
                "без спинки ниже верха подушек кровать сжать нельзя");
            Assert.AreEqual(700, BedLayout.MinHeightMM(true),
                "спинка обязана подниматься хотя бы на 100 мм над подушками, иначе это уже "
                + "кровать без спинки, просто с другим флагом");
        }

        [Test]
        public void ADoubleBed_Gets_SixLegs_AndASingleOne_Four()
        {
            Assert.AreEqual(4, BedLayout.LegCount(false),
                "односпальная 900 мм шириной несёт вдвое меньше нагрузки: четырёх углов хватает");
            Assert.AreEqual(6, BedLayout.LegCount(true),
                "у двуспальной пролёт между угловыми ножками 2000 мм под матрасом шириной "
                + "1800: середина длинной стороны провисает, поэтому пара средних ножек");

            Assert.AreEqual(4, BedLayout.LegCentresMM(Single(), false).Length,
                "раскладка обязана выдать столько точек, сколько объявлено LegCount — иначе "
                + "число ножек живёт в двух местах и они разъедутся");
            Assert.AreEqual(6, BedLayout.LegCentresMM(Double(), true).Length,
                "то же самое для двуспальной");
        }

        [Test]
        public void TheMiddleLegs_StandHalfwayAlongTheLongSides_NotUnderTheMattressCentre()
        {
            var legs = BedLayout.LegCentresMM(Double(), true);
            var corner = legs[0];

            Assert.AreEqual(0f, legs[4].y, Tol,
                "средняя ножка стоит ровно посреди длины — там, где провисает царга");
            Assert.AreEqual(0f, legs[5].y, Tol,
                "и вторая средняя тоже: они образуют пару поперёк кровати");
            Assert.AreEqual(corner.x, legs[4].x, Tol,
                "средние ножки стоят на тех же длинных сторонах, что и угловые: ножка под "
                + "серединой матраса не держала бы царгу");
            Assert.AreEqual(-corner.x, legs[5].x, Tol,
                "и симметрично на противоположной длинной стороне");
        }

        [Test]
        public void LegFootprint_StaysInsideTheFrame_ByItsInset()
        {
            var dims = Double();
            var legs = BedLayout.LegCentresMM(dims, true);
            float halfLeg = BedLayout.LegCrossSectionMM * 0.5f;

            foreach (var leg in legs)
            {
                Assert.LessOrEqual(Mathf.Abs(leg.x) + halfLeg,
                    dims.x * 0.5f - BedLayout.LegInsetMM + Tol,
                    "ножка обязана целиком стоять под царгой, отступив от края");
                Assert.LessOrEqual(Mathf.Abs(leg.y) + halfLeg,
                    dims.z * 0.5f - BedLayout.LegInsetMM + Tol,
                    "то же по длине: торчащая из-под царги ножка читается как дефект сборки");
            }
        }

        [Test]
        public void TheMattress_StopsAtTheHeadboard_AndIsInsetOnTheOtherThreeSides()
        {
            var dims = Double();
            var size = BedLayout.MattressSizeMM(dims);
            float centreZ = BedLayout.MattressCentreZMM(dims.z);

            Assert.AreEqual(dims.x - 2f * BedLayout.MattressInsetMM, size.x, Tol,
                "матрас утоплен в царгу на 30 мм с каждой стороны — иначе царга не видна");
            Assert.AreEqual(-dims.z * 0.5f + BedLayout.HeadboardThicknessMM,
                centreZ - size.z * 0.5f, Tol,
                "у изголовья матрас начинается ровно за спинкой: если он заедет под неё, "
                + "два меша будут пересекаться");
            Assert.AreEqual(dims.z * 0.5f - BedLayout.MattressInsetMM,
                centreZ + size.z * 0.5f, Tol,
                "в ногах остаётся тот же отступ 30 мм, что и по бокам");
        }

        [Test]
        public void TheMattress_SitsOnTheFrame_AndItsTopIsTheDeck()
        {
            var dims = Double();
            float top = BedLayout.MattressCentreYMM(dims.y) + BedLayout.MattressThicknessMM * 0.5f;
            float bottom = BedLayout.MattressCentreYMM(dims.y) - BedLayout.MattressThicknessMM * 0.5f;
            float frameTop = BedLayout.FrameCentreYMM(dims.y) + BedLayout.FrameHeightMM * 0.5f;

            Assert.AreEqual(BedLayout.FloorYMM(dims.y) + BedLayout.DeckTopMM, top, Tol,
                "верх матраса — это DeckTopMM над полом, на него ложатся подушки");
            Assert.AreEqual(frameTop, bottom, Tol,
                "матрас лежит НА царге, а не висит над ней и не тонет в ней");
        }

        [Test]
        public void TheFrame_SitsOnTheLegs()
        {
            var dims = Double();
            float legTop = BedLayout.LegCentreYMM(dims.y) + BedLayout.LegHeightMM * 0.5f;
            float frameBottom = BedLayout.FrameCentreYMM(dims.y) - BedLayout.FrameHeightMM * 0.5f;

            Assert.AreEqual(BedLayout.FloorYMM(dims.y) + BedLayout.LegHeightMM, legTop, Tol,
                "ножки ровно 100 мм — это задано пользователем");
            Assert.AreEqual(legTop, frameBottom, Tol,
                "царга стоит на ножках: щель между ними видна сразу, а перекрытие даёт z-fighting");
        }

        [Test]
        public void TheHeadboard_StandsAtMinusZ_FromTheLegTops_ToTheTopOfTheBox()
        {
            var dims = Double();
            float height = BedLayout.HeadboardPanelHeightMM(dims.y);
            float centreY = BedLayout.HeadboardCentreYMM(dims.y);
            float centreZ = BedLayout.HeadboardCentreZMM(dims.z);

            Assert.AreEqual(800, height, Tol,
                "900 общей высоты минус 100 мм ножек: спинка не висит в воздухе");
            Assert.AreEqual(BedLayout.FloorYMM(dims.y) + BedLayout.LegHeightMM,
                centreY - height * 0.5f, Tol,
                "спинка спускается за матрасом до верха ножек, как у настоящей кровати");
            Assert.AreEqual(dims.y * 0.5f, centreY + height * 0.5f, Tol,
                "и упирается ровно в потолок габаритной коробки — она и задаёт высоту");
            Assert.Less(centreZ, 0f,
                "изголовье смотрит в -Z: туда же смотрит спинка стула и дивана и оттуда же "
                + "снимает камера изометрии");
        }

        [Test]
        public void ADoubleBed_Carries_TwoPillows_AndASingleOne_One()
        {
            Assert.AreEqual(1, BedLayout.PillowCount(false),
                "число подушек — следствие типа кровати, а не отдельное свойство");
            Assert.AreEqual(2, BedLayout.PillowCount(true),
                "двуспальная несёт две подушки — так решил пользователь");
            Assert.AreEqual(1, BedLayout.PillowCentresMM(Single(), false).Length,
                "раскладка обязана выдать ровно столько точек, сколько объявлено PillowCount");
            Assert.AreEqual(2, BedLayout.PillowCentresMM(Double(), true).Length,
                "то же самое для двуспальной");
        }

        [Test]
        public void TwoPillows_AreSymmetricAboutTheCentreLine_WithTheGapBetweenThem()
        {
            var dims = Double();
            var centres = BedLayout.PillowCentresMM(dims, true);
            float width = BedLayout.PillowWidthForMM(dims, true);

            Assert.AreEqual(-centres[1].x, centres[0].x, Tol,
                "две подушки лежат симметрично: несимметричная пара читается как ошибка сборки");
            Assert.AreEqual(BedLayout.PillowGapMM,
                (centres[1].x - width * 0.5f) - (centres[0].x + width * 0.5f), Tol,
                "между подушками ровно заданный зазор — иначе они срастаются в один валик");
        }

        [Test]
        public void ASinglePillow_IsCentred()
        {
            Assert.AreEqual(0f, BedLayout.PillowCentresMM(Single(), false)[0].x, Tol,
                "одна подушка лежит по центру кровати, а не с той стороны, где у двуспальной "
                + "была первая");
        }

        [Test]
        public void Pillows_StayOnTheMattress_OnBothWidths()
        {
            var cases = new[] { (dims: Double(), isDouble: true), (dims: Single(), isDouble: false) };

            foreach (var (dims, isDouble) in cases)
            {
                var mattress = BedLayout.MattressSizeMM(dims);
                float mattressCentreZ = BedLayout.MattressCentreZMM(dims.z);
                var size = BedLayout.PillowSizeMM(dims, isDouble);

                foreach (var centre in BedLayout.PillowCentresMM(dims, isDouble))
                {
                    Assert.LessOrEqual(Mathf.Abs(centre.x) + size.x * 0.5f,
                        mattress.x * 0.5f + Tol,
                        "подушка обязана лежать на матрасе, а не свисать с царги");
                    Assert.GreaterOrEqual(centre.z - size.z * 0.5f,
                        mattressCentreZ - mattress.z * 0.5f - Tol,
                        "подушка не должна заезжать в спинку — там начинается щит изголовья");
                    Assert.LessOrEqual(centre.z + size.z * 0.5f,
                        mattressCentreZ + mattress.z * 0.5f + Tol,
                        "и не должна уезжать в ноги: подушки лежат у изголовья");
                }
            }
        }

        [Test]
        public void Pillows_ShrinkOnANarrowBed_InsteadOfHangingOverTheEdge()
        {
            var narrow = new Vector3Int(600, 900, 2000);
            float width = BedLayout.PillowWidthForMM(narrow, true);

            Assert.Less(width, (float)BedLayout.PillowWidthMM,
                "на суженной вручную кровати две подушки обязаны ужаться, а не остаться "
                + "по 700 мм и повиснуть по бокам");
            Assert.GreaterOrEqual(width, (float)BedLayout.MinPartMM,
                "но не тоньше минимальной детали — нулевая подушка это дыра в мешах");
        }

        [Test]
        public void IsDoubleWidth_SplitsTheTwoStandardsAtTheirMidpoint()
        {
            Assert.IsFalse(BedLayout.IsDoubleWidth(BedLayout.SingleWidthMM),
                "стандартная односпальная обязана классифицироваться как односпальная");
            Assert.IsTrue(BedLayout.IsDoubleWidth(BedLayout.DoubleWidthMM),
                "и стандартная двуспальная — как двуспальная");
            Assert.IsFalse(BedLayout.IsDoubleWidth(1349),
                "порог посередине между 900 и 1800 — на 1350");
            Assert.IsTrue(BedLayout.IsDoubleWidth(1350),
                "ровно на пороге кровать считается двуспальной, границу закрываем сверху");
        }

        [Test]
        public void FittedRadius_NeverExceedsHalfOfTheSmallestSide()
        {
            Assert.AreEqual(30f, BedLayout.FittedRadiusMM(90f, 60f, 200f), Tol,
                "скругление больше половины стороны вывернуло бы контур наизнанку");
            Assert.AreEqual(40f, BedLayout.FittedRadiusMM(40f, 600f, 200f), Tol,
                "если радиус помещается, его не трогают: подгонка не должна занижать");
            Assert.AreEqual(0f, BedLayout.FittedRadiusMM(-5f, 600f, 200f), Tol,
                "отрицательный радиус — это ноль, а не вывернутая дуга");
        }

        [Test]
        public void HeadboardOrientation_LaysTheExtrusionUpright()
        {
            Assert.AreEqual(new Vector3(-90f, 0f, 0f), BedLayout.HeadboardEulerAngles,
                "ProfileExtrusionMesh выдавливает контур ВВЕРХ по Y; чтобы щит спинки встал "
                + "вертикально, его надо положить на бок поворотом -90 вокруг X — тем же, "
                + "которым диван ставит подушки спинки");
        }

        [Test]
        public void PillowNames_AreOneBased_AndDistinct()
        {
            Assert.AreEqual("BedPillow1", BedLayout.PillowName(0),
                "имена детей нумеруются с единицы, как Leg1..Leg4 у табуретки");
            Assert.AreEqual("BedPillow2", BedLayout.PillowName(1),
                "подушки ищут ПО ИМЕНИ, а не по индексу в списке детей: индекс поехал бы "
                + "при смене типа кровати, когда вторая подушка исчезает");
        }

        [Test]
        public void TheMattressPlan_IsNoLongerCappedByItsOwnThickness()
        {
            var size = BedLayout.MattressSizeMM(Double());
            float capsule = size.x * 0.5f;

            var cushion = new CushionSurface(size, capsule);
            Assert.Less(cushion.Radius, BedLayout.MattressThicknessMM * 0.5f,
                "пока матрас был подушкой, план и кромку скругляло ОДНО число, и оно "
                + "упиралось в половину толщины: капсульный или круглый план кровати был "
                + "физически недостижим, сколько бы миллиметров ни просили");

            var slab = new SoftSlabSurface(size.x, size.z, CornerRadii.Uniform(capsule),
                size.y, BedLayout.MattressFilletMM);
            Assert.AreEqual(capsule, slab.Radii.MinusXMinusZ, Tol,
                "мягкая плита держит план и кромку врозь: радиус плана ограничен только "
                + "стороной следа, поэтому матрас в пол-ширины радиусом — капсула — доезжает "
                + "до меша целиком");
            Assert.AreEqual(BedLayout.MattressFilletMM, slab.Fillet, Tol,
                "и кромка при этом остаётся своей, 60 мм: она не тянется за радиусом плана");
        }
    }
}
