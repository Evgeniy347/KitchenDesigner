using NUnit.Framework;
using KitchenDesigner.Core.Construction;

/// <summary>docs/NORMATIVE-DEFAULTS.md §3 "Забор из профлиста": сечение столба, глубина ямы
/// и марка профлиста каждая цитирует свой документ (coordinator instruction 2026-09-25 —
/// каждая константа с ссылкой на документ, conventions/COMMENTS.md исключение для
/// нормативной константы). Шаг столбов (2500 мм) в этот список НЕ входит — это принятый
/// в проекте дефолт поля (`todo_evolution.md` §3.3), а не норматив, поэтому без ссылки,
/// как и диаметр/шаг арматуры у FoundationRebarDefaults.</summary>
public class FenceDefaultsTests
{
    [Test]
    [Category("NormativeUnverified")]
    public void PostSectionMm_Is60_TheStandardSquareTubeSize()
    {
        Assert.AreEqual(60, FenceDefaults.PostSectionMm,
            "60x60 — ходовое сечение стальной профильной трубы по ряду ГОСТ 8639-82; "
            + "пункт не сверен исполнителем дословно, см. NormativeUnverified");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void PitDepthMm_Is1200_TiedToFrostProtectionPrinciple()
    {
        Assert.AreEqual(1200, FenceDefaults.PitDepthMm,
            "1200 мм — низ диапазона практики (1,2-1,5 м), увязанного с принципом заглубления "
            + "ниже глубины промерзания СП 22.13330.2016; точное число — дефолт поля, не "
            + "цитата пункта");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void SheetMark_IsС8_ARealGost24045Marking()
    {
        Assert.AreEqual(FenceSheetMark.C8, FenceDefaults.SheetMark,
            "С8 — маркировка профлиста по ряду ГОСТ 24045-2016; точные размеры листа "
            + "(1200/1150/8 мм) не сверены исполнителем дословно, см. NormativeUnverified");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void SheetWorkingWidthMm_Is1150_TheGost24045WorkingWidthForС8()
    {
        Assert.AreEqual(1150, FenceDefaults.SheetWorkingWidthMm,
            "1150 мм — рабочая ширина листа С8 по ряду ГОСТ 24045-2016 (общая ширина 1200 мм "
            + "с нахлёстом); число не сверено исполнителем дословно, см. NormativeUnverified");
    }

    [Test]
    public void PostStepMm_Is2500_AnOrdinaryFieldDefault_NotFromANormativeClause()
    {
        Assert.AreEqual(2500, FenceDefaults.PostStepMm,
            "2500 мм — середина ходового диапазона 2000-3000 мм, дефолт поля "
            + "(todo_evolution.md §3.3), как шаг арматуры у FoundationRebarDefaults — "
            + "без претензии на цитату норматива");
    }

    /// <summary>test-results/review-construction.md #11: FenceElement.GetSpecItems всегда
    /// передавал FenceDefaults.SheetWorkingWidthMm (1150, рабочую ширину С8) в FenceSpecItems,
    /// какая бы марка ни была выбрана — марка меняла только надпись в ведомости, а не число
    /// листов. НС35 и С20 у́же С8 (docs/NORMATIVE-DEFAULTS.md §3), поэтому ведомость
    /// недозаказывала материал для более узкого листа.</summary>
    [Test]
    [Category("NormativeUnverified")]
    public void SheetWorkingWidthMmOf_C8_ReturnsTheDefaultWidth()
    {
        Assert.AreEqual(FenceDefaults.SheetWorkingWidthMm,
            FenceDefaults.SheetWorkingWidthMmOf(FenceSheetMark.C8));
    }

    [Test]
    [Category("NormativeUnverified")]
    public void SheetWorkingWidthMmOf_HC35_Is1000_NotTheDefaultС8Width()
    {
        Assert.AreEqual(1000, FenceDefaults.SheetWorkingWidthMmOf(FenceSheetMark.HC35),
            "рабочая ширина НС35 по ГОСТ 24045-2016 — 1000 мм, у́же С8 (1150 мм); "
            + "ceil(10000/1000)=10 листов, а не ceil(10000/1150)=9");
    }

    [Test]
    [Category("NormativeUnverified")]
    public void SheetWorkingWidthMmOf_C20_Is1000_TheConservativeLowerBoundOfTheDisputedRange()
    {
        Assert.AreEqual(1000, FenceDefaults.SheetWorkingWidthMmOf(FenceSheetMark.C20),
            "источники по С20 расходятся (рабочая ширина 1000-1100 мм, NORMATIVE-DEFAULTS.md "
            + "§3) — взят нижний край диапазона, чтобы ведомость скорее переоценила число "
            + "листов, чем недозаказала материал");
    }
}
