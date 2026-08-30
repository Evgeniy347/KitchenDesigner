using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class HelpLayoutReproTests
{
    private const float PixelEpsilon = 0.01f;

    private GameObject? _canvasGo;
    private RectTransform? _panel;
    private RectTransform? _title;
    private RectTransform? _body;
    private RectTransform? _close;

    [SetUp]
    public void SetUp()
    {
        _canvasGo = new GameObject("HelpTestCanvas");
        var canvas = _canvasGo.AddComponent<Canvas>();
        _canvasGo.AddComponent<CanvasScaler>();
        _canvasGo.AddComponent<GraphicRaycaster>();
        var help = _canvasGo.AddComponent<HelpUI>();
        help.Build(canvas.transform);

        _panel = (RectTransform)_canvasGo!.transform.Find("HelpPanel");
        _panel.gameObject.SetActive(true);
        _title = (RectTransform)_panel.Find("HelpTitle");
        _body = (RectTransform)_panel.Find("HelpText");
        _close = (RectTransform)_panel.Find("HelpClose");
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_canvasGo!);

    private static float ContentWidth => HelpUI.PanelWidth - 2f * UIStyle.WindowPad;

    [Test]
    public void HelpUI_Build_BodyStartsBelowTitleWithSectionGap()
    {
        float titleBottom = _title!.anchoredPosition.y - _title.rect.height * 0.5f;
        float bodyTop = _body!.anchoredPosition.y + _body.rect.height * 0.5f;

        Assert.LessOrEqual(bodyTop, titleBottom - UIStyle.GapSection + PixelEpsilon,
            "Баг: первая строка справки наезжала на заголовок «Справка» — высота текстового " +
            "блока была угадана константой, а не измерена; текст обязан начинаться не выше " +
            "GapSection под заголовком.");
    }

    [Test]
    public void HelpUI_Build_TextFitsItsRectWithoutTruncation()
    {
        var label = _body!.GetComponent<TextMeshProUGUI>();
        float preferred = label.GetPreferredValues(ContentWidth, 0f).y;

        Assert.LessOrEqual(preferred, _body.rect.height + PixelEpsilon,
            "Прямоугольник текста обязан равняться измеренной высоте строк, иначе при " +
            "добавлении строк часть справки обрезается.");
    }

    [Test]
    public void HelpUI_Build_CloseButtonSitsRightBelowText()
    {
        float bodyBottom = _body!.anchoredPosition.y - _body.rect.height * 0.5f;
        float closeTop = _close!.anchoredPosition.y + _close.rect.height * 0.5f;

        Assert.LessOrEqual(closeTop, bodyBottom - UIStyle.GapSection + PixelEpsilon,
            "Кнопка «Закрыть» обязана стоять под текстом через GapSection.");
        Assert.GreaterOrEqual(closeTop, bodyBottom - UIStyle.GapSection - 1f,
            "Баг: внизу окна была мёртвая зона ~90px — кнопка прижималась к краю панели, " +
            "а не к последней строке текста; зазор между ними не должен расползаться.");
    }

    [Test]
    public void HelpUI_Build_PanelHeightMatchesMeasuredContent()
    {
        float titleHeight = _title!.GetComponent<TextMeshProUGUI>()
            .GetPreferredValues(ContentWidth, 0f).y;
        float expected = 2f * UIStyle.WindowPad + titleHeight + UIStyle.GapSection
            + _body!.rect.height + UIStyle.GapSection + _close!.rect.height;

        Assert.AreEqual(expected, _panel!.rect.height, PixelEpsilon,
            "Высота окна обязана складываться из измеренных частей и токенов отступов " +
            "(WindowPad/GapSection) — любые захардкоженные поля добавляют мёртвую зону.");
    }

    [Test]
    public void HelpUI_Build_CloseButtonNotShorterThanHitTarget()
    {
        Assert.GreaterOrEqual(_close!.rect.height, UIStyle.HitTarget - PixelEpsilon,
            "Правило 8 UI-GUIDELINES: кликабельный элемент не ниже HitTarget.");
    }
}
