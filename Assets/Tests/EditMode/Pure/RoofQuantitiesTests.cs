using NUnit.Framework;
using KitchenDesigner.Core.Construction;

public class RoofQuantitiesTests
{
    private static readonly RoofFootprint Footprint6000By4000 = new RoofFootprint(0f, 6000f, 0f, 4000f);

    private static RoofFrame GableFrameOverhang500() =>
        RoofPitchPlanes.Build(Footprint6000By4000, RoofType.Gable, RoofRidgeAxis.Auto, 500f);

    [Test]
    public void RoofQuantities_PlanAreaM2_GableOverAnExtendedFootprint_Is35m2()
    {
        Assert.AreEqual(35d, RoofQuantities.PlanAreaM2(GableFrameOverhang500()), 1e-6d,
            "план крыши — расширенный контур 7,0 × 5,0 м = 35 м², не зависит от угла ската");
    }

    [Test]
    public void RoofQuantities_PitchAreaM2_35m2Plan_Pitch30Degrees_Is40_41m2()
    {
        var area = RoofQuantities.PitchAreaM2(GableFrameOverhang500(), 30f);

        Assert.AreEqual(40.414518843273804d, area, 1e-6d,
            "площадь ската = план / cos(30°) = 35 / 0,8660254 = 40,4145 м² — скат длиннее "
            + "своей проекции на пол ровно во столько раз");
        Assert.Greater(area, 35d, "наклонная площадь обязана быть БОЛЬШЕ плоской проекции");
    }

    [Test]
    public void RoofQuantities_PitchAreaM2_Pitch90Degrees_IsZero_NotInfinity()
    {
        var area = RoofQuantities.PitchAreaM2(GableFrameOverhang500(), 90f);

        Assert.AreEqual(0d, area, 1e-9d,
            "cos(90°) = 0 — вертикальная «крыша» не имеет смысла как скат; вернуть 0, а не "
            + "делить на ноль и не выбрасывать исключение из-за некорректного угла");
    }

    [Test]
    public void RoofQuantities_CoveringAreaM2_AddsTheWastePercentOnTopOfThePitchArea()
    {
        var pitchArea = RoofQuantities.PitchAreaM2(GableFrameOverhang500(), 30f);
        var covering = RoofQuantities.CoveringAreaM2(pitchArea, 10f);

        Assert.AreEqual(44.45597072760119d, covering, 1e-6d,
            "40,414519 × 1,10 = 44,455971 м² — запас 10 % поверх площади ската");
    }

    [Test]
    public void RoofQuantities_CoveringAreaM2_ZeroWaste_EqualsThePitchAreaExactly()
    {
        var pitchArea = RoofQuantities.PitchAreaM2(GableFrameOverhang500(), 30f);
        var covering = RoofQuantities.CoveringAreaM2(pitchArea, 0f);

        Assert.AreEqual(pitchArea, covering, 1e-9d,
            "без запаса покрытие равно площади ската один в один");
    }

    [Test]
    public void RoofQuantities_RidgeEaveGutter_ConvertMillimetresToMetres()
    {
        var frame = GableFrameOverhang500();

        Assert.AreEqual(6d, RoofQuantities.RidgeLengthM(frame), 1e-9d, "6 000 мм = 6 м");
        Assert.AreEqual(24d, RoofQuantities.EaveLengthM(frame), 1e-9d, "24 000 мм = 24 м");
        Assert.AreEqual(14d, RoofQuantities.GutterLengthM(frame), 1e-9d, "14 000 мм = 14 м");
    }

    [Test]
    public void RoofQuantities_RafterCountPerPlane_GrowsOnlyWhenAWholeStepIsCrossed()
    {
        Assert.AreEqual(12, RoofQuantities.RafterCountPerPlane(7000f, 600f),
            "7 000 / 600 = 11,67 → 11 полных шагов, +1 = 12 стропил на скат, как у "
            + "WallQuantities.StudCount — недобор шага восполняет свес обрешётки");
        Assert.AreEqual(0, RoofQuantities.RafterCountPerPlane(0f, 600f),
            "нулевой карниз — стропил нет");
        Assert.AreEqual(0, RoofQuantities.RafterCountPerPlane(7000f, 0f),
            "нулевой шаг — невалидный ввод, а не стропило на каждый миллиметр");
    }

    [Test]
    public void RoofQuantities_RafterCount_GableTwoPlanes_Step600mm_Is24Total()
    {
        Assert.AreEqual(24, RoofQuantities.RafterCount(GableFrameOverhang500(), 600f),
            "по 12 стропил на каждый из двух скатов = 24");
    }

    [Test]
    public void RoofQuantities_RafterLengthM_IsTheSlantLength_LongerThanTheRun()
    {
        var lengthM = RoofQuantities.RafterLengthM(2500f, 30f);

        Assert.AreEqual(2.8867513459481287d, lengthM, 1e-9d,
            "стропило идёт по СКАТУ, а не по горизонтали: 2,5 / cos(30°) = 2,886751 м");
        Assert.Greater(lengthM, 2.5d, "наклонная длина обязана быть больше горизонтального пролёта");
    }

    [Test]
    public void RoofQuantities_RafterVolumeM3_GableTwoPlanes_Step600mm_Pitch30_Is0_5196m3()
    {
        var volume = RoofQuantities.RafterVolumeM3(GableFrameOverhang500(), 600f, 30f);

        Assert.AreEqual(0.5196152422706631d, volume, 1e-6d,
            "12 стропил на скат × 2 ската = 24 штуки; сечение по RafterSectionTable для "
            + "пролёта 2 500 мм — 50×150 мм (первая строка); длина стропила 2,886751 м; "
            + "24 × 2,886751 × 0,05 × 0,15 = 0,519615 м³, посчитано руками");
    }

    [Test]
    public void RoofQuantities_RafterVolumeM3_ZeroStep_IsZero_NotACrash()
    {
        Assert.AreEqual(0d, RoofQuantities.RafterVolumeM3(GableFrameOverhang500(), 0f, 30f), 1e-9d);
    }
}
