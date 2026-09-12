using System;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;
using KitchenDesigner.Core.UI;

/// <summary>
/// Подсказка «i» поля «Глубина промерзания» — единственное место, где приложение
/// ОБЪЯСНЯЕТ своё число. Три вещи должны быть в ней видны, и каждая появилась потому,
/// что без неё число выглядит подлогом:
///
/// 1. Источник климата. Поле считает по СП 22.13330, но температуры берутся из
///    СП 131.13330.2020, и по нему Москва даёт 1079 мм, а не привычные из справочников
///    ≈1,4 м (те посчитаны по отменённому СНиП 23-01-99*, где зима холоднее). Приложение
///    называет источник — значит обязано назвать и расхождение, иначе человек, сверившись
///    со справочником, решит, что программа врёт.
/// 2. Опорная станция. Восьми макрорегионам приложения в СП соответствуют не строки, а
///    пункты наблюдений: «Крайний Север» — это Воркута, а не Норильск и не Салехард.
///    Станция подразумевалась в коде и была невидима в окне; теперь она названа для
///    КАЖДОГО региона.
/// 3. Причина отсутствия числа. Прочерк без причины неотличим от ошибки программы.
///
/// Текст собирается из базового предложения в <c>HintText.All</c> и строк, зависящих от
/// выбранных региона и грунта, — см. docs/UI-GUIDELINES.md §13, «Подсказка, зависящая от
/// состояния». Поэтому базовое предложение здесь не переписывается: тест требует, чтобы
/// собранный текст НАЧИНАЛСЯ с него, и второй копии словарной строки не заводит.
/// </summary>
public class FrostDepthHintTests
{
    private static ConstructionRegion[] AllRegions =>
        Enum.GetValues(typeof(ConstructionRegion)).Cast<ConstructionRegion>().ToArray();

    [Test]
    public void TheHint_StartsWithTheDictionarySentence_SoThereIsNoSecondCopyOfIt()
    {
        var baseText = HintText.Of(FrostDepthHint.Key);

        foreach (var region in AllRegions)
            Assert.That(FrostDepthHint.For(region, SoilKind.Loam), Does.StartWith(baseText),
                "динамические строки ДОПИСЫВАЮТСЯ к словарной, а не заменяют её: иначе текст "
                + "подсказки окажется в двух местах и разъедется — " + region);
    }

    [Test]
    public void TheHint_NamesTheClimateSource_AndTheHandbookDisagreement()
    {
        var hint = FrostDepthHint.For(ConstructionRegion.Centre, SoilKind.Clay);

        Assert.That(hint, Does.Contain("СП 131.13330.2020"),
            "температуры взяты не из СП 22.13330, а из СП 131.13330.2020 — источник числа "
            + "обязан быть назван там же, где число");
        Assert.That(hint, Does.Contain("СНиП 23-01-99*"),
            "справочные 1,4 м посчитаны по отменённому СНиП 23-01-99*; не назвав его, "
            + "приложение оставляет расхождение со справочником необъяснённым");
        Assert.That(hint, Does.Contain("≈1,4 м"),
            "названо должно быть само расхождение, а не только его причина: человек сверяет "
            + "с числом из справочника, а не с номером норматива");
    }

    [Test]
    public void TheClimateSourceLine_IsInEveryRegionsHint()
    {
        foreach (var region in AllRegions)
            Assert.That(FrostDepthHint.For(region, SoilKind.Clay),
                Does.Contain(FrostDepthHint.ClimateSourceLine),
                "источник климата один на всю таблицу — регион " + region + " не исключение");
    }

    [Test]
    public void EveryRegion_NamesItsReferenceStation_InTheHint()
    {
        foreach (var climate in RegionClimate.Table)
        {
            var hint = FrostDepthHint.For(climate.Region, SoilKind.Clay);

            Assert.That(climate.ReferenceStation, Is.Not.Empty,
                "регион без опорной станции нечего показывать — " + climate.Region);
            Assert.That(hint, Does.Contain(FrostDepthHint.StationPrefix + climate.ReferenceStation),
                "станция, по которой считан регион, обязана быть ВИДНА, а не подразумеваться: "
                + climate.Region + " считается по " + climate.ReferenceStation);
        }
    }

    [Test]
    public void TheHintOfOneRegion_DoesNotNameTheStationOfAnother()
    {
        Assert.That(FrostDepthHint.For(ConstructionRegion.Centre, SoilKind.Clay),
            Does.Not.Contain("Воркута"),
            "иначе проверка выше зеленела бы на подсказке, перечисляющей все восемь станций, "
            + "и не спрашивала бы ничего про ВЫБРАННЫЙ регион");
        Assert.That(FrostDepthHint.For(ConstructionRegion.FarNorth, SoilKind.Clay),
            Does.Not.Contain("Москва"));
    }

    [Test]
    public void PeatHint_NamesTheMissingFactor_AsTheReasonForTheDash()
    {
        var hint = FrostDepthHint.For(ConstructionRegion.Centre, SoilKind.Peat);

        Assert.That(hint, Does.Contain("СП 22.13330 не даёт d0 для торфа"),
            "прочерк без причины неотличим от сломанной программы. Подставить сюда чужое "
            + "число с оговоркой было бы хуже прочерка: выдуманная инженерная константа");
        Assert.That(FrostDepth.Read(ConstructionRegion.Centre, SoilKind.Peat).Value,
            Is.EqualTo(FrostDepth.UnknownValue), "в самом поле остаётся прочерк");
    }

    [Test]
    public void ASoilTheNormCovers_GetsNoReasonLine_BecauseThereIsNothingToExplain()
    {
        var hint = FrostDepthHint.For(ConstructionRegion.Centre, SoilKind.Clay);

        Assert.That(hint, Does.Not.Contain("Прочерк:"),
            "объяснение отсутствия числа в подсказке, где число есть, — шум; без этой "
            + "встречной проверки тест на торф зеленел бы и на безусловной строке");
        Assert.That(hint, Does.Not.Contain("теплотехнический расчёт"));
    }

    [Test]
    public void VorkutaOnSand_NamesTheThermalCalculation_WhereTheFormulaStops()
    {
        var hint = FrostDepthHint.For(ConstructionRegion.FarNorth, SoilKind.Sand);

        Assert.That(hint, Does.Contain("теплотехнический расчёт по СП 25.13330"),
            "0,30·√99,8 = 3,00 м — за границей применимости формулы (5.3). Норматив требует "
            + "другого расчёта, которого приложение не делает, и это надо назвать");
        Assert.That(hint, Does.Contain("Воркута"),
            "станция остаётся названной и там, где числа нет");
    }

    [Test]
    public void VorkutaOnClay_StillGetsANumber_SoTheSandCaseIsAboutDepthNotAboutTheRegion()
    {
        var hint = FrostDepthHint.For(ConstructionRegion.FarNorth, SoilKind.Clay);

        Assert.That(hint, Does.Not.Contain("теплотехнический расчёт"),
            "0,23·√99,8 = 2,30 м — формула ещё применима. Если бы оговорка стояла на "
            + "регионе, а не на глубине, этот тест был бы красным");
        Assert.That(FrostDepth.Read(ConstructionRegion.FarNorth, SoilKind.Clay).HasNumber,
            Is.True);
    }

    [Test]
    public void EveryRegionAndSoil_ProducesAHint_WithoutThrowing()
    {
        foreach (var region in AllRegions)
            foreach (var soil in Enum.GetValues(typeof(SoilKind)).Cast<SoilKind>())
                Assert.That(FrostDepthHint.For(region, soil), Is.Not.Empty,
                    "подсказка строится при построении панели: исключение здесь отняло бы у "
                    + "человека всё окно настроек, а не одну строку — " + region + "/" + soil);
    }
}
