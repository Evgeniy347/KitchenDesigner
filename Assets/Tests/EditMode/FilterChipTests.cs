using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Чип фильтра (D8, «Ошибки»): выбранный — AccentSubtle с рамкой Accent и полужирным счётчиком,
// невыбранный — на фоне окна с тихой рамкой; ширина по тексту, значок уровня своим цветом.
public class FilterChipTests
{
    private GameObject? _canvas;

    [SetUp]
    public void SetUp() => _canvas = UiTestCanvas.Create("ChipCanvas");

    [TearDown]
    public void TearDown() => UiTestCanvas.Release(_canvas);

    private FilterChip Chip(string? glyph = null, System.Action? onClick = null) =>
        FilterChip.Create(_canvas!.transform, "Chip", "Ошибки", glyph, UIStyle.TextError, onClick ?? (() => { }));

    private static string Text(FilterChip chip) => chip.Button.GetComponentInChildren<TMP_Text>().text;

    [Test]
    public void TheChip_IsAPill28High_WithTheCaptionAndTheCount()
    {
        var chip = Chip();
        chip.SetCount(7);

        Assert.AreEqual(UIStyle.ChipH, chip.Rect.sizeDelta.y);
        Assert.AreEqual("Ошибки 7", Text(chip));
    }

    [Test]
    public void Selected_UsesAccentSubtleWithAnAccentRim_AndABoldCount()
    {
        var chip = Chip();
        chip.SetCount(2);
        chip.SetSelected(true);

        Assert.AreEqual(UIStyle.AccentSubtle, chip.Button.GetComponent<Image>().color);
        Assert.AreEqual(UIStyle.Accent, chip.Rect.Find(FilterChip.RimNode).GetComponent<Image>().color);
        Assert.AreEqual("Ошибки <b>2</b>", Text(chip));
        Assert.AreEqual(UIStyle.Text, chip.Button.GetComponentInChildren<TMP_Text>().color);

        chip.SetSelected(false);
        Assert.AreEqual(UIStyle.Panel, chip.Button.GetComponent<Image>().color);
        Assert.AreEqual(UIStyle.Separator, chip.Rect.Find(FilterChip.RimNode).GetComponent<Image>().color);
        Assert.AreEqual("Ошибки 2", Text(chip));
        Assert.AreEqual(UIStyle.TextSecondary, chip.Button.GetComponentInChildren<TMP_Text>().color,
            "невыбранный чип — вторичный текст: выбранный выделяется не только рамкой");
    }

    [Test]
    public void TheWidth_FollowsTheTextAndTheCount_AndMakesRoomForTheGlyph()
    {
        var plain = Chip();
        var withGlyph = Chip(UIStyle.GlyphClose);
        plain.SetCount(1);
        withGlyph.SetCount(1);
        float small = plain.Width;

        plain.SetCount(1234);

        Assert.Greater(plain.Width, small, "больше цифр — шире чип, и соседей двигает тот, кто их раскладывает");
        Assert.Greater(withGlyph.Width, small, "значок берёт своё место перед текстом");
        var glyph = withGlyph.Rect.Find(FilterChip.GlyphNode).GetComponent<TMP_Text>();
        Assert.AreEqual(UIStyle.TextError, glyph.color, "значок уровня — цветом уровня");
        Assert.AreEqual(UIStyle.GlyphClose, glyph.text);
    }

    [Test]
    public void Clicking_RunsTheHandler()
    {
        int clicks = 0;
        var chip = Chip(onClick: () => clicks++);

        chip.Button.onClick.Invoke();

        Assert.AreEqual(1, clicks);
    }

    [Test]
    public void ABadge_HugsItsText_AndTakesItsColoursFromTheCaller()
    {
        var row = UIFactory.CreateRect("Row", _canvas!.transform);
        var badge = RowBadge.Create(row, "newer", "новее программы", UIStyle.TextWarning, UIStyle.BadgeWarningFill);

        Assert.AreEqual(UIStyle.BadgeH, badge.sizeDelta.y);
        Assert.AreEqual(UIStyle.BadgeWarningFill, badge.GetComponent<Image>().color);
        var label = badge.GetComponentInChildren<TMP_Text>();
        Assert.AreEqual(UIStyle.TextWarning, label.color);
        Assert.AreEqual(UIStyle.FontCaption, (int)label.fontSize);
        Assert.AreEqual(Mathf.Ceil(label.GetPreferredValues("новее программы").x) + 2f * UIStyle.BadgePadX,
            badge.sizeDelta.x, 0.5f, "ширина метки — текст и поля по токену");
        Assert.IsFalse(badge.GetComponent<Image>().raycastTarget, "метка не перехватывает клик строки");
    }
}
