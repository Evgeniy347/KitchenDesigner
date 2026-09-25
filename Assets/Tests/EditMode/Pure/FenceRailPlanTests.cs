using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>docs/NORMATIVE-DEFAULTS.md §3: число прожилин зависит от высоты забора — 2 ниже
/// 2 м, 3 от 2 м и выше (граница решена явно: coordinator instruction 2026-09-25, «3 rails
/// when the height is ≥2 m»). Источника-норматива для самого порога в собранных материалах
/// нет — это практика забора из профлиста, не пункт СП/ГОСТ, поэтому константа без
/// комментария-ссылки.</summary>
public class FenceRailPlanTests
{
    [Test]
    public void RailCountFor_JustBelow2M_IsTwo()
    {
        Assert.AreEqual(2, FenceRailPlan.RailCountFor(1999f),
            "1999 мм — ниже порога, две прожилины");
    }

    [Test]
    public void RailCountFor_Exactly2M_IsThree()
    {
        Assert.AreEqual(3, FenceRailPlan.RailCountFor(2000f),
            "ровно 2000 мм — порог включает верхнюю границу, три прожилины. Это "
            + "противоположный вход к предыдущему тесту: соседние значения по разные стороны "
            + "от границы обязаны различаться, иначе правило не проверяет саму границу");
    }

    [Test]
    public void RailCountFor_Above2M_IsThree()
    {
        Assert.AreEqual(3, FenceRailPlan.RailCountFor(2500f));
    }

    [Test]
    public void RailCountFor_Zero_IsTwo_NotACrash()
    {
        Assert.AreEqual(2, FenceRailPlan.RailCountFor(0f));
    }
}
