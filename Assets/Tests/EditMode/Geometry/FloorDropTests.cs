using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;

namespace KitchenDesigner.Tests.Geometry
{
    /// <summary>Арифметика посадки напольного элемента на опору под ним.
    ///
    /// «Посадка на пол» — это НЕ то же, что <c>IAutoSeated</c>. Тот меняет
    /// СВОЙ ГАБАРИТ под зазор (колонна, винтовая опора), и ElementMover
    /// дописывает за ним ResizeCommand. Ванне и унитазу менять нечего: им нужно
    /// просто встать низом на то, что под ними. Габарит не трогается, поэтому и
    /// отмена бесплатна — MoveCommand строится ПОСЛЕ посадки и запоминает уже
    /// севшую позицию.
    ///
    /// Три решения, которые здесь и записаны:
    ///
    /// 1. Ищется не пол мира (y = 0), а САМАЯ ВЫСОКАЯ опора под пятном элемента.
    ///    «Всегда прибивать к y = 0» уничтожает ванну, стоящую на подиуме;
    ///    «садиться, только когда под тобой ничего нет» — наоборот, роняет ванну
    ///    с полутора метров в пустой комнате и оставляет её висеть над подиумом.
    ///    Гравитация до ближайшей поверхности снизу верна в обоих случаях.
    /// 2. Опора берётся по ПЕРЕСЕЧЕНИЮ пятен в плане, а не по накрытию центра.
    ///    Ванна, наполовину свесившаяся с подиума, опирается на подиум — по
    ///    центру её уже ничто не держит, и посадка «по центру» сбросила бы её
    ///    на пол мимо привязки, которая её туда и поставила.
    /// 3. Двигаться можно только ВНИЗ и только по Y. Отсюда мир со SnapSystem:
    ///    привязка, посадившая элемент на горизонтальную грань, даёт зазор
    ///    меньше допуска контакта, и посадка становится пустой операцией; а
    ///    привязка по вертикальной грани переживает изменение одного Y.
    ///    Прыжка «прилип к грани — и сразу на пол» не бывает не потому, что он
    ///    где-то запрещён, а потому, что двигать оказывается нечего.</summary>
    public class FloorDropTests
    {
        private const float U = AppConstants.MM_TO_UNITS;

        private static Span Metres(float min, float max) => new Span(min, max);

        private static FloorSupport Slab(float topY, float minX, float maxX,
            float minZ, float maxZ) =>
            new FloorSupport(Metres(minX, maxX), Metres(minZ, maxZ), topY);

        private static FloorSupport WholeFloor(float topY) =>
            Slab(topY, -10f, 10f, -10f, 10f);

        private static float? TopUnder(float bottomY, params FloorSupport[] supports) =>
            FloorDrop.SupportTopUnder(Metres(-0.85f, 0.85f), Metres(-0.35f, 0.35f), bottomY,
                new List<FloorSupport>(supports));

        [Test]
        public void AnEmptyScene_HasNothingToSeatOn()
        {
            Assert.IsNull(TopUnder(1.5f),
                "без единой опоры сажать не на что: элемент остаётся там, где его отпустили, "
                + "а не проваливается в жёстко зашитый ноль");
        }

        [Test]
        public void TheFloor_CarriesAnElementHangingInTheAir()
        {
            Assert.AreEqual(0f, TopUnder(1.5f, WholeFloor(0f))!.Value, Tolerance.EpsilonUnits,
                "ванна, брошенная в воздухе, опирается на пол");
        }

        [Test]
        public void APodiumUnderTheFootprint_WinsOverTheFloor()
        {
            var top = TopUnder(0.6f, WholeFloor(0f), Slab(0.15f, -1f, 1f, -1f, 1f));

            Assert.AreEqual(0.15f, top!.Value, Tolerance.EpsilonUnits,
                "опора выбирается САМАЯ ВЫСОКАЯ из тех, что ниже: иначе ванна на подиуме "
                + "проваливалась бы сквозь него на пол при каждом перетаскивании");
        }

        [Test]
        public void AnOverhangingPodium_StillCarries_EvenThoughItMissesTheCentre()
        {
            var top = TopUnder(0.6f, WholeFloor(0f), Slab(0.15f, 0.5f, 2f, -1f, 1f));

            Assert.AreEqual(0.15f, top!.Value, Tolerance.EpsilonUnits,
                "опора ищется по пересечению пятен, а не по накрытию центра: у ванны "
                + "1700 мм центр висит в воздухе задолго до того, как она свалится "
                + "с подиума");
        }

        [Test]
        public void ASlabBesideTheFootprint_CarriesNothing()
        {
            Assert.IsNull(TopUnder(1.5f, Slab(0.15f, 2f, 3f, -1f, 1f)),
                "плита, не пересекающая пятно ни по X, ни по Z, элемент не держит");
        }

        [Test]
        public void ASurfaceAboveTheElement_IsNotASupport()
        {
            Assert.IsNull(TopUnder(0.1f, Slab(0.9f, -1f, 1f, -1f, 1f)),
                "столешница над ванной — не опора: посадка ходит только ВНИЗ, иначе "
                + "элемент запрыгивал бы на первое, что окажется над ним");
        }

        [Test]
        public void ASurfaceWithinContactTolerance_CountsAsTheSupport()
        {
            float bottomY = 0.5f;
            float touchingFromAbove = bottomY + FloorDrop.ContactUnits * 0.5f;

            Assert.AreEqual(touchingFromAbove,
                TopUnder(bottomY, Slab(touchingFromAbove, -1f, 1f, -1f, 1f))!.Value,
                Tolerance.EpsilonUnits,
                "контакт с точностью до Tolerance.ContactMm — это контакт, а не «опора "
                + "выше меня»: иначе округление позиции к миллиметровой сетке отбирало бы "
                + "у элемента ту самую поверхность, на которой он стоит");
        }

        [Test]
        public void AnElementAlreadyResting_IsLeftAlone()
        {
            Assert.IsFalse(FloorDrop.WorthSeating(0f, 0f),
                "нулевой зазор двигать нечего");
            Assert.IsFalse(FloorDrop.WorthSeating(FloorDrop.ContactUnits * 0.5f, 0f),
                "зазор меньше допуска контакта — шум позиции, а не висящий элемент. "
                + "Здесь же и мир с привязкой: SnapSystem, посадившая элемент на "
                + "горизонтальную грань, оставляет зазор ниже этого порога, и посадка "
                + "не трогает ничего");
        }

        [Test]
        public void AVisibleGap_IsWorthSeating()
        {
            Assert.IsTrue(FloorDrop.WorthSeating(1f * U, 0f),
                "миллиметр над полом уже видно, и ради него посадка и существует");
        }

        [Test]
        public void SeatedCentre_MovesTheElementDownByExactlyTheGap()
        {
            float centreY = 1.5f;
            float bottomY = 1.21f;

            Assert.AreEqual(centreY - 1.21f, FloorDrop.SeatedCentreY(centreY, bottomY, 0f),
                Tolerance.EpsilonUnits,
                "центр опускается ровно на зазор: смещение центра над низом у элемента "
                + "своё (у унитаза это не половина габарита), и посадка обязана его "
                + "сохранить, а не пересчитывать из размеров");
        }

        [Test]
        public void SeatedCentre_KeepsTheOffsetWhenLandingOnAPodium()
        {
            Assert.AreEqual(0.4f, FloorDrop.SeatedCentreY(1.5f, 1.25f, 0.15f),
                Tolerance.EpsilonUnits,
                "на подиуме 150 мм элемент стоит на 150 мм выше, а не «на полу»");
        }
    }
}
