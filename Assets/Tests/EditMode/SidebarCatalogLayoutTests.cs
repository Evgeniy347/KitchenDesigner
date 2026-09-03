using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Настоящий каталог, пропущенный через арифметику сайдбара. Пункты
/// здесь не выдуманные: их высоты приходят из имён каталога через
/// <see cref="SidebarUI.ItemHeight"/>, а значит рассинхрон между «сколько строк
/// занимает имя» и «на сколько сдвигается следующая кнопка» виден именно тут,
/// а не в PlayMode на глаз.
///
/// Повод: с приходом «Сантехники», розетки и выключателя каталог вырос до
/// 7 групп и 34 пунктов — 1280 px против 960 px тогдашнего корня. Позиции при
/// этом считались правильно, врала только высота содержимого, и нижние восемь
/// пунктов оказались за пределами панели. Проверка здесь одна и та же с двух
/// сторон: высота содержимого обязана накрывать нижний край последней строки,
/// а строки не имеют права наезжать друг на друга.</summary>
public class SidebarCatalogLayoutTests
{
    private static List<SidebarGroupMetrics> AllGroupsOpen()
    {
        var metrics = new List<SidebarGroupMetrics>();
        foreach (var g in SidebarCatalog.Build())
        {
            var heights = new List<float>();
            foreach (var it in g.items) heights.Add(SidebarUI.ItemHeight(it.name));
            metrics.Add(new SidebarGroupMetrics(true, heights));
        }
        return metrics;
    }

    [Test]
    public void WholeCatalog_ContentHeight_CoversTheLowestItem()
    {
        var rows = new List<SidebarRow>();

        float height = SidebarLayout.Place(AllGroupsOpen(), rows);

        float lowest = 0f;
        foreach (var row in rows)
            if (row.Visible) lowest = Mathf.Max(lowest, row.Bottom);

        Assert.Greater(rows.Count, 0);
        Assert.GreaterOrEqual(height, lowest,
            $"каталог занимает {lowest} px, а содержимому объявлено {height} px — "
            + "разница обрезается и прокруткой не достаётся");
    }

    [Test]
    public void WholeCatalog_RowsFollowOneAnother_WithoutOverlap()
    {
        var rows = new List<SidebarRow>();
        SidebarLayout.Place(AllGroupsOpen(), rows);

        float previousBottom = 0f;
        foreach (var row in rows)
        {
            if (!row.Visible) continue;
            float top = -row.Position.y;
            Assert.GreaterOrEqual(top, previousBottom,
                $"строка группы {row.Group} (пункт {row.Item}) начинается выше конца предыдущей");
            previousBottom = row.Bottom;
        }
    }

    [Test]
    public void EveryCatalogItem_GetsARowOfItsOwn()
    {
        var catalog = SidebarCatalog.Build();
        int expected = catalog.Count;
        foreach (var g in catalog) expected += g.items.Count;

        var rows = new List<SidebarRow>();
        SidebarLayout.Place(AllGroupsOpen(), rows);

        Assert.AreEqual(expected, rows.Count,
            "ни один пункт каталога не имеет права потеряться по дороге в раскладку");
    }
}
