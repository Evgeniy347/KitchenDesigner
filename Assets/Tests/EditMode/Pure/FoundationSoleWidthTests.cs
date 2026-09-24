using System;
using System.Linq;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;

/// <summary>FND-02 («ширина подошвы меньше минимума для грунта и толщины стены»,
/// docs/todo_evolution.md §3.3) нужен минимум ширины подошвы ленты. У СП 22.13330.2016
/// НЕТ готовой таблицы «грунт → ширина подошвы»: в норме ширина — результат расчёта
/// несущей способности (нагрузка на погонный метр стены делится на расчётное сопротивление
/// грунта R0 из приложения Б), а нагрузку (этажность, снеговой район, кровля) это
/// приложение сегодня не считает нигде.
///
/// Поэтому здесь не таблица, вычитанная из конкретного пункта, а КОНСТРУКТИВНЫЙ минимум:
/// ширина подошвы не меньше толщины стены плюс запас на грунт с каждой стороны — грунт
/// хуже, запас больше, тем же порядком, что и у FrostDepth («неизвестно» = худший случай
/// из предложенных). Значения запаса (100/150/200 мм) исполнитель НЕ смог сверить с
/// конкретным пунктом СП 22.13330 — это практический минимум напуска, который встречается
/// в справочниках по малоэтажному строительству, а не цитата норматива. Поэтому весь класс
/// помечен [Category("NormativeUnverified")]: по правилу из §3.6 («если исполнителю
/// недоступен текст СП — он пишет таблицу с пунктом, который нашёл, и помечает тест»)
/// менеджер сверяет цифры сам, пользователя не спрашивать.</summary>
[Category("NormativeUnverified")]
public class FoundationSoleWidthTests
{
    [Test]
    public void FoundationSoleWidth_Sp22_13330_PrilozhenieB_MarginsGrowWithWorseSoil()
    {
        Assert.IsTrue(FoundationSoleWidth.TryMarginMm(SoilKind.Sand, out float sand));
        Assert.IsTrue(FoundationSoleWidth.TryMarginMm(SoilKind.SandyLoam, out float sandyLoam));
        Assert.IsTrue(FoundationSoleWidth.TryMarginMm(SoilKind.Loam, out float loam));
        Assert.IsTrue(FoundationSoleWidth.TryMarginMm(SoilKind.Clay, out float clay));

        Assert.AreEqual(100f, sand, 1e-3f,
            "песок — лучшая несущая способность из четырёх грунтов приложения, поэтому "
            + "наименьший запас; число не цитата пункта СП, см. FoundationSoleWidth.Source");
        Assert.AreEqual(150f, sandyLoam, 1e-3f, "супесь — средний случай между песком и глиной");
        Assert.AreEqual(200f, loam, 1e-3f,
            "суглинок — самый большой запас из четырёх, та же строка, что и глина, как и у "
            + "FrostDepth (там суглинок и глина делят один d0)");
        Assert.AreEqual(200f, clay, 1e-3f, "глина — та же строка, что и суглинок");

        Assert.LessOrEqual(sand, sandyLoam, "хуже грунт — не меньше запас, монотонность ряда");
        Assert.LessOrEqual(sandyLoam, loam, "хуже грунт — не меньше запас, монотонность ряда");
    }

    [Test]
    public void FoundationSoleWidth_UnknownSoil_TakesTheWidestMarginOfTheOnesOffered()
    {
        Assert.IsTrue(FoundationSoleWidth.TryMarginMm(SoilKind.Unknown, out float unknown));

        float widest = Enum.GetValues(typeof(SoilKind)).Cast<SoilKind>()
            .Where(s => FoundationSoleWidth.TryMarginMm(s, out _))
            .Max(s => { FoundationSoleWidth.TryMarginMm(s, out float m); return m; });

        Assert.AreEqual(widest, unknown, 1e-3f,
            "«неизвестно» — худший случай, как и грунт промерзания в FrostDepth: берётся "
            + "наибольший запас из тех, что приложение вообще предлагает. Взять меньший "
            + "значило бы выдать самое мягкое требование там, где про участок ничего не "
            + "известно");
    }

    [Test]
    public void FoundationSoleWidth_Peat_IsRefused_BecauseAStripFootingIsNotUsedOnPeat()
    {
        bool hasMargin = FoundationSoleWidth.TryMarginMm(SoilKind.Peat, out float marginMm);

        Assert.IsFalse(hasMargin,
            "на торфе ленточный фундамент не применяют без замены грунта или свайного поля — "
            + "выдумать для него ширину подошвы значило бы одобрить решение, которое сам "
            + "норматив для торфа не рассматривает как ленту (тот же отказ, что и у "
            + "FrostDepth.TrySoilFactorMm для торфа)");
        Assert.AreEqual(0f, marginMm, 1e-4f, "отказ не оставляет в out мусорного числа");

        Assert.IsFalse(FoundationSoleWidth.TryMinimumWidthMm(SoilKind.Peat, 250f, out float minWidthMm),
            "минимальная ширина наследует отказ запаса — не считать вширь то, для чего нет "
            + "запаса");
        Assert.AreEqual(0f, minWidthMm, 1e-4f);
    }

    [Test]
    public void FoundationSoleWidth_BrickWall250_OnLoam_MinimumIs650mm()
    {
        Assert.IsTrue(FoundationSoleWidth.TryMinimumWidthMm(SoilKind.Loam, 250f, out float minWidthMm));

        Assert.AreEqual(650f, minWidthMm, 0.5f,
            "250 (толщина стены) + 2 × 200 (запас на суглинок с каждой стороны) = 650 мм, "
            + "посчитано руками");
    }

    [Test]
    public void FoundationSoleWidth_FrameWall150_OnSand_MinimumIs350mm()
    {
        Assert.IsTrue(FoundationSoleWidth.TryMinimumWidthMm(SoilKind.Sand, 150f, out float minWidthMm));

        Assert.AreEqual(350f, minWidthMm, 0.5f,
            "150 (каркасная стена) + 2 × 100 (запас на песок с каждой стороны) = 350 мм, "
            + "посчитано руками");
    }

    [Test]
    public void FoundationSoleWidth_MinimumWidth_IsNeverNarrowerThanTheWallItself()
    {
        foreach (var soil in Enum.GetValues(typeof(SoilKind)).Cast<SoilKind>())
        {
            if (!FoundationSoleWidth.TryMinimumWidthMm(soil, 400f, out float minWidthMm)) continue;

            Assert.Greater(minWidthMm, 400f,
                "подошва обязана быть шире самой стены — иначе лента не удерживает нагрузку "
                + "по краям стены; грунт: " + soil);
        }
    }
}
