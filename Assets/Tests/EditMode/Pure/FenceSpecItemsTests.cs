using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Ведомость забора — четыре строки (столбы, бетон, профлист, прожилины), все в
/// SpecSections.Structures рядом с фундаментом и плитой перекрытия.</summary>
public class FenceSpecItemsTests
{
    [Test]
    public void Of_10MetreRun_Height2000_ProducesFourLines_EachInStructures()
    {
        var items = FenceSpecItems.Of(10000f, 2000f, FenceDefaults.PostStepMm,
            FenceDefaults.PostSectionMm, FenceDefaults.PitDepthMm, 1150f, "С8").ToList();

        Assert.AreEqual(4, items.Count);
        foreach (var item in items)
            Assert.AreEqual(SpecSections.Structures, item.section);
    }

    [Test]
    public void Of_Posts_CarriesSectionAsMaterial_AndMatchesFenceQuantities()
    {
        var items = FenceSpecItems.Of(10000f, 2000f, 2500f, 60f, 1200f, 1150f, "С8").ToList();
        var posts = items.Single(i => i.name == FenceSpecItems.PostsName);

        Assert.AreEqual("60x60", posts.material);
        Assert.AreEqual(FenceQuantities.PostsPerRun(10000f, 2500f), posts.qty, 0.01f,
            "число столбов обязано совпасть со счётом FenceQuantities — иначе ведомость и "
            + "геометрия разойдутся");
        Assert.AreEqual(SpecUnit.Pieces, posts.unit);
    }

    [Test]
    public void Of_RailCount_FollowsFenceRailPlan_NotAHardcodedTwo()
    {
        var low = FenceSpecItems.Of(10000f, 1500f, 2500f, 60f, 1200f, 1150f, "С8")
            .Single(i => i.name == FenceSpecItems.RailName);
        var tall = FenceSpecItems.Of(10000f, 2000f, 2500f, 60f, 1200f, 1150f, "С8")
            .Single(i => i.name == FenceSpecItems.RailName);

        Assert.AreEqual(10d * FenceRailPlan.RailCountBelowThreshold, low.qty, 0.01f,
            "1500 мм — ниже порога, две прожилины на 10 погонных метров");
        Assert.AreEqual(10d * FenceRailPlan.RailCountAtOrAboveThreshold, tall.qty, 0.01f,
            "2000 мм — на пороге, три прожилины: противоположный вход к предыдущему");
    }

    [Test]
    public void Of_ZeroLength_ProducesNoLinesAtAll()
    {
        var items = FenceSpecItems.Of(0f, 2000f, 2500f, 60f, 1200f, 1150f, "С8");

        Assert.IsEmpty(items, "забор нулевой длины не заказывает ни столбов, ни бетона, ни "
            + "листа, ни прожилин — ведомость обязана промолчать, а не напечатать нули");
    }
}
