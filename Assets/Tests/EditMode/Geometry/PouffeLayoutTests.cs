using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Пуфик — это ДВА объёма друг на друге, а не табуретка с большим
    /// радиусом: снизу глухая обитая тумба до самого пола (она же заменяет
    /// ножки), сверху тонкая мягкая сидушка. У табуретки наоборот: жёсткое
    /// сиденье 30 мм и четыре отдельные ножки 40x40 под ним. Разная
    /// конструкция, разный список деталей, разные слоты материала — числа тут
    /// ни при чём.
    ///
    /// Раскладка живёт на быстром пути (Core/Geometry) по той же причине, что и
    /// SofaLayout: это арифметика в миллиметрах, сцена ей не нужна, и потому
    /// она проверяется за 0,3 с вместо холодного Unity.
    ///
    /// Главное свойство, которое здесь удерживается, — СИДУШКА НИКОГДА НЕ
    /// СВЕШИВАЕТСЯ С ТУМБЫ. Тумба строится профильным выдавливанием и её углы
    /// срезаны радиусом; сидушка — мягкая плита SoftSlabSurface того же
    /// семейства контуров, СЖАТАЯ ВНУТРЬ на отступ. Сжатие скруглённого
    /// прямоугольника внутрь на d — это стороны минус 2d и радиусы минус d,
    /// поэтому круглая тумба несёт КРУГЛУЮ сидушку, а квадратная — квадратную.
    ///
    /// Отступ при этом остался пропорционален радиусу, и это уже не про
    /// вписанность, а про вид: угол прямоугольника, вписанного в скругление
    /// радиуса r, отходит от дуги ровно на r*(1 - 1/sqrt2), и на этом числе
    /// сидушка круглого пуфика читается отдельной деталью, а не срезом тумбы.
    ///
    /// До SoftSlabSurface сидушка была подушкой CushionMesh — коробкой, чьё
    /// угловое скругление подрезано собственной толщиной (50 мм дают не больше
    /// 25). На круглой тумбе лежал КВАДРАТ, вписанный в окружность; это и
    /// видно на iso_pouffe_450x400x450_round.png.</summary>
    public class PouffeLayoutTests
    {
        private const float Tol = 1e-3f;

        private static Vector3Int Default() => new Vector3Int(
            PouffeLayout.DefaultWidthMM, PouffeLayout.DefaultHeightMM,
            PouffeLayout.DefaultDepthMM);

        private static void AssertSeatStaysOnTheBody(Vector3Int dims, int radiusMM, string what)
        {
            var seat = PouffeLayout.Seat(dims, radiusMM, PouffeLayout.DefaultSeatThicknessMM);
            float halfW = seat.ProfileWidthMM * 0.5f;
            float halfD = seat.ProfileDepthMM * 0.5f;
            int fitted = PouffeLayout.ClampCornerRadiusMM(dims, radiusMM);

            foreach (var corner in new[]
                {
                    new Vector2(halfW, halfD), new Vector2(-halfW, halfD),
                    new Vector2(halfW, -halfD), new Vector2(-halfW, -halfD),
                })
                Assert.LessOrEqual(
                    RoundedRectProfile.SignedDistance(corner, dims.x, dims.z, fitted), 0f,
                    "угол сидушки вылез за контур тумбы (" + what + "): подушка висела бы "
                    + "в воздухе над срезанным углом. Отступ сидушки обязан расти вместе с "
                    + "радиусом, а не быть константой");
        }

        [Test]
        public void Defaults_AreALowSquarePouffe_450x400x450()
        {
            Assert.AreEqual(450, PouffeLayout.DefaultWidthMM,
                "450 мм в плане — середина обычного диапазона пуфика 350-500 мм и заметно "
                + "больше табуретки: пуфик садятся на него боком и ставят ноги, ему нужна "
                + "площадь, а не точка опоры");
            Assert.AreEqual(450, PouffeLayout.DefaultDepthMM,
                "план квадратный, чтобы ВСЯ разница формы (квадрат - скругление - круг) "
                + "шла одним параметром радиуса, а не парой ширина/глубина");
            Assert.AreEqual(400, PouffeLayout.DefaultHeightMM,
                "400 мм — середина диапазона 350-450: ниже табуретки (450) и чуть выше "
                + "сиденья дивана (360), то есть пуфик встаёт рядом с диваном как "
                + "дополнительное место, а не как рабочий стул");
            Assert.AreEqual(50, PouffeLayout.DefaultSeatThicknessMM,
                "сидушка 50 мм — это то, что назвал пользователь; на этой толщине мягкая "
                + "плита получает фаску кромки 25 мм, то есть бок читается полукруглым и "
                + "выглядит мягким, а не доской");
            Assert.AreEqual(120, PouffeLayout.DefaultCornerRadiusMM,
                "120 мм — тот же радиус скругления, что у дивана по умолчанию: пуфик и "
                + "диван стоят рядом и обязаны читаться одной семьёй. На 450 мм это "
                + "внятное скругление, но ещё очевидно квадратный пуфик");
        }

        [Test]
        public void BodyHeight_IsEverythingTheSeatDidNotTake()
        {
            Assert.AreEqual(350, PouffeLayout.BodyHeightMM(400, 50),
                "нижний объём — это весь пуфик минус сидушка: у него нет ножек, он сам "
                + "стоит на полу");
            Assert.AreEqual(FurnitureLayout.LegHeightMM(400, 50),
                PouffeLayout.BodyHeightMM(400, 50),
                "и это ровно то же выражение, что даёт высоту ножки у табуретки: у пуфика "
                + "роль ножки играет тумба. Своя копия арифметики разъехалась бы с "
                + "FurnitureLayout молча");
        }

        [Test]
        public void SeatCentreYMM_PutsTheSeatFlushWithTheTopOfThePouffe()
        {
            Assert.AreEqual(175f, PouffeLayout.SeatCentreYMM(400, 50), Tol,
                "верх сидушки совпадает с верхом габарита: 400/2 - 50/2 = 175 мм");
            Assert.AreEqual(FurnitureLayout.TopCentreY(400, 50) / AppConstants.MM_TO_UNITS,
                PouffeLayout.SeatCentreYMM(400, 50), Tol,
                "то же самое, что считает FurnitureLayout для крышки, только в "
                + "миллиметрах: SofaUpholstery принимает центр части в мм и сама "
                + "переводит в юниты");
        }

        [Test]
        public void BodyCentreY_AndSeatCentre_LeaveNoGapBetweenTheTwoVolumes()
        {
            float toU = AppConstants.MM_TO_UNITS;
            float bodyTop = PouffeLayout.BodyCentreY(400, 50)
                + PouffeLayout.BodyHeightMM(400, 50) * 0.5f * toU;
            float seatBottom = (PouffeLayout.SeatCentreYMM(400, 50) - 50 * 0.5f) * toU;

            Assert.AreEqual(seatBottom, bodyTop, 1e-6f,
                "сидушка лежит на тумбе без щели и без взаимного проникновения: щель "
                + "видна только на рендере, и тестами её ни разу не поймали");
            Assert.AreEqual(-400 * 0.5f * toU,
                PouffeLayout.BodyCentreY(400, 50) - PouffeLayout.BodyHeightMM(400, 50) * 0.5f * toU,
                1e-6f,
                "низ тумбы совпадает с низом габарита: пуфик стоит на полу всей "
                + "плоскостью, ножек у него нет");
        }

        [Test]
        public void MaxCornerRadius_TakesTheSmallerSideOfThePlan_NotTheWidth()
        {
            Assert.AreEqual(175, PouffeLayout.MaxCornerRadiusMM(new Vector3Int(600, 400, 350)),
                "радиус ограничен половиной МЕНЬШЕЙ стороны плана: 350/2. На квадрате "
                + "перепутанные оси дали бы тот же ответ, поэтому число несимметричное");
            Assert.AreEqual(225, PouffeLayout.MaxCornerRadiusMM(Default()),
                "на квадратном плане 450 максимум радиуса — это полностью круглый пуфик");
        }

        [Test]
        public void ClampCornerRadius_HoldsBothEnds()
        {
            Assert.AreEqual(0, PouffeLayout.ClampCornerRadiusMM(Default(), -50),
                "отрицательный радиус — это квадратный пуфик, а не вывернутый контур");
            Assert.AreEqual(225, PouffeLayout.ClampCornerRadiusMM(Default(), 10000),
                "запрошенный сверх меры радиус подрезается до круга");
        }

        [Test]
        public void MaxSeatThickness_IsAThirdOfThePouffe_SoItStaysACushionAndNotASecondBox()
        {
            Assert.AreEqual(133, PouffeLayout.MaxSeatThicknessMM(400),
                "сидушка не вправе занимать больше трети высоты: на половине пуфик "
                + "перестаёт читаться как «тумба с подушкой» и превращается в две "
                + "одинаковые коробки");
            Assert.AreEqual(PouffeLayout.MinSeatThicknessMM, PouffeLayout.MaxSeatThicknessMM(30),
                "у крошечного пуфика треть высоты меньше минимальной подушки — верхняя "
                + "граница обязана уступить нижней, иначе Clamp получит перевёрнутый "
                + "диапазон и вернёт мусор");
        }

        [Test]
        public void ClampSeatThickness_HoldsBothEnds()
        {
            Assert.AreEqual(PouffeLayout.MinSeatThicknessMM,
                PouffeLayout.ClampSeatThicknessMM(400, 1),
                "тоньше 20 мм сидушка вырождается: фаска кромки — половина толщины, и "
                + "на 10 мм мягкости уже не видно");
            Assert.AreEqual(133, PouffeLayout.ClampSeatThicknessMM(400, 300),
                "и толще трети высоты — тоже нет");
            Assert.AreEqual(50, PouffeLayout.ClampSeatThicknessMM(400, 50),
                "штатное значение проходит нетронутым: сторож, который правит ВСЁ, "
                + "не отличить от сторожа, который не правит ничего");
        }

        [Test]
        public void SeatInset_GrowsWithTheRadius_SoTheSeatNeverHangsOverACutCorner()
        {
            Assert.AreEqual(PouffeLayout.MinSeatInsetMM, PouffeLayout.SeatInsetMM(0),
                "на квадратном пуфике отступ минимальный — 15 мм: сидушка обязана быть "
                + "видна отдельной деталью, а не сливаться с тумбой в один параллелепипед");
            Assert.AreEqual(66, PouffeLayout.SeatInsetMM(225),
                "на круглом пуфике 450 мм отступ 66 мм даёт ровно квадрат, вписанный в "
                + "окружность: 225*(1 - 1/sqrt2) = 65,9, округляем вверх");
            Assert.Greater(PouffeLayout.SeatInsetMM(225), PouffeLayout.SeatInsetMM(120),
                "чем круглее пуфик, тем глубже утоплена сидушка — иначе её угол окажется "
                + "в воздухе");
        }

        [Test]
        public void Seat_StaysOnTheBody_AtEveryShapeFromSquareToRound()
        {
            AssertSeatStaysOnTheBody(Default(), 0, "квадратный пуфик");
            AssertSeatStaysOnTheBody(Default(), 60, "слегка скруглённый");
            AssertSeatStaysOnTheBody(Default(), PouffeLayout.DefaultCornerRadiusMM, "штатный");
            AssertSeatStaysOnTheBody(Default(), 225, "полностью круглый");
            AssertSeatStaysOnTheBody(new Vector3Int(600, 400, 350), 175,
                "капсула на несимметричном плане");
        }

        [Test]
        public void AConstantInset_WouldLetTheSeatHangOverARoundPouffe()
        {
            var dims = Default();
            float half = dims.x * 0.5f - PouffeLayout.MinSeatInsetMM;

            Assert.Greater(
                RoundedRectProfile.SignedDistance(new Vector2(half, half), dims.x, dims.z, 225),
                0f,
                "положительный контроль к предыдущему тесту: с постоянным отступом 15 мм "
                + "угол сидушки на круглом пуфике ВЫЛЕЗАЕТ наружу. Без этой проверки тест "
                + "на вписанность зеленел бы и на константе");
        }

        [Test]
        public void SeatRadius_IsTheBodyRadiusOffsetInwardByTheInset()
        {
            Assert.AreEqual(159f, PouffeLayout.SeatRadiusMM(225), Tol,
                "круглый пуфик 450 мм: тумба радиуса 225, отступ 66, значит сидушка — "
                + "окружность радиуса 159 на плане 318x318. То есть КРУГ, а не квадрат: "
                + "внутренний офсет скруглённого прямоугольника снимает по d и со "
                + "стороны, и с радиуса. Подрезка радиуса толщиной (25 мм) вернула бы "
                + "квадратную сидушку на круглую тумбу");
            Assert.AreEqual(PouffeLayout.SeatRadiusMM(225),
                (450 - 2 * PouffeLayout.SeatInsetMM(225)) * 0.5f, Tol,
                "и этот радиус — ровно половина стороны сжатого плана, то есть сидушка "
                + "круглого пуфика кругла ПОЛНОСТЬЮ, без остатка прямого участка");
            Assert.AreEqual(0f, PouffeLayout.SeatRadiusMM(0), Tol,
                "у квадратного пуфика сидушка тоже с прямыми углами в плане: форма "
                + "верха и низа обязана читаться одной");
            Assert.AreEqual(0f, PouffeLayout.SeatRadiusMM(10), Tol,
                "и при радиусе меньше минимального отступа офсет упирается в ноль, а не "
                + "уходит в отрицательный: вывернутый контур дал бы вывернутый меш");
        }

        [Test]
        public void SeatFillet_ComesFromTheThickness_AndIsIndependentOfThePlan()
        {
            float toU = AppConstants.MM_TO_UNITS;
            var seat = PouffeLayout.Seat(Default(), 225, 50);
            var surface = new SoftSlabSurface(seat.ProfileWidthMM * toU,
                seat.ProfileDepthMM * toU, CornerRadii.Uniform(seat.RadiusMM * toU),
                seat.ThicknessMM * toU,
                seat.ThicknessMM * toU * SoftSlabSurface.MaxFilletThicknessRatio);

            Assert.AreEqual(25f * toU, surface.Fillet, 1e-6f,
                "фаска кромки — половина толщины сидушки и БОЛЬШЕ НИЧЕГО: именно "
                + "разведение плана и толщины отличает мягкую плиту от подушки, у "
                + "которой одно число подрезало другое");
            Assert.AreEqual(seat.RadiusMM * toU, surface.Radii.PlusXPlusZ, 1e-6f,
                "а план прошёл нетронутым: 159 мм радиуса на сидушке толщиной 50 — "
                + "ровно то, что CushionSurface подрезал бы до 25");
        }

        [Test]
        public void Seat_IsASoftSlab_LyingFlat_AndNamedSoItCanBeFoundByName()
        {
            var seat = PouffeLayout.Seat(Default(), PouffeLayout.DefaultCornerRadiusMM,
                PouffeLayout.DefaultSeatThicknessMM);

            Assert.AreEqual(FurniturePartShape.SoftSlab, seat.Shape,
                "сидушка — мягкая плита: контур повторяет тумбу, а верхняя и нижняя "
                + "кромки скруглены фаской. Профильное выдавливание дало бы доску с "
                + "острой кромкой, то есть второе жёсткое сиденье табуретки; подушка "
                + "CushionMesh не умеет круглый план и дала бы квадрат на круглой тумбе");
            Assert.AreEqual(FurniturePartOrientation.Horizontal, seat.Orientation,
                "и лежит плашмя: разворот нужен только вертикальным деталям дивана");
            Assert.AreEqual(PouffeLayout.SeatName, seat.Name,
                "имя детали — единственный способ найти её потом: поиск ребёнка по "
                + "индексу однажды уже уронил тайлинг декора на ножку");
        }

        [Test]
        public void Seat_StaysInsideTheDeclaredBox()
        {
            var dims = Default();
            var seat = PouffeLayout.Seat(dims, PouffeLayout.DefaultCornerRadiusMM,
                PouffeLayout.DefaultSeatThicknessMM);
            var size = seat.SizeMM;
            var half = new Vector3(dims.x * 0.5f, dims.y * 0.5f, dims.z * 0.5f);

            for (int axis = 0; axis < 3; axis++)
            {
                Assert.LessOrEqual(seat.CentreMM[axis] + size[axis] * 0.5f, half[axis] + Tol,
                    "деталь вылезает за габаритную коробку по оси " + axis + ": рамка "
                    + "изометрического снимка и AABB для привязки строятся по DimensionsMM");
                Assert.GreaterOrEqual(seat.CentreMM[axis] - size[axis] * 0.5f, -half[axis] - Tol,
                    "то же снизу, ось " + axis);
            }
        }

        [Test]
        public void Seat_OnANonSquarePlan_IsInsetOnBothAxesSeparately()
        {
            var seat = PouffeLayout.Seat(new Vector3Int(600, 400, 350), 100,
                PouffeLayout.DefaultSeatThicknessMM);
            int inset = PouffeLayout.SeatInsetMM(100);

            Assert.AreEqual(600 - 2 * inset, seat.ProfileWidthMM, Tol,
                "по ширине отступ снимается с обеих сторон");
            Assert.AreEqual(350 - 2 * inset, seat.ProfileDepthMM, Tol,
                "по глубине — ровно столько же: числа 600 и 350 взяты разными нарочно, "
                + "на квадрате перепутанные оси дали бы тот же ответ");
        }

        [Test]
        public void Seat_ClampsTheParametersItself_SoTheMeshNeverInvertsOnBadInput()
        {
            var seat = PouffeLayout.Seat(Default(), 10000, 10000);

            Assert.AreEqual(PouffeLayout.MaxSeatThicknessMM(PouffeLayout.DefaultHeightMM),
                seat.ThicknessMM, Tol,
                "раскладка обязана подрезать вход сама: она вызывается и из элемента, и "
                + "из тестов, и второй путь не обязан помнить про Clamp");
            Assert.Greater(seat.ProfileWidthMM, 0f,
                "ширина подушки остаётся положительной даже на предельном радиусе — "
                + "нулевая или отрицательная дала бы вывернутый меш");
        }
    }
}
