using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Каждое правило ниже проверено парой противоположных входов: один, где лента
/// заведомо в порядке, и один, где она заведомо нарушает то же самое условие, — иначе
/// «зелёный тест» мог бы означать «правило всегда возвращает true» (conventions/TEST-NAMING.md,
/// «Two guards that complement each other need two runs», применено здесь к одному правилу
/// вместо пары).</summary>
public class FoundationRulesTests
{
    [Test]
    public void DepthMeetsFrostRule_Sand_IsAlwaysTrue_RegardlessOfDepth_BecauseSandDoesNotHeave()
    {
        Assert.IsTrue(FoundationRules.DepthMeetsFrostRule(SoilKind.Sand, frostDepthKnown: true,
            frostDepthMm: 1200f, actualDepthMm: 100f),
            "FND-01 — «при пучинистом грунте (всё, кроме песка)»: на песке правило не действует "
            + "даже когда заложение мельче нормативной глубины промерзания");
    }

    [Test]
    public void DepthMeetsFrostRule_ClayDeeperThanFrost_IsTrue_ClayShallowerThanFrost_IsFalse()
    {
        Assert.IsTrue(FoundationRules.DepthMeetsFrostRule(SoilKind.Clay, frostDepthKnown: true,
            frostDepthMm: 1200f, actualDepthMm: 1200f),
            "заложение РОВНО на глубине промерзания — ещё не мельче, граница входит");
        Assert.IsFalse(FoundationRules.DepthMeetsFrostRule(SoilKind.Clay, frostDepthKnown: true,
            frostDepthMm: 1200f, actualDepthMm: 1199f),
            "на 1 мм мельче нормативной глубины промерзания на глине — уже нарушение FND-01");
    }

    [Test]
    public void DepthMeetsFrostRule_UnknownFrostDepth_IsTrue_ThereIsNothingToCompareAgainst()
    {
        Assert.IsTrue(FoundationRules.DepthMeetsFrostRule(SoilKind.Clay, frostDepthKnown: false,
            frostDepthMm: 0f, actualDepthMm: 1f),
            "FrostDepth отказал (например, торф или регион вне таблицы) — правилу не с чем "
            + "сравнивать заложение, и оно не должно придумывать число вместо FrostDepth");
    }

    [Test]
    public void SoleWidthMeetsMinimum_Wall250OnLoam_700mmIsTrue_600mmIsFalse()
    {
        Assert.IsTrue(FoundationRules.SoleWidthMeetsMinimum(SoilKind.Loam, 250f, 700f),
            "минимум для суглинка и стены 250 мм — 650 мм (FoundationSoleWidth); 700 мм шире");
        Assert.IsFalse(FoundationRules.SoleWidthMeetsMinimum(SoilKind.Loam, 250f, 600f),
            "600 мм у́же минимума 650 мм — нарушение FND-02");
    }

    [Test]
    public void SoleWidthMeetsMinimum_Peat_IsAlwaysTrue_ThereIsNoMinimumTableForIt()
    {
        Assert.IsTrue(FoundationRules.SoleWidthMeetsMinimum(SoilKind.Peat, 250f, 1f),
            "FoundationSoleWidth отказывает по торфу (лента на торфе не применяется) — FND-02 "
            + "не может проверить то, для чего нет минимума, и не должен рапортовать нарушение "
            + "там, где сам расчёт отказался работать");
    }

    [Test]
    public void CushionMeetsMinimum_Compacted_100And100_IsTrue_80And100_IsFalse()
    {
        Assert.IsTrue(FoundationRules.CushionMeetsMinimum(compacted: true, sandMm: 100f, gravelMm: 100f),
            "ровно 100 мм при трамбовке — минимум выполнен, граница входит");
        Assert.IsFalse(FoundationRules.CushionMeetsMinimum(compacted: true, sandMm: 80f, gravelMm: 100f),
            "песок 80 мм тоньше минимума 100 мм — нарушение FND-03, даже если щебень в норме");
    }

    [Test]
    public void CushionMeetsMinimum_NotCompacted_ThinLayers_IsTrue_RuleDoesNotApplyUncompacted()
    {
        Assert.IsTrue(FoundationRules.CushionMeetsMinimum(compacted: false, sandMm: 20f, gravelMm: 20f),
            "формулировка FND-03 — «тоньше минимума ... при заданной трамбовке»: без трамбовки "
            + "минимум 100 мм не заявлен нигде в docs/todo_evolution.md §3.3, поэтому правило "
            + "не оценивает этот случай, а не молча одобряет придуманное число");
    }

    [Test]
    public void RebarCoverOk_ExactlyTwoDiameters_IsTrue_OneMillimetreLess_IsFalse()
    {
        Assert.IsTrue(FoundationRules.RebarCoverOk(coverMm: 24f, diameterMm: 12f),
            "защитный слой ровно 2×Ø12 = 24 мм — граница выполнена");
        Assert.IsFalse(FoundationRules.RebarCoverOk(coverMm: 23f, diameterMm: 12f),
            "23 мм меньше 2×Ø12 = 24 мм — нарушение FND-04");
    }

    [Test]
    public void RebarStepOk_EqualToWidth_IsTrue_OneMillimetreWider_IsFalse()
    {
        Assert.IsTrue(FoundationRules.RebarStepOk(stepMm: 600f, widthMm: 600f),
            "шаг ровно равен ширине ленты — граница выполнена");
        Assert.IsFalse(FoundationRules.RebarStepOk(stepMm: 601f, widthMm: 600f),
            "шаг на 1 мм больше ширины ленты — хомуты реже, чем сама лента широкая, "
            + "нарушение FND-04");
    }
}
