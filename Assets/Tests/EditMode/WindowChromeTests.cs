using System;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// D5 — анатомия окна одной сборкой: заголовок слева на линии крестика, тихий ×, футер с основной
// кнопкой справа. Окна T5–T11 переезжают на WindowChrome по одному; здесь — контракт компонента,
// на который они переезжают (docs/ui-redesign/README.md → «API общих компонентов»).
public class WindowChromeTests
{
    private GameObject? _canvas;
    private bool _closed;

    [SetUp]
    public void SetUp()
    {
        _canvas = UiTestCanvas.Create("ChromeTestCanvas");
        _closed = false;
    }

    [TearDown]
    public void TearDown() => UiTestCanvas.Release(_canvas);

    private WindowChrome Build(bool footer = true, Action? onClose = null) =>
        WindowChrome.Create(_canvas!.transform, "Probe", "Проба", new Vector2(640f, 480f),
            new WindowChromeOptions { OnClose = onClose ?? (() => _closed = true), HasFooter = footer });

    private static float CentreYFromTop(RectTransform panel, RectTransform rect) =>
        panel.rect.yMax - panel.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).y;

    [Test]
    public void Title_IsLeftAligned_AtTheTitleInset_OnTheCloseButtonRow()
    {
        var chrome = Build();
        var title = chrome.Title.rectTransform;
        float left = chrome.Panel.InverseTransformPoint(title.TransformPoint(title.rect.min)).x
            - chrome.Panel.rect.xMin;

        Assert.AreEqual(UIStyle.WindowTitleInset, left, 0.5f,
            "заголовок слева на 16 px от края во ВСЕХ окнах (D5): по центру стояли «Настройки», «Этажи», «Музыка»");
        Assert.AreEqual(TextAlignmentOptions.Left, chrome.Title.alignment);
        Assert.AreEqual(UIStyle.FontWindowTitle, (int)chrome.Title.fontSize);
        Assert.AreEqual(UIStyle.TitleBarH * 0.5f, CentreYFromTop(chrome.Panel, title), 0.5f,
            "центр заголовка — центр шапки 48, та же линия, что у ×");
        Assert.AreEqual(UIStyle.TitleBarH * 0.5f,
            CentreYFromTop(chrome.Panel, (RectTransform)chrome.CloseButton!.transform), 0.5f);
        Assert.IsNotNull(chrome.Title.GetComponent<WindowTitleMarker>(), "WindowChromeGuardTests узнаёт заголовок по маркеру");
    }

    [Test]
    public void CloseButton_IsQuiet_NamedForTheChromeGuard_AndCloses()
    {
        var chrome = Build();
        var close = chrome.CloseButton!;
        Assert.AreEqual(WindowChrome.CloseButtonName, close.name);
        Assert.AreEqual(0f, close.colors.normalColor.a, 1e-4f,
            "× — тихая кнопка: без фона, пока не наведён курсор; залитый квадрат спорит с основной (D5)");
        Assert.AreEqual(1f, close.colors.highlightedColor.a, 1e-4f, "по наведению фон SurfaceHover появляется");
        Assert.AreEqual(UIStyle.TextSecondary, close.GetComponentInChildren<TMP_Text>().color);
        close.onClick.Invoke();
        Assert.IsTrue(_closed);
    }

    [Test]
    public void NoCloseHandler_MeansNoCloseButton()
    {
        var chrome = WindowChrome.Create(_canvas!.transform, "NoClose", "Т", new Vector2(400f, 300f),
            new WindowChromeOptions());
        Assert.IsNull(chrome.CloseButton);
        Assert.IsFalse(chrome.Panel.GetComponentsInChildren<Button>(true).Any(b => b.name == WindowChrome.CloseButtonName));
    }

    [Test]
    public void Footer_PutsThePrimaryRightmost_TheCancelToItsLeft_SecondaryActionsOnTheLeft()
    {
        var footer = Build().Footer!;
        var cancel = (RectTransform)footer.AddSecondary("Cancel", "Отмена", () => { }).transform;
        var save = (RectTransform)footer.AddPrimary("Save", "Сохранить", () => { }).transform;
        var reset = (RectTransform)footer.AddLeft("Reset", "Сбросить раздел", () => { }, ButtonRole.Link).transform;
        Canvas.ForceUpdateCanvases();

        float Right(RectTransform rt) => footer.Root.InverseTransformPoint(rt.TransformPoint(rt.rect.max)).x;
        float Left(RectTransform rt) => footer.Root.InverseTransformPoint(rt.TransformPoint(rt.rect.min)).x;

        Assert.AreEqual(footer.Root.rect.xMax - UIStyle.Space4, Right(save), 0.5f,
            "основная — крайняя справа, отступ 16 (решение пользователя 2026-10-04: «Отмена | Сохранить»)");
        Assert.AreEqual(Left(save) - UIStyle.Space2, Right(cancel), 0.5f, "«Отмена» слева от основной через 8");
        Assert.AreEqual(footer.Root.rect.xMin + UIStyle.Space4, Left(reset), 0.5f,
            "вторичное, не меняющее решения окна, — слева (D5)");
    }

    [Test]
    public void Footer_PrimaryIsAccent_DangerIsDanger_LabelsReadable()
    {
        var footer = Build().Footer!;
        var save = footer.AddPrimary("Save", "Сохранить", () => { });
        var delete = footer.AddDanger("Delete", "Удалить", () => { });
        Assert.AreEqual(UIStyle.Accent, ((Image)save.targetGraphic).color);
        Assert.AreEqual(UIStyle.TextOnAccent, save.GetComponentInChildren<TMP_Text>().color);
        Assert.AreEqual(UIStyle.Danger, ((Image)delete.targetGraphic).color);
    }

    [Test]
    public void Footer_ButtonWidth_FollowsItsCaption_WithSixteenEachSide()
    {
        var footer = Build().Footer!;
        var b = footer.AddSecondary("Long", "Очень длинная подпись кнопки", () => { });
        var label = b.GetComponentInChildren<TMP_Text>();
        float text = label.GetPreferredValues(label.text).x;
        Assert.GreaterOrEqual(((RectTransform)b.transform).sizeDelta.x, text + 2f * UIStyle.Space4 - 1f,
            "подпись кнопки — одна строка с полем по 16 (макет), а не обрезанный текст в кнопке-константе");
        Assert.AreEqual(UIStyle.FooterButtonH, ((RectTransform)b.transform).sizeDelta.y);
    }

    [Test]
    public void HeaderActions_AtMostTwo_SitBeforeTheClose_OnItsRow()
    {
        var chrome = Build();
        var a = chrome.AddHeaderAction("Add", IconFactory.Pin, "Новая группа", () => { });
        chrome.AddHeaderAction("More", IconFactory.Gear, "Ещё", () => { });
        Assert.Throws<InvalidOperationException>(() => chrome.AddHeaderAction("Third", IconFactory.Pin, "x", () => { }),
            "в шапке не больше двух иконных действий (D5)");

        var close = (RectTransform)chrome.CloseButton!.transform;
        var rt = (RectTransform)a.transform;
        Assert.Less(rt.anchoredPosition.x, close.anchoredPosition.x, "действие стоит левее ×");
        Assert.AreEqual(CentreYFromTop(chrome.Panel, close), CentreYFromTop(chrome.Panel, rt), 0.5f);
        Assert.AreEqual(close.GetSiblingIndex(), chrome.Panel.childCount - 1, "× рисуется последним");
    }

    [Test]
    public void Surface_IsRoundedOpaquePanel_AndItsDecorationsAreNotContent()
    {
        var chrome = Build();
        var fill = chrome.Panel.Find(WindowSurface.FillNode).GetComponent<Image>();
        Assert.AreEqual(UIStyle.Panel, fill.color);
        Assert.AreEqual(Image.Type.Sliced, fill.type, "скругление 6 — 9-slice, а не картинка во весь размер");

        var spans = new System.Collections.Generic.List<ContentSpan>();
        RectSpans.Collect(chrome.Panel, spans);
        Assert.IsFalse(spans.Any(s => s.Name == WindowSurface.ShadowNode),
            "тень выступает за край окна на 8 px — это украшение, а не содержимое: сторож переполнения "
            + "её не меряет");
    }

    [Test]
    public void Body_SitsBetweenHeaderAndFooter_WithTheKindPadding()
    {
        var dialog = Build();
        Assert.AreEqual(UIStyle.TitleBarH + UIStyle.DialogPad, dialog.BodyTop, "тело отступает от линии шапки на паддинг окна");
        Assert.AreEqual(UIStyle.FooterH + UIStyle.DialogPad, dialog.BodyBottom);
        Assert.AreEqual(UIStyle.DialogPad, dialog.BodyPad);
        Assert.AreEqual(640f - 2f * UIStyle.DialogPad, dialog.BodyWidth);

        var tool = WindowChrome.Create(_canvas!.transform, "Tool", "Т", new Vector2(360f, 400f),
            new WindowChromeOptions { Kind = WindowKind.Tool, OnClose = () => { } });
        Assert.AreEqual(UIStyle.ToolPanelPad, tool.BodyPad, "панель-инструмент — паддинг 12 (D5)");
        Assert.AreEqual(UIStyle.Space5, tool.BodyBottom, "окно без футера — 24 снизу (Fluent, D5)");
    }
}
