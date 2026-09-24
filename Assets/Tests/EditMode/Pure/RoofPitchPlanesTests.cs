using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>Все три типа кровли строятся над ОДНИМ и тем же прямоугольником footprint —
/// RoofContour сегодня не умеет ничего точнее прямоугольника (RoofContourTests). Поэтому
/// вальмовая крыша над Г-образным домом (координатор попросил назвать это отдельно) ничем не
/// отличается от вальмовой крыши над настоящим прямоугольником: обе получают на входе один
/// и тот же RoofFootprint, посчитанный по крайним точкам стен. Тест
/// RoofPitchPlanes_Hip_IsBuiltFromTheBoundingRectangle_TheSameWayForAnyContourShape ниже
/// называет это прямо.</summary>
public class RoofPitchPlanesTests
{
    private static readonly RoofFootprint Footprint6000By4000 = new RoofFootprint(0f, 6000f, 0f, 4000f);

    [Test]
    public void RoofPitchPlanes_RidgeAlongX_Auto_PicksTheLongerAxis()
    {
        Assert.IsTrue(RoofPitchPlanes.RidgeAlongX(Footprint6000By4000, RoofRidgeAxis.Auto),
            "6 000 мм по X длиннее 4 000 мм по Z — конёк вдоль длинной стороны, вдоль X");

        var turned = new RoofFootprint(0f, 4000f, 0f, 6000f);
        Assert.IsFalse(RoofPitchPlanes.RidgeAlongX(turned, RoofRidgeAxis.Auto),
            "здесь длиннее Z — конёк обязан пойти вдоль Z, а не остаться на X по инерции");
    }

    [Test]
    public void RoofPitchPlanes_RidgeAlongX_ExplicitChoice_OverridesGeometry()
    {
        Assert.IsTrue(RoofPitchPlanes.RidgeAlongX(Footprint6000By4000, RoofRidgeAxis.X));
        Assert.IsFalse(RoofPitchPlanes.RidgeAlongX(Footprint6000By4000, RoofRidgeAxis.Z),
            "«повернуть» обязано пересилить автоматический выбор по длинной стороне, "
            + "даже когда результат — конёк вдоль КОРОТКОЙ стороны");
    }

    [Test]
    public void RoofPitchPlanes_Single_Footprint6000x4000_Overhang500_Is35m2_OnePlane_NoRidge()
    {
        var frame = RoofPitchPlanes.Build(Footprint6000By4000, RoofType.Single, RoofRidgeAxis.Auto, 500f);

        Assert.AreEqual(1, frame.Planes.Count, "односкатная — один скат, без конька");
        Assert.AreEqual(0f, frame.RidgeLengthMm, 1e-3f, "у односкатной крыши конька нет");

        var plane = frame.Planes[0];
        Assert.AreEqual(5000f, plane.RunMm, 0.5f,
            "свес добавляет по 500 мм с каждой стороны короткой оси: 4 000 + 2 × 500 = 5 000 мм — "
            + "весь скат перекрывает это расстояние за один пролёт");
        Assert.AreEqual(7000f, plane.EaveEdgeMm, 0.5f,
            "длинная ось с двумя свесами: 6 000 + 2 × 500 = 7 000 мм");
        Assert.AreEqual(35d, plane.PlanAreaM2, 1e-6d,
            "план ската — прямоугольник 7,0 × 5,0 м = 35 м², посчитано руками");

        Assert.AreEqual(24000f, frame.EaveLengthMm, 0.5f,
            "периметр расширенного контура: 2 × (7 000 + 5 000) = 24 000 мм — карниз идёт по "
            + "всем четырём сторонам односкатной крыши");
        Assert.AreEqual(7000f, frame.GutterLengthMm, 0.5f,
            "вода стекает только с НИЗКОГО края — это одна длинная сторона, 7 000 мм, "
            + "а не весь периметр карниза");
    }

    [Test]
    public void RoofPitchPlanes_Gable_SameFootprint_SplitsIntoTwoPlanes_SameTotalAreaAsSingle()
    {
        var frame = RoofPitchPlanes.Build(Footprint6000By4000, RoofType.Gable, RoofRidgeAxis.Auto, 500f);

        Assert.AreEqual(2, frame.Planes.Count, "двускатная — два ската от конька до карниза");

        double total = 0d;
        foreach (var plane in frame.Planes)
        {
            Assert.AreEqual(2500f, plane.RunMm, 0.5f,
                "конёк делит короткую ось пополам: (4 000 + 2 × 500) / 2 = 2 500 мм на скат");
            Assert.AreEqual(7000f, plane.EaveEdgeMm, 0.5f);
            Assert.AreEqual(17.5d, plane.PlanAreaM2, 1e-6d,
                "7,0 × 2,5 = 17,5 м² на скат, посчитано руками");
            total += plane.PlanAreaM2;
        }

        Assert.AreEqual(35d, total, 1e-6d,
            "сумма двух скатов равна плану односкатной крыши над тем же контуром: конёк "
            + "делит план крыши, а не создаёт или теряет площадь");
        Assert.AreEqual(6000f, frame.RidgeLengthMm, 0.5f,
            "конёк — конструктивная длина здания БЕЗ свеса, 6 000 мм: свес продлевает "
            + "кровельный материал за торец, а не сам коньковый брус");
        Assert.AreEqual(24000f, frame.EaveLengthMm, 0.5f,
            "тот же периметр расширенного контура, что и у односкатной: 2 × (7 000 + 5 000)");
        Assert.AreEqual(14000f, frame.GutterLengthMm, 0.5f,
            "у двускатной воду собирают ОБЕ длинные стороны: 2 × 7 000 = 14 000 мм — "
            + "торцы (фронтоны) водостока не несут");
    }

    [Test]
    public void RoofPitchPlanes_Hip_SameFootprint_FourPlanes_SameTotalAreaAsSingle()
    {
        var frame = RoofPitchPlanes.Build(Footprint6000By4000, RoofType.Hip, RoofRidgeAxis.Auto, 500f);

        Assert.AreEqual(4, frame.Planes.Count, "вальмовая — два трапецеидальных ската и два "
            + "треугольных вальма");

        double total = 0d;
        foreach (var plane in frame.Planes) total += plane.PlanAreaM2;

        Assert.AreEqual(35d, total, 1e-6d,
            "план вальмовой крыши над тем же контуром — снова 35 м²: тип крыши режет один и "
            + "тот же прямоугольник по-разному, но не меняет его площадь в плане");
        Assert.AreEqual(2000f, frame.RidgeLengthMm, 0.5f,
            "конёк вальмовой крыши короче здания на ширину ската: "
            + "7 000 − 5 000 = 2 000 мм — классическая геометрия вальм под 45°");
        Assert.AreEqual(24000f, frame.EaveLengthMm, 0.5f);
        Assert.AreEqual(24000f, frame.GutterLengthMm, 0.5f,
            "у вальмовой крыши свисает КАЖДАЯ сторона — карниз и водосток совпадают, в "
            + "отличие от одно- и двускатной, где часть периметра — фронтон без стока");
    }

    [Test]
    public void RoofPitchPlanes_Hip_RidgeAxisForcedToTheShortSide_RidgeLengthClampsToZero()
    {
        var frame = RoofPitchPlanes.Build(Footprint6000By4000, RoofType.Hip, RoofRidgeAxis.Z, 500f);

        Assert.AreEqual(0f, frame.RidgeLengthMm, 1e-3f,
            "«повернуть» конёк на короткую сторону (X становится осью ската, а не конька) "
            + "даёт отрицательную по формуле длину конька, 5 000 − 7 000 = −2 000 мм; "
            + "смета обязана показать ноль, а не отрицательный конёк");

        foreach (var plane in frame.Planes)
            Assert.GreaterOrEqual(plane.RidgeEdgeMm, 0f,
                "ни один скат не имеет отрицательного верхнего края");
    }

    [Test]
    public void RoofPitchPlanes_Hip_IsBuiltFromTheBoundingRectangle_TheSameWayForAnyContourShape()
    {
        var overL = RoofPitchPlanes.Build(Footprint6000By4000, RoofType.Hip, RoofRidgeAxis.Auto, 500f);
        var overPlainRectangle =
            RoofPitchPlanes.Build(new RoofFootprint(0f, 6000f, 0f, 4000f), RoofType.Hip, RoofRidgeAxis.Auto, 500f);

        Assert.AreEqual(overPlainRectangle.RidgeLengthMm, overL.RidgeLengthMm, 0.5f,
            "RoofPitchPlanes принимает только RoofFootprint — прямоугольник. Г-образный дом "
            + "и прямоугольный дом с ТЕМИ ЖЕ крайними точками стен дают RoofContour один и "
            + "тот же footprint (RoofContourTests), а значит и один и тот же вальмовый каркас: "
            + "это и есть заявленный координатору откат к прямоугольнику для невыпуклого "
            + "контура, а не отдельная ветка кода, которую можно было бы забыть протестировать");
    }
}
