using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

public class RunKindDefaultsTests
{
    [Test]
    public void TryParse_AcceptsTheThreeWords_IgnoringCaseAndSpaces_AndNothingElse()
    {
        Assert.IsTrue(RunKindDefaults.TryParse(" Base ", out var kind));
        Assert.AreEqual(RunKind.Base, kind);
        Assert.IsTrue(RunKindDefaults.TryParse("WALL", out kind));
        Assert.AreEqual(RunKind.Wall, kind);
        Assert.IsTrue(RunKindDefaults.TryParse("tall", out kind));
        Assert.AreEqual(RunKind.Tall, kind);
        Assert.IsFalse(RunKindDefaults.TryParse("island", out _));
        Assert.IsFalse(RunKindDefaults.TryParse("", out _));
        Assert.IsFalse(RunKindDefaults.TryParse(null, out _));
    }

    [Test]
    public void Word_RoundTripsEveryKind()
    {
        foreach (RunKind kind in System.Enum.GetValues(typeof(RunKind)))
        {
            Assert.IsTrue(RunKindDefaults.TryParse(RunKindDefaults.Word(kind), out var back));
            Assert.AreEqual(kind, back);
        }
    }

    [Test]
    public void BaseCabinet_Is720By560_AsInTheGuideExampleAndTheDishwasherFacadeNominal()
    {
        Assert.AreEqual(720, RunKindDefaults.BaseHeightMm);
        Assert.AreEqual(560, RunKindDefaults.BaseDepthMm);
        Assert.AreEqual(DishwasherBody.FACADE_NOMINAL_HEIGHT_MM, RunKindDefaults.BaseHeightMm,
            "номинал высоты под столешницей 820 в проекте один — у посудомойки; шкаф рядом не должен расходиться с ней");
        Assert.AreEqual(1400, RunKindDefaults.WallHangsAboveFloorMm, "высота подвеса — из примера place в guide");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void WallAndTallDefaults_AreTypicalPractice_NotACitedStandard()
    {
        Assert.AreEqual(320, RunKindDefaults.WallDepthMm, "типовая глубина навесного 300-350: источник ГОСТ не открыт исполнителем");
        Assert.AreEqual(720, RunKindDefaults.WallHeightMm);
        Assert.AreEqual(2100, RunKindDefaults.TallHeightMm, "типовой пенал 2000-2300: источник ГОСТ не открыт исполнителем");
    }

    [Test]
    public void EveryKind_HasAPositiveHeightAndDepth_AndOnlyTheWallKindHangs()
    {
        foreach (RunKind kind in System.Enum.GetValues(typeof(RunKind)))
        {
            Assert.Greater(RunKindDefaults.HeightMm(kind), 0, kind.ToString());
            Assert.Greater(RunKindDefaults.DepthMm(kind), 0, kind.ToString());
        }
        Assert.AreEqual(0, RunKindDefaults.HangsAboveFloorMm(RunKind.Base));
        Assert.AreEqual(0, RunKindDefaults.HangsAboveFloorMm(RunKind.Tall));
        Assert.Greater(RunKindDefaults.HangsAboveFloorMm(RunKind.Wall), RunKindDefaults.BaseHeightMm,
            "навесной висит выше основания столешницы, иначе он не навесной");
    }
}
