using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Construction;
using NUnit.Framework;

public class WallLayerQuantitiesTests
{
    private static readonly IReadOnlyList<WallOpening> NoOpenings = Array.Empty<WallOpening>();

    [Test]
    public void WallLayerQuantities_NetAreaM2_SubtractsOpeningsAtFullArea()
    {
        var window = new WallOpening("Okno-1", 1200f, 1400f);
        var net = WallLayerQuantities.NetAreaM2(4000f, 2700f, new[] { window });

        Assert.AreEqual(9.12d, net, 1e-9d,
            "стена 4,0 × 2,7 = 10,8 м²; окно вырезает слой НАСКВОЗЬ по площади фасада, "
            + "1,2 × 1,4 = 1,68 м²; 10,8 − 1,68 = 9,12 м² — это площадь, доступная под "
            + "утеплитель и обшивку, реиспользует ту же схему вычитания, что и "
            + "WallQuantities.NetVolumeM3, только по площади, а не по объёму");
    }

    [Test]
    public void WallLayerQuantities_InsulationAreaM2_EqualsTheNetAreaOfTheWall()
    {
        var window = new WallOpening("Okno-1", 1200f, 1400f);
        Assert.AreEqual(9.12d,
            WallLayerQuantities.InsulationAreaM2(4000f, 2700f, new[] { window }), 1e-9d,
            "утеплитель кроет ровно ту же чистую площадь, что осталась за вычетом проёмов");
    }

    [Test]
    public void WallLayerQuantities_InsulationVolumeM3_IsNetAreaTimesThickness_Is1_368m3()
    {
        var window = new WallOpening("Okno-1", 1200f, 1400f);
        var volume = WallLayerQuantities.InsulationVolumeM3(4000f, 2700f, 150f, new[] { window });

        Assert.AreEqual(1.368d, volume, 1e-9d,
            "9,12 м² × 0,15 м толщины = 1,368 м³ утеплителя");
    }

    [Test]
    public void WallLayerQuantities_CladdingAreaM2_EqualsTheNetAreaOfTheWall()
    {
        var window = new WallOpening("Okno-1", 1200f, 1400f);
        Assert.AreEqual(9.12d,
            WallLayerQuantities.CladdingAreaM2(4000f, 2700f, new[] { window }), 1e-9d,
            "обшивка кроет ту же чистую площадь: отдельный метод существует ради имени "
            + "статьи в ведомости («Утеплитель» и «Обшивка» — разные позиции), а не "
            + "ради другой формулы");
    }

    [Test]
    public void WallLayerQuantities_OpeningsBiggerThanTheWall_GiveZero_NotANegativeArea()
    {
        var gate = new WallOpening("Vorota", 5000f, 3000f);
        var net = WallLayerQuantities.NetAreaM2(1000f, 1000f, new[] { gate });

        Assert.AreEqual(0d, net, 1e-9d,
            "проём шире стены — ошибка ввода, но площадь слоя не может уйти в минус");
    }

    [Test]
    public void WallLayerQuantities_NullOpenings_AreReadAsAWallWithoutOpenings()
    {
        var net = WallLayerQuantities.NetAreaM2(4000f, 2700f, null);

        Assert.AreEqual(10.8d, net, 1e-9d,
            "стена, у которой список проёмов ещё не собран, считается глухой, а не падает — "
            + "проводка со стороны сцены отдаёт null до первого проёма");
    }

    [Test]
    public void WallLayerQuantities_VentGapBattenCount_GrowsOnlyWhenAWholeStepIsCrossed()
    {
        Assert.AreEqual(1, WallLayerQuantities.VentGapBattenCount(1f, 625f),
            "любая ненулевая стена начинается с одного бруска");
        Assert.AreEqual(2, WallLayerQuantities.VentGapBattenCount(625f, 625f),
            "ровно один шаг — два бруска, оба края");
        Assert.AreEqual(2, WallLayerQuantities.VentGapBattenCount(1249f, 625f),
            "1 249 мм — ещё не два шага: формула считает ПОЛНЫЕ шаги, "
            + "как WallQuantities.StudCount, а не ⌈⌉, как столбы забора — недобор "
            + "в обрешётке восполняет нахлёст листа, недобор столба забора — нет");
        Assert.AreEqual(3, WallLayerQuantities.VentGapBattenCount(1250f, 625f));
        Assert.AreEqual(0, WallLayerQuantities.VentGapBattenCount(0f, 625f),
            "у стены нулевой длины брусков нет");
    }

    [Test]
    public void WallLayerQuantities_VentGapBattenCount_ZeroOrNegativeStep_IsZero()
    {
        Assert.AreEqual(0, WallLayerQuantities.VentGapBattenCount(4000f, 0f),
            "нулевой шаг — невалидный ввод, а не брусок на каждый миллиметр");
        Assert.AreEqual(0, WallLayerQuantities.VentGapBattenCount(4000f, -10f),
            "отрицательный шаг — тоже невалидный ввод");
    }

    [Test]
    public void WallLayerQuantities_VentGapBattenRunningMetres_7Battens_Height2700mm_Is18_9m()
    {
        var metres = WallLayerQuantities.VentGapBattenRunningMetres(4000f, 2700f, 625f);

        Assert.AreEqual(18.9d, metres, 1e-9d,
            "4 000 / 625 = 6,4 → 6 полных шагов, +1 = 7 брусков; каждый идёт на всю "
            + "высоту стены 2,7 м; 7 × 2,7 = 18,9 погонных метра обрешётки");
    }
}
