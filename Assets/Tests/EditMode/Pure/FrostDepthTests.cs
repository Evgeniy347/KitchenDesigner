using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>Глубина промерзания — не «глубина региона», а НОРМАТИВНАЯ глубина
/// сезонного промерзания d_fn по СП 22.13330.2016, 5.5.3, формула (5.3):
/// d_fn = d0·√Mt, где d0 — свойство ГРУНТА, а Mt — сумма абсолютных значений
/// среднемесячных отрицательных температур, «принимаемых по СП 131.13330».
/// Поэтому в коде нет таблицы «регион → число»: есть климат региона
/// (двенадцать среднемесячных температур, переписанных из таблицы 5.1
/// СП 131.13330.2020) и формула, которая соединяет его с грунтом. Таблица
/// «регион → готовая глубина» была бы третьим числом для той же величины и
/// разъехалась бы с грунтом, который пользователь выбирает отдельным полем.
///
/// Расчётная глубина d_f = k_h·d_fn (5.5.4) здесь НЕ считается: k_h берётся по
/// таблице 5.2 от температуры в помещении и конструкции пола, а приложение про
/// тепловой режим дома не спрашивает. Показывать k_h = 1 молча значило бы выдать
/// расчётную глубину за нормативную.
///
/// Восемь макрорегионов приложения строк в СП 131.13330 не имеют — там пункты
/// наблюдений. Каждому макрорегиону сопоставлена ОДНА опорная станция, и её имя
/// лежит в самой строке таблицы (RegionClimate.ReferenceStation), чтобы подмена
/// станции была видна в диффе, а не пряталась за названием региона.</summary>
public class FrostDepthTests
{
    private static readonly Dictionary<ConstructionRegion, string> ExpectedStations = new()
    {
        [ConstructionRegion.Centre] = "Москва",
        [ConstructionRegion.NorthWest] = "Санкт-Петербург",
        [ConstructionRegion.South] = "Ростов-на-Дону",
        [ConstructionRegion.Volga] = "Самара",
        [ConstructionRegion.Urals] = "Екатеринбург",
        [ConstructionRegion.Siberia] = "Новосибирск",
        [ConstructionRegion.FarEast] = "Хабаровск",
        [ConstructionRegion.FarNorth] = "Воркута",
    };

    private static readonly Dictionary<ConstructionRegion, double> ExpectedNegativeSum = new()
    {
        [ConstructionRegion.Centre] = 22.0d,
        [ConstructionRegion.NorthWest] = 17.6d,
        [ConstructionRegion.South] = 8.0d,
        [ConstructionRegion.Volga] = 35.6d,
        [ConstructionRegion.Urals] = 46.3d,
        [ConstructionRegion.Siberia] = 63.1d,
        [ConstructionRegion.FarEast] = 67.7d,
        [ConstructionRegion.FarNorth] = 99.8d,
    };

    [Test]
    public void RegionClimate_Table_CoversEveryRegion_AndDeclaresNoneTwice()
    {
        var declared = Enum.GetValues(typeof(ConstructionRegion)).Cast<ConstructionRegion>().ToArray();
        var rows = RegionClimate.Table.Select(c => c.Region).ToArray();

        CollectionAssert.AreEquivalent(declared, rows,
            "регион без климатической строки — это дропдаун, который предлагает участок, "
            + "а глубину промерзания для него посчитать нечем; лишняя строка — климат, "
            + "который никому не выдаётся. Значение перечисления и строка таблицы заводятся "
            + "одним изменением");
        Assert.AreEqual(rows.Length, rows.Distinct().Count(),
            "две строки на один регион: TryOf вернёт первую, вторая — мёртвые данные");
    }

    [Test]
    public void RegionClimate_EveryRegion_NamesTheStationItsNumbersWereCopiedFrom()
    {
        foreach (var pair in ExpectedStations)
        {
            Assert.IsTrue(RegionClimate.TryOf(pair.Key, out var climate),
                "нет климата для региона " + pair.Key);
            Assert.AreEqual(pair.Value, climate.ReferenceStation,
                "макрорегион «" + ConstructionRegionTitles.Of(pair.Key) + "» взят по пункту «"
                + pair.Value + "» из " + RegionClimate.Source + ". Смена станции меняет ВСЕ "
                + "числа региона, поэтому она обязана быть видна здесь, а не только в диффе "
                + "двенадцати температур");
        }
    }

    [Test]
    public void RegionClimate_EveryRow_CarriesTwelveMonths_LikeTable51Does()
    {
        foreach (var climate in RegionClimate.Table)
        {
            Assert.IsNotNull(climate.MonthlyMeanDeg, "строка без температур: " + climate.Region);
            Assert.AreEqual(RegionClimate.MonthsPerYear, climate.MonthlyMeanDeg!.Count,
                "строка таблицы 5.1 " + RegionClimate.Source + " — это двенадцать средних "
                + "месячных температур. Одиннадцать означают потерянный месяц, и потерянный "
                + "зимний месяц уменьшает Mt, то есть делает фундамент мельче норматива. "
                + "Регион: " + climate.Region);
        }
    }

    [Test]
    public void RegionClimate_NegativeMonthSum_IsSummedFromTheRow_NotStoredSeparately()
    {
        foreach (var pair in ExpectedNegativeSum)
        {
            Assert.IsTrue(RegionClimate.TryOf(pair.Key, out var climate), "нет климата: " + pair.Key);
            Assert.AreEqual(pair.Value, climate.NegativeMonthSumDeg, 0.01d,
                "Mt — «сумма абсолютных значений среднемесячных отрицательных температур за "
                + "год» (СП 22.13330.2016, 5.5.3). Она пересчитывается из строки "
                + RegionClimate.Source + ", а не хранится вторым числом рядом: два описания "
                + "одной величины расходятся молча. Регион: " + pair.Key
                + ", станция: " + climate.ReferenceStation);
        }
    }

    [Test]
    public void RegionClimate_PositiveMonths_DoNotEnterTheSum()
    {
        Assert.IsTrue(RegionClimate.TryOf(ConstructionRegion.NorthWest, out var spb));

        Assert.AreEqual(0.5f, spb.MonthlyMeanDeg[10], 1e-4f,
            "ноябрь Санкт-Петербурга в таблице 5.1 положительный (+0,5 °C) — строка выбрана "
            + "именно ради этого месяца");
        Assert.AreEqual(17.6d, spb.NegativeMonthSumDeg, 0.01d,
            "положительный месяц в Mt не входит. Если бы суммировались модули всех месяцев, "
            + "вышло бы 17,6 + 0,5 = 18,1, и глубина оказалась бы больше нормативной");
    }

    [Test]
    public void FrostDepth_SoilFactors_AreTheFourNumbersPrintedInSp22()
    {
        Assert.IsTrue(FrostDepth.TrySoilFactorMm(SoilKind.Loam, out float loam));
        Assert.IsTrue(FrostDepth.TrySoilFactorMm(SoilKind.Clay, out float clay));
        Assert.IsTrue(FrostDepth.TrySoilFactorMm(SoilKind.SandyLoam, out float sandyLoam));
        Assert.IsTrue(FrostDepth.TrySoilFactorMm(SoilKind.Sand, out float sand));

        Assert.AreEqual(230f, loam, 1e-3f,
            "«d0 — величина, принимаемая равной для суглинков и глин 0,23 м» — "
            + FrostDepth.FormulaSource);
        Assert.AreEqual(230f, clay, 1e-3f, "глина — та же строка норматива, что и суглинок");
        Assert.AreEqual(280f, sandyLoam, 1e-3f,
            "«супесей, песков мелких и пылеватых - 0,28 м» — " + FrostDepth.FormulaSource);
        Assert.AreEqual(300f, sand, 1e-3f,
            "«песков гравелистых, крупных и средней крупности - 0,30 м» — "
            + FrostDepth.FormulaSource + ". Приложение предлагает один «Песок» без крупности, "
            + "и взята БОЛЬШАЯ из двух песчаных строк: она даёт большую глубину, то есть "
            + "требование к заложению строже");
    }

    [Test]
    public void FrostDepth_EverySoilTheAppOffers_EitherHasAFactor_OrIsRefusedOnPurpose()
    {
        var refused = Enum.GetValues(typeof(SoilKind)).Cast<SoilKind>()
            .Where(s => !FrostDepth.TrySoilFactorMm(s, out _))
            .ToArray();

        CollectionAssert.AreEqual(new[] { SoilKind.Peat }, refused,
            "список d0 в СП 22.13330.2016 (5.5.3) закрыт: суглинки и глины, супеси и пески "
            + "мелкие, пески крупные, крупнообломочные. Торфа в нём нет, поэтому формула (5.3) "
            + "к нему не применяется и поле показывает прочерк. Любой ДРУГОЙ отказ означает "
            + "грунт, который дропдаун предлагает, а норматив посчитать не даёт — это надо "
            + "назвать здесь, а не обнаруживать прочерком в окне");
    }

    [Test]
    public void FrostDepth_UnknownSoil_TakesTheDeepestFactorOfTheOnesOffered()
    {
        Assert.IsTrue(FrostDepth.TrySoilFactorMm(SoilKind.Unknown, out float unknown));

        float deepest = Enum.GetValues(typeof(SoilKind)).Cast<SoilKind>()
            .Where(s => FrostDepth.TrySoilFactorMm(s, out _))
            .Max(s => { FrostDepth.TrySoilFactorMm(s, out float f); return f; });

        Assert.AreEqual(deepest, unknown, 1e-3f,
            "«Неизвестно» — худший случай, как и для остальных правил грунта: берётся "
            + "наибольшее d0 из тех, что приложение вообще предлагает, и глубина выходит "
            + "наибольшей. Взять наименьшее значило бы выдать самое мягкое требование к "
            + "заложению там, где про участок ничего не известно");
    }

    [Test]
    public void FrostDepth_ForMoscowLoam_IsTheFormulaOfSp22_ReDerivedHere()
    {
        Assert.IsTrue(FrostDepth.TryNormativeMm(ConstructionRegion.Centre, SoilKind.Loam,
            out float depthMm));

        double expected = 230d * Math.Sqrt(22.0d);

        Assert.AreEqual(expected, depthMm, 0.5d,
            "d_fn = d0·√Mt = 0,23·√22,0 = 1,079 м. Mt = 22,0 — сумма отрицательных месяцев "
            + "Москвы по " + RegionClimate.Source + " (-7,8 -6,9 -1,3 -0,8 -5,2). Справочное "
            + "«для Москвы 1,4 м» считано по климату СНиП 23-01-99*, где зима холоднее; "
            + "здесь число выводится из ДЕЙСТВУЮЩЕЙ редакции климатологии, и расхождение с "
            + "цифрой из старых справочников — ожидаемое, а не ошибка");
    }

    [Test]
    public void FrostDepth_TheSameRegion_IsDeeperOnSandThanOnClay()
    {
        Assert.IsTrue(FrostDepth.TryNormativeMm(ConstructionRegion.Urals, SoilKind.Clay,
            out float onClay));
        Assert.IsTrue(FrostDepth.TryNormativeMm(ConstructionRegion.Urals, SoilKind.Sand,
            out float onSand));

        Assert.Greater(onSand, onClay,
            "глубина промерзания зависит от ГРУНТА, а не только от региона: это и есть "
            + "причина, по которой поле читает оба дропдауна. Если бы в коде лежала таблица "
            + "«регион → число», эти два значения совпали бы");
    }

    [Test]
    public void FrostDepth_ColderRegion_IsAlwaysDeeper_ForTheSameSoil()
    {
        var byRegion = RegionClimate.Table
            .Select(c =>
            {
                Assert.IsTrue(FrostDepth.TryNormativeMm(c.Region, SoilKind.Loam, out float mm),
                    "суглинок — самый мелкий случай, для него формула обязана работать "
                    + "во всех восьми регионах: " + c.Region);
                return new { c.Region, c.NegativeMonthSumDeg, DepthMm = mm };
            })
            .OrderBy(r => r.NegativeMonthSumDeg)
            .ToArray();

        for (int i = 1; i < byRegion.Length; i++)
            Assert.Greater(byRegion[i].DepthMm, byRegion[i - 1].DepthMm,
                "√Mt монотонна: регион с большей суммой морозов не может промерзать мельче. "
                + "Нарушение здесь — перепутанные строки таблицы 5.1, а не свойство формулы: "
                + byRegion[i].Region + " против " + byRegion[i - 1].Region);
    }

    [Test]
    public void FrostDepth_PeatHasNoDepth_BecauseSp22DoesNotGiveItAFactor()
    {
        Assert.IsFalse(FrostDepth.TryNormativeMm(ConstructionRegion.Centre, SoilKind.Peat,
            out float depthMm),
            "для торфа d0 в " + FrostDepth.FormulaSource + " не назван. Подставить сюда "
            + "чужое число (например, глиняное) — выдумать инженерную константу");
        Assert.AreEqual(0f, depthMm, 1e-4f, "отказ не оставляет в out мусорного числа");
    }

    [Test]
    public void FrostDepth_DeeperThanTwoAndAHalfMetres_IsRefused_WhereTheFormulaStops()
    {
        Assert.IsTrue(FrostDepth.TryNormativeMm(ConstructionRegion.FarNorth, SoilKind.Clay,
            out float onClay),
            "Воркута на глине: 0,23·√99,8 = 2,30 м — формула (5.3) ещё применима");
        Assert.Less(onClay, FrostDepth.ThermalCalculationAboveMm);

        Assert.IsFalse(FrostDepth.TryNormativeMm(ConstructionRegion.FarNorth, SoilKind.Sand,
            out _),
            "та же Воркута на песке: 0,30·√99,8 = 3,00 м. «Для районов, где глубина "
            + "промерзания не превышает 2,5 м, ее нормативное значение следует вычислять по "
            + "формуле» (" + FrostDepth.FormulaSource + "); глубже — теплотехнический расчёт "
            + "по " + FrostDepth.ThermalCalculationSource + ", которого приложение не делает. "
            + "Продолжить формулу за её границей означало бы показать число, которого "
            + "норматив не даёт");
    }

    [Test]
    public void FrostDepth_AnUnknownRegionValue_IsRefused_RatherThanSilentlyTakingTheFirstRow()
    {
        Assert.IsFalse(FrostDepth.TryNormativeMm((ConstructionRegion)99, SoilKind.Loam, out _),
            "регион вне перечисления приходит из сохранения чужой версии. Молчаливый откат "
            + "к первой строке таблицы показал бы московскую глубину для неизвестного "
            + "участка — тихий подлог вместо прочерка");
    }
}
