using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

public class UIFactoryContractTests
{
    private GameObject? _canvasGo;

    private Transform Canvas => _canvasGo!.transform;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        var canvas = _canvasGo!.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo!.AddComponent<CanvasScaler>();
    }

    [TearDown]
    public void Teardown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    [Test]
    public void UIFactory_FontAsset_CoversCyrillic()
    {
        var font = UIFactory.FontAsset;

        Assert.IsNotNull(font, "без шрифта весь UI пустой");
        Assert.IsTrue(font!.HasCharacter('ж'),
            "Дефолтный статический атлас LiberationSans SDF из TMP кириллицы НЕ содержит — "
            + "поэтому шрифт сначала берётся из Resources, затем генерируется в рантайме, "
            + "и только потом падает на TMP_Settings.defaultFontAsset");
    }

    [Test]
    public void UIFactory_InteractiveColors_BrightenOnHover_DarkenOnPress_DimWhenDisabled()
    {
        var colors = UIFactory.InteractiveColors();

        Assert.Greater(colors.highlightedColor.grayscale, colors.normalColor.grayscale,
            "hover обязан быть заметным (правило 9 UI-GUIDELINES)");
        Assert.Less(colors.pressedColor.grayscale, colors.normalColor.grayscale,
            "нажатая кнопка выглядит вдавленной, то есть темнее обычной");
        Assert.Less(colors.disabledColor.a, 1f,
            "неактивная кнопка затемнена прозрачностью, иначе её не отличить от рабочей");
    }

    [Test]
    public void UIFactory_DangerButton_IsPaintedWithTheDangerToken()
    {
        var button = UIFactory.CreateDangerButton("Del", Canvas, "Удалить",
            Vector2.zero, new Vector2(120, 32), null);

        Assert.AreEqual(UIStyle.Danger, button.GetComponent<Image>().color,
            "деструктивное действие красное — цвет берётся из палитры UIStyle");
    }

    [Test]
    public void UIFactory_CloseButton_SitsInTheTopRightCorner_WithTheCloseGlyph()
    {
        var btn = UIFactory.CreateCloseButton(Canvas, () => { });
        var rt = btn.GetComponent<RectTransform>();

        Assert.AreEqual(UIStyle.GlyphClose, btn.GetComponentInChildren<TMP_Text>().text,
            "закрытие окна помечается только «×» (правило 4: один глиф — одно значение)");
        Assert.AreEqual(new Vector2(UIStyle.CloseBtnSize, UIStyle.CloseBtnSize), rt.sizeDelta,
            "размер кнопки закрытия один на все окна (правило 7)");
        Assert.AreEqual(new Vector2(1, 1), rt.anchorMax, "правый верхний угол");
        Assert.AreEqual(new Vector2(-UIStyle.CloseBtnInset, -UIStyle.CloseBtnInset),
            rt.anchoredPosition, "отступ от углов один на все окна (правило 7)");
    }

    [Test]
    public void UIFactory_SectionHeader_UsesSecondaryText_AndASeparatorLine()
    {
        var header = UIFactory.CreateSectionHeader("Sec", Canvas, "Кромки", 200f);

        var label = header.Find("Sec_Label")!.GetComponent<TMP_Text>();
        var line = header.Find("Sec_Line")!.GetComponent<Image>();

        Assert.AreEqual(UIStyle.TextSecondary, label.color,
            "подпись секции вторичным цветом — она тише содержимого (правило 6)");
        Assert.AreEqual(UIStyle.Separator, line.color, "линия до правого края — цвет разделителя");
        Assert.Greater(line.rectTransform.offsetMin.x, 0f,
            "линия начинается ПОСЛЕ текста, а не под ним");
    }

    [Test]
    public void UIFactory_IconButton_KeepsTheIconVisible_InALowRowButton()
    {
        var lowRowButton = new Vector2(18f, 13f);

        var wide = UIFactory.CreateIconButton("Up", Canvas, IconFactory.CaretUp,
            Vector2.zero, lowRowButton, () => { });
        var tight = UIFactory.CreateIconButton("Down", Canvas, IconFactory.CaretDown,
            Vector2.zero, lowRowButton, () => { }, iconPaddingBothEdges: 4f);

        float wideIcon = ((RectTransform)wide.transform.Find("Up_Icon")).sizeDelta.x;
        float tightIcon = ((RectTransform)tight.transform.Find("Down_Icon")).sizeDelta.x;

        Assert.AreEqual(UIFactory.MinIconSize, wideIcon,
            "стандартные 12 px отступа в кнопке высотой 13 px не оставляют от иконки ничего — "
            + "остаётся только нижний предел");
        Assert.Greater(tightIcon, wideIcon,
            "поэтому у стрелок порядка в строке списка отступ свой, меньший");
    }

    [Test]
    public void UIFactory_NumberField_KeepsTypedTextClearOfTheUnitSuffix()
    {
        var field = UIFactory.CreateNumberField("W", Canvas, "800",
            Vector2.zero, new Vector2(120, 28), "мм");

        var unit = field.transform.Find("W_Unit")!.GetComponent<TMP_Text>();

        Assert.AreEqual("мм", unit.text,
            "единица измерения живёт ВНУТРИ поля серым суффиксом, а не в подписи (правило 1)");
        Assert.Less(field.textViewport!.offsetMax.x, -unit.GetPreferredValues("мм").x,
            "набранное число не должно заезжать под суффикс");
    }

    [Test]
    public void UIFactory_ErrorHighlight_IsClearedByTheNextSetHighlight()
    {
        var field = UIFactory.CreateNumberField("H", Canvas, "700",
            Vector2.zero, new Vector2(120, 28), "мм");
        var outline = field.GetComponent<Outline>();

        UIFactory.SetErrorHighlight(field);
        Assert.AreEqual(UIStyle.HighlightError, outline.effectColor,
            "невалидный ввод не откатывается молча — поле обводится красным (правило 2)");
        Assert.IsTrue(outline.enabled);

        UIFactory.SetHighlight(field, false);
        Assert.IsFalse(outline.enabled, "красная рамка снимается любым следующим SetHighlight");
        Assert.AreEqual(UIStyle.HighlightChanged, outline.effectColor,
            "и возвращает полю жёлтый цвет «изменено, но не применено»");
    }

    [Test]
    public void UIFactory_Checkmark_IsAnImage_BecauseTheTickGlyphIsOutsideWgl4()
    {
        var box = UIFactory.CreatePanel("Box", Canvas, Vector2.zero, new Vector2(22, 22),
            UIFactory.FieldColor);
        var check = UIFactory.CreateCheckmark("Check", box.transform);

        Assert.IsFalse(Wgl4CharSet.Contains('✓'),
            "✓ (U+2713) вне WGL4, а рантайм-атлас TMP собирается из LiberationSans — "
            + "текстового глифа галочки в нём нет");
        Assert.IsNull(check.GetComponentInChildren<TMP_Text>(),
            "поэтому галочка — картинка, а не буква: «X» вместо неё читалась бы как "
            + "«закрыть/удалить» (правило 4)");
        Assert.AreEqual(UIStyle.Accent, check.color, "заливка акцентным цветом");
    }
}
