using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Оболочка по макету shell.png (docs/ui-redesign/shell.md): тулбар 48 с кнопкой 40 и
/// иконкой 20 (D11), группы Файл | Правка | Этаж | Инструменты | Режим вида … справа
/// Панели | Сервис, у всех кнопок ОДИН фон-состояние, а F9-HUD висит под тулбаром.
/// Порядок и размеры меряются по построенным узлам — «на глаз по скриншоту» их не удержать.</summary>
public class ToolbarShellLayoutTests
{
    private sealed class FakeToolbarHost : IToolbarHost
    {
        public void TogglePanel(ToolbarPanel panel) { }
        public bool IsPanelVisible(ToolbarPanel panel) => false;
        public void SaveCurrent() { }
        public void NewProject() { }
        public void SaveAs() { }
        public void LoadDialog() { }
    }

    private static readonly string[] LeftFlowInReadingOrder =
    {
        "New", "Load", "Save", "SaveAs",
        "Undo", "Redo",
        "LevelUp", "LevelLabel", "LevelDown", "LevelsWindow",
        "HandleMode", "MeasureToggle", "Eyedropper", "LightsToggle",
        "ViewMode",
    };

    private static readonly string[] RightGroupInReadingOrder =
    {
        "Hierarchy", "Errors", "GotoIssue", "Spec", "ProjectInstructions",
        "DayNight", "Music", "Settings",
    };

    private GameObject? _canvasGo;
    private ToolbarUI _toolbar = null!;
    private Transform _bar = null!;
    private float _hudBottomBefore;

    [SetUp]
    public void SetUp()
    {
        _hudBottomBefore = PerfHud.ToolbarBottomY;
        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 2800),
        });
        CommandStack.Clear();
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
        _toolbar = new ToolbarUI();
        _toolbar.Build(_canvasGo.transform, new FakeToolbarHost());
        _bar = _canvasGo.transform.Find("Toolbar")!;
        _toolbar.Refresh();
    }

    [TearDown]
    public void TearDown()
    {
        PerfHud.ToolbarBottomY = _hudBottomBefore;
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        LevelRegistry.Reset();
        CommandStack.Clear();
    }

    private RectTransform Node(string name) => (RectTransform)_bar.Find(name)!;

    private IEnumerable<Button> IconButtons() =>
        _bar.GetComponentsInChildren<Button>(true).Where(b => b.transform.parent == _bar);

    [Test]
    public void TheBar_IsFortyEightHigh_AndItsConstantIsTheDesignToken()
    {
        Assert.AreEqual(UIStyle.ToolbarH, ToolbarUI.BarHeight, "высота тулбара — токен D11, а не число по месту");
        Assert.AreEqual(48f, UIStyle.ToolbarH);
        Assert.AreEqual(ToolbarUI.BarHeight, ((RectTransform)_bar).sizeDelta.y, "узел «Toolbar» имеет высоту константы");
    }

    [Test]
    public void EveryIconButton_IsFortyBy40_WithAOneTwentyIconInside()
    {
        var wrong = new List<string>();
        foreach (var button in IconButtons())
        {
            var rect = (RectTransform)button.transform;
            var iconNode = button.transform.Find(button.name + "_Icon");
            if (iconNode == null) { wrong.Add(button.name + ": нет узла иконки"); continue; }

            var icon = iconNode.GetComponent<Image>();
            var iconRect = (RectTransform)iconNode;
            if (rect.sizeDelta != new Vector2(UIStyle.IconButton, UIStyle.IconButton))
                wrong.Add($"{button.name}: кнопка {rect.sizeDelta}, а не 40×40");
            if (iconRect.sizeDelta != new Vector2(UIStyle.IconSize, UIStyle.IconSize))
                wrong.Add($"{button.name}: иконка {iconRect.sizeDelta}, а не 20×20");
            if (icon.sprite == null || icon.sprite.rect.width != icon.sprite.rect.height)
                wrong.Add($"{button.name}: спрайт иконки не квадратный");
        }

        Assert.IsEmpty(wrong, "D11: иконка 20 в кнопке 40 — одна на весь ряд (аудит: «ключ ~12 px, шестерёнка 24»):\n"
            + string.Join("\n", wrong));
        Assert.GreaterOrEqual(IconButtons().Count(), 20, "скан увидел кнопки тулбара — иначе проверять нечего");
    }

    [Test]
    public void EveryIcon_ComesFromTheOneOutlineSet()
    {
        var foreign = IconButtons()
            .Select(b => (name: b.name, sprite: b.transform.Find(b.name + "_Icon")!.GetComponent<Image>().sprite))
            .Where(p => p.sprite == null || !p.sprite.texture.name.StartsWith("OutlineIcon_"))
            .Select(p => p.name)
            .ToList();

        Assert.IsEmpty(foreign, "иконки мимо OutlineIcons — это снова иконки разного веса: " + string.Join(", ", foreign));
    }

    [Test]
    public void EveryIconButton_HasTheSameQuietBackground_AndATooltip()
    {
        var bad = new List<string>();
        foreach (var button in IconButtons())
        {
            var bg = (Image)button.targetGraphic;
            bool quiet = button.colors.normalColor == UIStyle.TintHidden && bg.color == UIStyle.SurfaceHover;
            bool pressed = button.colors.normalColor == UIStyle.NoTint && bg.color == UIStyle.SurfaceActive;
            if (!quiet && !pressed)
                bad.Add(button.name + ": фон не из двух состояний — тихий SurfaceHover или нажатый SurfaceActive");
            if (button.GetComponent<EventTrigger>() == null)
                bad.Add(button.name + ": нет tooltip");
        }

        Assert.IsEmpty(bad, "«у всех кнопок один фон-состояние» (shell.md, п. 1):\n" + string.Join("\n", bad));
    }

    [Test]
    public void TheLeftFlow_RunsInTheDesignedGroupOrder_FileEditLevelToolsMode()
    {
        var xs = LeftFlowInReadingOrder.Select(n => (name: n, x: Node(n).anchoredPosition.x)).ToList();

        for (int i = 1; i < xs.Count; i++)
            Assert.Greater(xs[i].x, xs[i - 1].x,
                $"«{xs[i].name}» обязана стоять правее «{xs[i - 1].name}» — группы Файл | Правка | Этаж | Инструменты | Режим вида");
    }

    [Test]
    public void TheRightGroup_RunsPanelsThenService_AndHugsTheRightEdge()
    {
        var xs = RightGroupInReadingOrder.Select(n => (name: n, x: Node(n).anchoredPosition.x)).ToList();

        for (int i = 1; i < xs.Count; i++)
            Assert.Greater(xs[i].x, xs[i - 1].x, $"«{xs[i].name}» правее «{xs[i - 1].name}»: Панели | Сервис");
        foreach (var name in RightGroupInReadingOrder)
        {
            Assert.AreEqual(1f, Node(name).anchorMin.x, name + " якорится справа: окно расширили — группа поехала за краем");
            Assert.AreEqual(1f, Node(name).anchorMax.x, name);
        }
        Assert.AreEqual(-ToolbarMetrics.EdgeInset, Node("Settings").anchoredPosition.x, 0.01f, "крайняя кнопка — в 8 px от края");
    }

    [Test]
    public void TheFileGroup_IsNewOpenSaveSaveAs_AndTheFileIsNotTornByPanels()
    {
        float panelLeft = RightGroupInReadingOrder.Min(n => Node(n).anchoredPosition.x);
        Assert.Less(panelLeft, 0f);

        Assert.Less(Node("SaveAs").anchoredPosition.x, Node("Undo").anchoredPosition.x,
            "«Сохранить как» стоит вплотную к файловой группе, а «Отменить» уже в правке (дефект №2 аудита: файл был разорван панелями)");
    }

    [Test]
    public void ThereAreFiveSeparators_FourBetweenTheLeftGroupsAndOneBetweenPanelsAndService()
    {
        var seps = _bar.Cast<Transform>().Where(t => t.name == "Separator").Select(t => (RectTransform)t).ToList();

        Assert.AreEqual(5, seps.Count);
        foreach (var sep in seps)
        {
            Assert.AreEqual(UIStyle.DividerPx, sep.sizeDelta.x, 0.01f, "разделитель 1 px");
            Assert.AreEqual(24f, sep.sizeDelta.y, 0.01f, "высотой 24");
            Assert.AreEqual(UIStyle.Separator, sep.GetComponent<Image>().color);
        }
        Assert.AreEqual(1, seps.Count(s => s.anchorMin.x == 1f), "один разделитель правой группы");
    }

    [Test]
    public void TheViewMode_IsOneSegmentedControl_VerticallyCentredInTheBar()
    {
        var mode = Node("ViewMode");

        Assert.IsNotNull(mode.GetComponent<SegmentedControl>());
        Assert.AreEqual(ToolbarMetrics.SegmentH, mode.sizeDelta.y, "сегмент высотой 32");
        Assert.AreEqual(-(ToolbarUI.BarHeight - mode.sizeDelta.y) * 0.5f, mode.anchoredPosition.y, 0.01f,
            "по вертикали он стоит по центру полосы");
    }

    [Test]
    public void TheBar_HasAQuietBottomRule_NotAShadowOrAFrame()
    {
        var rule = (RectTransform)_bar.Find("BottomRule")!;

        Assert.AreEqual(UIStyle.Divider, rule.GetComponent<Image>().color);
        Assert.AreEqual(UIStyle.DividerPx, rule.sizeDelta.y, 0.01f);
        Assert.IsFalse(rule.GetComponent<Image>().raycastTarget, "линия не ловит клики");
    }

    [Test]
    public void TheHud_StaysUnderTheBar_OnTheCanvasScaleOfTheMoment()
    {
        var canvas = _canvasGo!.GetComponent<Canvas>();
        canvas.scaleFactor = 1.5f;

        _toolbar.Refresh();

        Assert.AreEqual(ToolbarUI.BarHeight * 1.5f, PerfHud.ToolbarBottomY, 0.01f,
            "HUD F9 рисуется IMGUI в физических пикселях, а тулбар лежит в единицах канвы: на масштабе 150% "
            + "тулбар занимает 72 px, и HUD по прежней константе 48 залезал бы на него");
    }

    [Test]
    public void TheHud_DefaultsToTheBarHeight_BeforeTheFirstRefresh()
    {
        Assert.AreEqual(ToolbarUI.BarHeight, PerfHud.DefaultToolbarBottomY);
    }
}
