using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;

/// <summary>Арифметика левой палитры: стек «заголовок → пункты → следующий
/// заголовок» и ВЫСОТА СОДЕРЖИМОГО, по которой панель размеряет область
/// прокрутки.
///
/// Раскладка и высота обязаны считаться ОДНОЙ функцией. Пока они были разными
/// (стек в RelayoutFull, высота — зашитая константа 960), каталог перерос
/// корень и нижние пункты стали недостижимы: 34 пункта в 7 группах занимают
/// 1280 px. Никакой тест этого не увидел, потому что позиции считались верно —
/// врала только высота, которую никто не считал.
///
/// Отсюда форма проверок: высоту здесь ВСЕГДА пересчитывают независимо от
/// Place — суммой видимых строк, — и сравнивают с тем, что Place вернул.
/// Вернуть постоянное число вместо суммы значит покраснеть.</summary>
public class SidebarLayoutTests
{
    private static readonly List<SidebarRow> Rows = new List<SidebarRow>();

    private static SidebarGroupMetrics Group(bool open, params float[] heights)
        => new SidebarGroupMetrics(open, heights);

    private static float LowestRowBottom(List<SidebarRow> rows)
    {
        float lowest = 0f;
        foreach (var row in rows)
            if (row.Visible && row.Bottom > lowest)
                lowest = row.Bottom;
        return lowest;
    }

    [Test]
    public void Place_EmptyCatalog_IsJustTheTopPadding()
    {
        float h = SidebarLayout.Place(new List<SidebarGroupMetrics>(), Rows);

        Assert.AreEqual(SidebarLayout.Pad, h);
        Assert.IsEmpty(Rows);
    }

    [Test]
    public void Place_StacksHeaderThenItsItems()
    {
        SidebarLayout.Place(new List<SidebarGroupMetrics> { Group(true, 26f, 26f) }, Rows);

        Assert.AreEqual(3, Rows.Count);
        Assert.IsTrue(Rows[0].IsHeader);
        Assert.AreEqual(-SidebarLayout.Pad, Rows[0].Position.y);
        Assert.AreEqual(SidebarLayout.Pad, Rows[0].Position.x);

        Assert.AreEqual(-(SidebarLayout.Pad + SidebarLayout.HeaderH + SidebarLayout.HeaderGap),
            Rows[1].Position.y);
        Assert.AreEqual(SidebarLayout.Pad + SidebarLayout.ItemIndent, Rows[1].Position.x,
            "пункт отступает от заголовка вправо — по этому уступу видно вложенность");
        Assert.AreEqual(Rows[1].Position.y - (26f + SidebarLayout.ItemGap), Rows[2].Position.y);
    }

    [Test]
    public void Place_StepFollowsTheActualRowHeight_NotAConstant()
    {
        SidebarLayout.Place(new List<SidebarGroupMetrics> { Group(true, 42f, 26f) }, Rows);

        float step = Rows[1].Position.y - Rows[2].Position.y;

        Assert.AreEqual(42f + SidebarLayout.ItemGap, step,
            "с постоянным шагом двухстрочный пункт накрыл бы следующий");
    }

    [Test]
    public void Place_ClosedGroup_HidesItsItemsAndCollapsesToTheHeaderAlone()
    {
        var groups = new List<SidebarGroupMetrics> { Group(false, 26f, 26f), Group(true, 26f) };

        float h = SidebarLayout.Place(groups, Rows);

        Assert.IsFalse(Rows[1].Visible);
        Assert.IsFalse(Rows[2].Visible);
        Assert.AreEqual(-(SidebarLayout.Pad + SidebarLayout.HeaderH + SidebarLayout.HeaderGap),
            Rows[3].Position.y, "свёрнутая группа не оставляет за собой пустоты");
        Assert.AreEqual(SidebarLayout.Pad + 2f * (SidebarLayout.HeaderH + SidebarLayout.HeaderGap)
            + 26f + SidebarLayout.ItemGap, h);
    }

    [Test]
    public void Place_ContentHeight_IsTheSumOfEveryVisibleRow()
    {
        var groups = new List<SidebarGroupMetrics>
        {
            Group(true, 26f, 42f, 26f),
            Group(false, 26f, 26f),
            Group(true, 26f, 26f, 42f, 26f),
        };

        float h = SidebarLayout.Place(groups, Rows);

        float expected = SidebarLayout.Pad;
        foreach (var g in groups)
        {
            expected += SidebarLayout.HeaderH + SidebarLayout.HeaderGap;
            if (!g.Open) continue;
            for (int i = 0; i < g.ItemCount; i++)
                expected += g.ItemHeights![i] + SidebarLayout.ItemGap;
        }

        Assert.AreEqual(expected, h,
            "высота содержимого — сумма видимых строк, и только она даёт прокрутке "
            + "дотянуться до последнего пункта");
    }

    [Test]
    public void Place_ContentHeight_ReachesBelowTheLowestRow()
    {
        var groups = new List<SidebarGroupMetrics>();
        for (int g = 0; g < 7; g++) groups.Add(Group(true, 26f, 26f, 42f, 26f, 26f));

        float h = SidebarLayout.Place(groups, Rows);

        Assert.GreaterOrEqual(h, LowestRowBottom(Rows),
            "нижний край последнего пункта обязан быть внутри содержимого: "
            + "то, что вылезло за него, прокруткой не достать");
    }

    [Test]
    public void ItemHeight_OneLine_IsTheSingleLineMinimum()
    {
        Assert.AreEqual(SidebarLayout.SingleLineItemH, SidebarLayout.ItemHeight(1, 14, 1.2f));
    }

    [Test]
    public void ItemHeight_TwoLines_GrowsByAWholeLine()
    {
        Assert.AreEqual(42f, SidebarLayout.ItemHeight(2, 14, 1.2f));
        Assert.Greater(SidebarLayout.ItemHeight(2, 14, 1.2f), SidebarLayout.ItemHeight(1, 14, 1.2f));
    }

    [Test]
    public void MiniStrip_StacksGroupButtonsAndReportsTheirTotalHeight()
    {
        Assert.AreEqual(-SidebarLayout.MiniPad, SidebarLayout.MiniItemY(0));
        Assert.AreEqual(SidebarLayout.MiniItemY(0) - (SidebarLayout.MiniButtonH + SidebarLayout.MiniGap),
            SidebarLayout.MiniItemY(1));
        Assert.AreEqual(SidebarLayout.MiniPad, SidebarLayout.MiniContentHeight(0));
        Assert.AreEqual(SidebarLayout.MiniPad + 7f * (SidebarLayout.MiniButtonH + SidebarLayout.MiniGap),
            SidebarLayout.MiniContentHeight(7),
            "полоса растёт с каждой группой — её высота тоже обязана быть считаемой");
    }
}
