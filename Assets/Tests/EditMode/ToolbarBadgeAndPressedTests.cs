using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Счётчик ошибок и нажатое состояние тулбара по макету shell.png: бейдж 16 в правом
/// верхнем углу кнопки («красная цифра 10 px поверх красной иконки» — дефект №4 аудита),
/// включённая кнопка — фон <c>SurfaceActive</c>, выключенная — тихая.</summary>
public class ToolbarBadgeAndPressedTests
{
    private sealed class SwitchableHost : IToolbarHost
    {
        public ToolbarPanel? Visible;
        public void TogglePanel(ToolbarPanel panel) { }
        public bool IsPanelVisible(ToolbarPanel panel) => Visible == panel;
        public void SaveCurrent() { }
        public void NewProject() { }
        public void SaveAs() { }
        public void LoadDialog() { }
    }

    private GameObject? _canvasGo;

    [SetUp]
    public void SetUp()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        LevelRegistry.Reset();
    }

    private ToolbarIssueBadge NewBadge()
    {
        var host = new GameObject("ErrorsButton", typeof(RectTransform));
        host.transform.SetParent(_canvasGo!.transform, false);
        return ToolbarIssueBadge.Create(host.transform);
    }

    private Image Fill(ToolbarIssueBadge badge) => (Image)FindPill(badge).GetComponent<Image>();

    private static Transform FindPill(ToolbarIssueBadge badge) => badge.Pill;

    [Test]
    public void NoIssues_HideTheBadgeCompletely()
    {
        var badge = NewBadge();

        badge.Show(0, false);

        Assert.IsFalse(badge.Visible, "ноль проблем — бейджа нет, а не красный «0»");
    }

    [Test]
    public void Errors_ShowAWhiteNumberOnAFilledDangerPill()
    {
        var badge = NewBadge();

        badge.Show(2, true);

        Assert.IsTrue(badge.Visible);
        Assert.AreEqual("2", badge.Text);
        Assert.AreEqual(UIStyle.Danger, Fill(badge).color, "заливка Danger: белое на ней 6,6:1, а красная цифра на красной иконке не читалась");
        Assert.AreEqual(UIStyle.TextOnAccent, badge.LabelColor);
    }

    [Test]
    public void WarningsOnly_UseTheWarningFill_WithDarkText_NotWhiteOnOrange()
    {
        var badge = NewBadge();

        badge.Show(3, false);

        Assert.AreEqual(UIStyle.TextWarning, Fill(badge).color);
        Assert.AreEqual(UIStyle.NavBg, badge.LabelColor,
            "белый на оранжевом — около 2:1, ниже порога 4,5:1: на заливке предупреждения цифра тёмная");
    }

    [Test]
    public void TheBadge_IsSixteenHigh_AtLeastSixteenWide_AndCappedAtNinetyNinePlus()
    {
        var badge = NewBadge();

        badge.Show(7, true);
        var narrow = (RectTransform)FindPill(badge);
        Assert.AreEqual(16f, narrow.sizeDelta.y, "высота бейджа 16 (макет)");
        Assert.GreaterOrEqual(narrow.sizeDelta.x, 16f, "минимум круг 16×16");

        badge.Show(250, true);
        Assert.AreEqual("99+", badge.Text, "трёхзначное число не раздувает кнопку");
        Assert.Greater(narrow.sizeDelta.x, 16f, "«99+» шире круга — пилюля растёт под текст");
    }

    [Test]
    public void TheBadge_SitsInTheTopRightCornerOfItsButton()
    {
        var badge = NewBadge();
        var rect = (RectTransform)FindPill(badge);

        Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMin);
        Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMax);
        Assert.AreEqual(new Vector2(1f, 1f), rect.pivot);
        Assert.Less(rect.anchoredPosition.x, 0f, "внутрь от правого края");
        Assert.Less(rect.anchoredPosition.y, 0f, "и вниз от верхнего");
    }

    [Test]
    public void ABadgeNumberTakesTheCaptionSize_NotTenPixels()
    {
        var badge = NewBadge();
        badge.Show(5, true);

        Assert.AreEqual(UIStyle.FontCaption, badge.FontSize, "меньше 13 в интерфейсе не бывает (D3)");
    }

    [Test]
    public void APressedToggle_PaintsSurfaceActive_AndGoesQuietAgainWhenReleased()
    {
        var host = new SwitchableHost();
        var toolbar = new ToolbarUI();
        toolbar.Build(_canvasGo!.transform, host);
        var bar = _canvasGo.transform.Find("Toolbar")!;
        var scene = bar.Find("Hierarchy")!.GetComponent<Button>();

        toolbar.Refresh();
        Assert.AreEqual(UIStyle.TintHidden, scene.colors.normalColor, "до нажатия фон не виден");

        host.Visible = ToolbarPanel.Hierarchy;
        toolbar.Refresh();
        Assert.AreEqual(UIStyle.SurfaceActive, ((Image)scene.targetGraphic).color, "окно открыто — кнопка нажата: SurfaceActive");
        Assert.AreEqual(UIStyle.NoTint, scene.colors.normalColor, "и фон виден в покое");

        host.Visible = null;
        toolbar.Refresh();
        Assert.AreEqual(UIStyle.TintHidden, scene.colors.normalColor, "окно закрыто — кнопка снова тихая");
        Assert.AreEqual(UIStyle.SurfaceHover, ((Image)scene.targetGraphic).color);
    }

    [Test]
    public void OnlyTheOpenPanel_LooksPressed()
    {
        var host = new SwitchableHost { Visible = ToolbarPanel.Settings };
        var toolbar = new ToolbarUI();
        toolbar.Build(_canvasGo!.transform, host);
        toolbar.Refresh();
        var bar = _canvasGo.transform.Find("Toolbar")!;

        Assert.AreEqual(UIStyle.NoTint, bar.Find("Settings")!.GetComponent<Button>().colors.normalColor);
        Assert.AreEqual(UIStyle.TintHidden, bar.Find("Spec")!.GetComponent<Button>().colors.normalColor);
        Assert.AreEqual(UIStyle.TintHidden, bar.Find("Hierarchy")!.GetComponent<Button>().colors.normalColor);
    }
}
