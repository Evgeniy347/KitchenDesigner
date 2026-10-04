using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>D9 + D10 + D10.1: режим правки и вид «Фото» больше не делят одну кнопку с
/// трёхшаговым циклом, а панели тулбара приведены к одному правилу — иконка с
/// tooltip, текстом остаются только режимы. D10.1 дорисовала недостающие иконки
/// («Спецификация», «Сцена», «Инструкции», «Ручки», «Тонировка») и перевела на них
/// последние пять текстовых кнопок. Ширина проверяется прямым замером построенного
/// UI, а не константой на глаз.</summary>
public class ToolbarModeAndWidthTests
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

    private GameObject? _canvasGo;
    private Transform _bar = null!;
    private ResizeHandleManager.HandleMode _handleModeBefore;

    [SetUp]
    public void SetUp()
    {
        _handleModeBefore = ResizeHandleManager.Mode;
        EditModeManager.SetMode(EditMode.Normal);

        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();

        var toolbar = new ToolbarUI();
        toolbar.Build(_canvasGo.transform, new FakeToolbarHost());
        _bar = _canvasGo.transform.Find("Toolbar")!;
    }

    [TearDown]
    public void TearDown()
    {
        if (ResizeHandleManager.Mode != _handleModeBefore) ResizeHandleManager.ToggleMode();
        EditModeManager.SetMode(EditMode.Normal);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    // ── D9: режим и вид разведены ───────────────────────────

    private Transform Deep(string name)
    {
        foreach (var t in _bar.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        Assert.Fail("на тулбаре нет узла " + name);
        return null!;
    }

    [Test]
    public void Build_CreatesOneSegmentedViewModeControl_WithNormalRoomAndPhotoSegments()
    {
        var segmented = _bar.Find("ViewMode")!.GetComponent<SegmentedControl>();

        Assert.IsNotNull(segmented, "режим вида — ОДИН сегментный контрол (D11), а не три отдельные кнопки");
        Assert.AreEqual(3, segmented.Segments.Count);
        Assert.AreSame(Deep("ModeNormal").GetComponent<Button>(), segmented.Segments[0], "«Обычный» — первый сегмент");
        Assert.AreSame(Deep("ModeRoom").GetComponent<Button>(), segmented.Segments[1], "«Помещение» — второй");
        Assert.AreSame(Deep("ModePhoto").GetComponent<Button>(), segmented.Segments[2], "«Фото» — третий, а не шаг общего цикла");
    }

    [Test]
    public void NormalModeButton_FromPhoto_ReturnsToNormalInOneClick()
    {
        EditModeManager.SetMode(EditMode.Room);
        EditModeManager.SetMode(EditMode.Photo);
        Assume.That(PhotoMode.Active, Is.True, "фоторежим обязан быть включён перед проверкой");

        Deep("ModeNormal").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(EditMode.Normal, EditModeManager.Mode,
            "раньше выход из фото требовал сначала попасть в «Помещение» — один клик обязан "
            + "вернуть сразу в «Обычный»");
        Assert.IsFalse(PhotoMode.Active);
    }

    [Test]
    public void RoomModeButton_Click_NeverTogglesPhoto()
    {
        Deep("ModeRoom").GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(EditMode.Room, EditModeManager.Mode);
        Assert.IsFalse(PhotoMode.Active, "переключение обычный/помещение не имеет права включать фото");
    }

    [Test]
    public void PhotoModeButton_Click_DrivesThePhotoModeEntryPoint()
    {
        Deep("ModePhoto").GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(PhotoMode.Active,
            "кнопка «Фото» обязана звать существующую точку входа PhotoMode.Toggle");
        Assert.AreEqual(EditMode.Photo, EditModeManager.Mode);
    }

    // ── D10: панели — иконки с tooltip, режимы — текст ──────

    [Test]
    public void ErrorsButton_IsAnIconWithTooltip_NotAWordyLabel()
    {
        var errors = _bar.Find("Errors")!;

        Assert.IsNotNull(errors.Find("Errors_Icon"), "«Ошибки» переведена на иконку (правило D10)");
        Assert.IsNotNull(errors.GetComponent<EventTrigger>(),
            "иконная кнопка обязана иметь tooltip — UI-GUIDELINES §5");
    }

    [TestCase("Spec")]
    [TestCase("Hierarchy")]
    [TestCase("ProjectInstructions")]
    public void FormerlyTextPanelButtons_AreNowIconsWithTooltip_D10Point1(string buttonName)
    {
        var btn = _bar.Find(buttonName)!;

        Assert.IsNotNull(btn.Find(buttonName + "_Icon"),
            $"«{buttonName}» не из чего было сделать иконку раньше — D10.1 дорисовала недостающий "
            + "значок в IconFactory, и кнопка обязана его использовать");
        Assert.IsNull(btn.GetComponentInChildren<TMP_Text>(),
            "текст должен уйти целиком — иначе кнопка несёт и иконку, и подпись разом");
        Assert.IsNotNull(btn.GetComponent<EventTrigger>(),
            "иконная кнопка обязана иметь tooltip — UI-GUIDELINES §5");
    }

    /// <summary>L3 (обзор ui-mcp): переключатель этажа рисовал стрелки текстовыми
    /// глифами `▲`/`▼` (<c>UIStyle.GlyphUp</c>, <c>GlyphDropdown</c> — тот же символ,
    /// что и у раскрытия дерева) вместо спрайта IconFactory, которым правило D10
    /// требует рисовать управляющие стрелки (docs/UI-GUIDELINES.md §4).</summary>
    [TestCase("LevelUp")]
    [TestCase("LevelDown")]
    public void LevelSwitcherButtons_AreIconsWithTooltip_NotTextGlyphs(string buttonName)
    {
        var btn = _bar.Find(buttonName)!;

        Assert.IsNotNull(btn.Find(buttonName + "_Icon"),
            $"«{buttonName}» обязана нести спрайт IconFactory, а не текстовый глиф ▲/▼");
        Assert.IsNull(btn.GetComponentInChildren<TMP_Text>(),
            "текстового глифа остаться не должно — только значок");
        Assert.IsNotNull(btn.GetComponent<EventTrigger>(),
            "иконная кнопка обязана иметь tooltip — UI-GUIDELINES §5");
    }

    [Test]
    public void ModeButtons_StayText_AsTheOneNamedExceptionToTheIconRule()
    {
        Assert.IsNotNull(Deep("ModeNormal").GetComponentInChildren<TMP_Text>(),
            "режимы — единственное, что остаётся текстом по правилу D10");
        Assert.IsNotNull(Deep("ModeRoom").GetComponentInChildren<TMP_Text>());
        Assert.IsNotNull(Deep("ModePhoto").GetComponentInChildren<TMP_Text>());
    }

    [Test]
    public void TextButtons_WidthTracksItsOwnLabel_NotASharedMagicNumber()
    {
        var normal = (RectTransform)Deep("ModeNormal");
        var room = (RectTransform)Deep("ModeRoom");

        Assert.Greater(room.sizeDelta.x, normal.sizeDelta.x,
            "«Помещение» длиннее «Обычный» — авторазмер обязан это отразить, а не тащить "
            + "общую фиксированную ширину. Это единственная пара, что осталась текстом после "
            + "D10.1 — «Спецификация»/«Сцена» стали иконками");
    }

    [Test]
    public void HandleModeButton_IsAnIconWithTooltip_SwapsShapeWithMode_NeverResizes()
    {
        var handleBtn = (RectTransform)_bar.Find("HandleMode")!;
        var icon = handleBtn.Find("HandleMode_Icon")!.GetComponent<Image>();
        Assert.IsNotNull(handleBtn.GetComponent<EventTrigger>(),
            "иконная кнопка обязана иметь tooltip — UI-GUIDELINES §5");

        float widthBefore = handleBtn.sizeDelta.x;
        var spriteBefore = icon.sprite;

        handleBtn.GetComponent<Button>().onClick.Invoke();

        Assert.AreNotEqual(spriteBefore, icon.sprite,
            "«растяжение» и «перенос» читаются теперь по ФОРМЕ значка (доступность — "
            + "LEAD-AGENT.md §2), а не по тексту, значит клик обязан её сменить");
        Assert.AreEqual(widthBefore, handleBtn.sizeDelta.x, 0.01f,
            "иконная кнопка фиксированной ширины не имеет права дрожать при переключении");

        handleBtn.GetComponent<Button>().onClick.Invoke();
        Assert.AreEqual(spriteBefore, icon.sprite, "второй клик обязан вернуть исходную иконку");
    }

    [Test]
    public void LevelLabel_ShowsTheDefaultLevelName_OnOneLine_NotWrapped()
    {
        var label = _bar.Find("LevelLabel")!.GetComponent<TMP_Text>();
        label.gameObject.SetActive(true);
        label.text = "1 этаж";
        label.ForceMeshUpdate();

        Assert.IsFalse(label.enableWordWrapping,
            "перенос по словам на узкой плашке рвёт «1 этаж» на «1» и «этаж» — подпись обязана "
            + "остаться одной строкой");
        Assert.AreEqual(1, label.textInfo.lineCount,
            "«1 этаж» — имя уровня по умолчанию (LevelResolution.DefaultLevelName) — обязано "
            + "читаться одной строкой, а не двумя");
    }

    // ── D10: помещается на 1366px ноутбука, и не разъезжается на 1920 ──

    private static float RightEdgeOfLeftFlow(Transform bar)
    {
        float maxRight = 0f;
        foreach (Transform child in bar)
        {
            var rt = (RectTransform)child;
            if (rt.anchorMin.x != 0f || rt.anchorMax.x != 0f) continue;
            float right = rt.anchoredPosition.x + rt.sizeDelta.x;
            if (right > maxRight) maxRight = right;
        }
        return maxRight;
    }

    private static float WidthReservedByRightGroup(Transform bar)
    {
        float reserved = 0f;
        foreach (Transform child in bar)
        {
            var rt = (RectTransform)child;
            if (rt.anchorMin.x != 1f || rt.anchorMax.x != 1f) continue;
            float leftEdgeFromRight = -rt.anchoredPosition.x + rt.sizeDelta.x;
            if (leftEdgeFromRight > reserved) reserved = leftEdgeFromRight;
        }
        return reserved;
    }

    [TestCase(1920f)]
    [TestCase(1366f)]
    public void Toolbar_LeftFlowFitsBesideTheRightAnchoredGroup(float screenWidth)
    {
        float reservedFromRightEdge = WidthReservedByRightGroup(_bar);
        float budget = screenWidth - reservedFromRightEdge;

        float flowRight = RightEdgeOfLeftFlow(_bar);

        Assert.Greater(reservedFromRightEdge, 0f, "правая группа (панели и сервис) обязана существовать — иначе бюджет считает пустоту");
        Assert.Less(flowRight, budget,
            $"при ширине экрана {screenWidth}px поток кнопок доходит до {flowRight:0}px, "
            + $"а свободно только {budget:0}px до правой группы «Панели | Сервис»");
    }
}
