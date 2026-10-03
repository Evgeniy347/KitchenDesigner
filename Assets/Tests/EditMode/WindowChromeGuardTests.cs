using System;
using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Шапка окна — заголовок, крестик и полоса перетаскивания — собиралась руками в каждом окне,
/// и у каждого вышли свои числа: у «Настроек» заголовок стоял на 31 px ниже крестика (в строке
/// «Этажи» он на одной линии), у «Спецификации» на 6 px, у «Инструкций» на 11, а перетаскивать
/// «Настройки» и «Спецификацию» было нельзя вовсе — при создании окон полосу перетаскивания
/// навесили четырём окнам из списка, и эти два в нём не оказались.
///
/// Сторож обходит не список окон, а КРЕСТИКИ: любое окно, у которого есть <c>CloseBtn</c>, обязано
/// иметь заголовок, собранный <see cref="WindowTitle"/> (он ставит центр на ту же линию, что у
/// крестика), и полосу перетаскивания, накрывающую этот заголовок. Новое окно, собранное мимо
/// <see cref="WindowTitle"/>, краснеет здесь, а не на скриншоте у пользователя.
/// </summary>
public class WindowChromeGuardTests
{
    private const float SameRowTolerancePx = 1f;
    private const float ExtraHeightToTryPx = 120f;
    private const int LevelsThatStretchTheWindow = 9;

    private GameObject? _canvasGo;
    private GameObject? _host;

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        CommandStack.Clear();
        _canvasGo = UiTestCanvas.Create("ChromeCanvas");
        _host = new GameObject("ChromeHost");

        foreach (var type in WindowTypes())
        {
            var build = type.GetMethod("Build", new[] { typeof(Transform) });
            Assert.IsNotNull(build, $"{type.Name}: нет Build(Transform) — сторож не может собрать окно");
            build!.Invoke(_host.AddComponent(type), new object[] { _canvasGo.transform });
        }
    }

    [TearDown]
    public void TearDown()
    {
        UiTestCanvas.Release(_canvasGo);
        if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        LevelRegistry.Reset();
        CommandStack.Clear();
        ProjectWindows.Clear();
    }

    private static IEnumerable<Type> WindowTypes() =>
        typeof(IProjectWindow).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t)
                && typeof(IProjectWindow).IsAssignableFrom(t))
            .Concat(new[] { typeof(ContextMenuUI), typeof(GroupMenuUI), typeof(MeasurePropertiesUI) })
            .Distinct()
            .OrderBy(t => t.Name);

    private List<(RectTransform panel, RectTransform close)> Windows() =>
        _canvasGo!.GetComponentsInChildren<Button>(true)
            .Where(b => b.name == "CloseBtn")
            .Select(b => ((RectTransform)b.transform.parent, (RectTransform)b.transform))
            .ToList();

    private static float CentreY(RectTransform panel, RectTransform rect) =>
        panel.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).y;

    private static List<RectTransform> Titles(RectTransform panel) =>
        panel.GetComponentsInChildren<WindowTitleMarker>(true)
            .Select(m => (RectTransform)m.transform)
            .ToList();

    private static List<string> TitleOffenders(RectTransform panel, RectTransform close)
    {
        var offenders = new List<string>();
        var titles = Titles(panel);
        if (titles.Count == 0)
        {
            offenders.Add($"{panel.name}: заголовок не собран WindowTitle.Create");
            return offenders;
        }

        float closeY = CentreY(panel, close);
        foreach (var title in titles)
        {
            float delta = CentreY(panel, title) - closeY;
            if (Mathf.Abs(delta) > SameRowTolerancePx)
                offenders.Add($"{panel.name}/{title.name}: заголовок на {delta:F1} px от линии крестика");
        }
        return offenders;
    }

    [Test]
    public void TheSweep_SeesEveryWindowWithACloseButton()
    {
        var names = Windows().Select(w => w.panel.name).ToList();

        Assert.GreaterOrEqual(names.Count, 12,
            "обход крестиков нашёл меньше окон, чем есть в продукте, — сторож смотрит не туда и зеленел бы впустую: "
            + string.Join(", ", names));
        foreach (var expected in new[] { "SettingsPanel", "SpecPanel", "LevelsWindow", "ContextMenu", "GroupMenu" })
            CollectionAssert.Contains(names, expected, $"в обходе нет окна {expected}");
    }

    [Test]
    public void EveryWindow_TitleAndCloseButton_ShareOneRow()
    {
        var offenders = Windows().SelectMany(w => TitleOffenders(w.panel, w.close)).ToList();

        Assert.IsEmpty(offenders,
            $"заголовок и крестик окна стоят на одной линии с допуском {SameRowTolerancePx} px (центры по Y); "
            + "заголовок строится WindowTitle.Create, а не руками с собственным числом:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void EveryWindow_TitleRow_DoesNotMoveWhenTheWindowChangesHeight()
    {
        var offenders = new List<string>();
        foreach (var (panel, close) in Windows())
        {
            if (panel.name == "ContextMenu") continue;
            var size = panel.sizeDelta;
            panel.sizeDelta = new Vector2(size.x, size.y + ExtraHeightToTryPx);
            offenders.AddRange(TitleOffenders(panel, close));
            panel.sizeDelta = size;
        }

        Assert.IsEmpty(offenders,
            "окна меняют высоту (список этажей растёт, групповое меню сжимается до подсказки), и заголовок "
            + "с крестиком обязаны оставаться пришитыми к ВЕРХУ окна, а не к его середине:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void EveryWindow_CanBeDraggedByItsTitleBar()
    {
        var offenders = new List<string>();
        foreach (var (panel, _) in Windows())
        {
            var handle = panel.GetComponent<WindowDragHandle>();
            if (handle == null)
            {
                offenders.Add($"{panel.name}: нет WindowDragHandle — окно не перетаскивается");
                continue;
            }
            if (panel.GetComponent<WindowScreenGuard>() == null)
                offenders.Add($"{panel.name}: нет WindowScreenGuard — окно можно утащить за экран");

            foreach (var title in Titles(panel))
            {
                float depth = panel.rect.yMax - CentreY(panel, title);
                if (depth > handle.HandleHeight)
                    offenders.Add($"{panel.name}/{title.name}: центр заголовка на {depth:F0} px от верха, "
                        + $"полоса перетаскивания {handle.HandleHeight:F0} px");
            }
        }

        Assert.IsEmpty(offenders,
            "каждое окно с шапкой тянется за шапку: полоса перетаскивания накрывает заголовок:\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void LevelsWindow_StretchedByManyLevels_KeepsTitleAndColumnHeadersAtTheTop()
    {
        var levels = Enumerable.Range(0, LevelsThatStretchTheWindow)
            .Select(i => new Level("L" + i, "этаж " + i, i * 3000, 3000))
            .ToArray();
        LevelRegistry.Set(levels);
        _host!.GetComponent<LevelsWindowUI>().SetVisible(true);
        var panel = (RectTransform)_canvasGo!.transform.Find("LevelsWindow");
        var close = (RectTransform)panel.Find("CloseBtn");

        Assert.Greater(panel.sizeDelta.y, LevelsWindowUI.MinPanelHeight + 0.1f,
            "список из девяти этажей обязан раздвинуть окно, иначе проверка ничего не проверяет");
        Assert.IsEmpty(TitleOffenders(panel, close), "окно выросло — заголовок остался на линии крестика");

        float headerY = CentreY(panel, (RectTransform)panel.Find("LvHeaderName"));
        float firstRowY = CentreY(panel, (RectTransform)panel.Find("LvRows").GetChild(0));
        Assert.Greater(headerY, firstRowY,
            "подписи колонок стоят над первой строкой списка; у выросшего окна они уезжали на середину");
        Assert.Less(panel.rect.yMax - headerY, LevelsWindowUI.RowsTopY,
            "подписи колонок стоят в верхней части окна, выше начала списка");
    }
}
