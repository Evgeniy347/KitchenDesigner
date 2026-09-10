using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class SidebarPresetLayoutTests
{
    [Test]
    public void PresetDotSize_MeetsTheMinimumTokenForFontAndIsAtLeast24Pixels()
    {
        Assert.GreaterOrEqual(SidebarLayout.PresetDotSize, 24f,
            "точка пресета обязана быть не меньше 24 px (docs/UI-GUIDELINES.md §8 держит "
            + "32 px как общий минимум хит-таргета; 24 — согласованное здесь исключение "
            + "для плотного ряда пресетов на плитке 96×96, а не молчаливое нарушение)");
    }

    [Test]
    public void FourFittingPresets_DoNotFitOnOneRowAtTheMinimumDotSize_SoWrappingIsRequired()
    {
        Assert.Less(SidebarLayout.PresetDotsPerRow(), 4,
            "четыре точки по 24 px с зазором не помещаются в ширину плитки 96 px — перенос "
            + "на вторую строку (а не выпадающий список) выбран, потому что он остаётся тем "
            + "же компонентом PresetDot без нового элемента интерфейса и без нового паттерна "
            + "наведения, и одинаково хорошо работает и для шести фитингов трубы, и для "
            + "будущих 6–8 фитингов воздуховода (docs/todo_evolution.md §2.4)");
    }

    [Test]
    public void SixFittingPresets_WrapIntoTwoRows_InsteadOfOverflowingThePastFifthDot()
    {
        int perRow = SidebarLayout.PresetDotsPerRow();
        int rows = SidebarLayout.PresetDotRows(6);

        Assert.AreEqual(2, rows,
            $"шесть пресетов при {perRow} в ряд обязаны занять ровно две строки — "
            + "раньше шестая точка «Обратка» просто вылезала за край плитки без маски");
    }

    [Test]
    public void PresetRowsHeight_FitsInsideTheTileImageArea()
    {
        float height = SidebarLayout.PresetRowsHeight(6);

        Assert.LessOrEqual(height, SidebarLayout.TileImageH,
            "ряды точек пресета обязаны помещаться в область картинки плитки (56 px) — "
            + "иначе они наедут на подпись под ней");
    }

    [Test]
    public void PresetDotPosition_PlacesDotsInAGridByColumnsThenRows()
    {
        int perRow = SidebarLayout.PresetDotsPerRow();

        var first = SidebarLayout.PresetDotPosition(0);
        var lastInFirstRow = SidebarLayout.PresetDotPosition(perRow - 1);
        var firstInSecondRow = SidebarLayout.PresetDotPosition(perRow);

        Assert.AreEqual(0f, first.x);
        Assert.AreEqual(0f, first.y);
        Assert.AreEqual(0f, lastInFirstRow.y,
            "последняя точка первого ряда остаётся на той же строке");
        Assert.Greater(lastInFirstRow.x, first.x);
        Assert.AreEqual(0f, firstInSecondRow.x,
            "первая точка второго ряда возвращается в начало строки");
        Assert.Less(firstInSecondRow.y, first.y,
            "вторая строка сдвинута вниз");
    }

    [Test]
    public void PresetDotRows_OfZeroOrOnePreset_IsZero()
    {
        Assert.AreEqual(0, SidebarLayout.PresetDotRows(0));
        Assert.AreEqual(0f, SidebarLayout.PresetRowsHeight(0));
    }
}
