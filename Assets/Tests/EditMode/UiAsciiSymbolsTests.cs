using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using TMPro;

/// <summary>
/// Гарантирует, что глифы UI отрисовываются шрифтом LiberationSans: символы,
/// которых нет в ttf, в WebGL вызывают warning и заменяются на □. Стрелки/
/// крестики берутся из UIStyle и обязаны присутствовать в шрифте (проверено —
/// × ▼ ► входят в LiberationSans; ✓/✕ отсутствуют и заменены на Image-галочку).
/// </summary>
public class UiAsciiSymbolsTests
{
    private static bool FontHasGlyph(string s)
    {
        var font = UIFactory.FontAsset;
        Assert.IsNotNull(font, "TMP font asset should be available");
        font!.TryAddCharacters(s, out string missing);
        return string.IsNullOrEmpty(missing);
    }

    [Test]
    public void UIStyleGlyphs_PresentInFont()
    {
        Assert.IsTrue(FontHasGlyph(UIStyle.GlyphClose), "× must render in LiberationSans");
        Assert.IsTrue(FontHasGlyph(UIStyle.GlyphExpanded), "▼ must render in LiberationSans");
        Assert.IsTrue(FontHasGlyph(UIStyle.GlyphCollapsed), "► must render in LiberationSans");
    }

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name);
        return go.AddComponent<Canvas>();
    }

    [Test]
    public void FacadeDoor_Symbol_EveryMode_IsAsciiSingleChar()
    {
        foreach (DoorMode mode in (DoorMode[])System.Enum.GetValues(typeof(DoorMode)))
        {
            var symbol = FacadeDoor.Symbol(mode);
            Assert.AreEqual(1, symbol.Length, $"symbol for {mode} must be single char");
            Assert.Less(symbol[0], 128, $"symbol for {mode} must be ASCII, got '{symbol}'");
        }
    }

    [Test]
    public void UIFactory_DropdownArrow_UsesDropdownGlyph()
    {
        var canvas = CreateCanvas("AsciiDropdownCanvas");
        var dropdown = UIFactory.CreateDropdown(
            "TestDropdown", canvas.transform,
            new List<string> { "a", "b" },
            Vector2.zero, new Vector2(100, 30), null!);

        var arrow = dropdown.transform.Find("TestDropdown_Arrow");
        Assert.IsNotNull(arrow, "arrow child exists");

        var text = arrow.GetComponent<TMP_Text>();
        Assert.IsNotNull(text, "arrow has TMP_Text");
        Assert.AreEqual(UIStyle.GlyphDropdown, text.text, "dropdown arrow uses the ▼ glyph");

        Object.DestroyImmediate(canvas.gameObject);
    }

    [Test]
    public void DayNightPanelUI_CloseButton_UsesCloseGlyph()
    {
        var canvas = CreateCanvas("DayNightAsciiCanvas");
        var ui = canvas.gameObject.AddComponent<DayNightPanelUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("DayNightPanel");
        Assert.IsNotNull(root, "panel root exists");

        // Стандартная кнопка закрытия называется CloseBtn (UIFactory.CreateCloseButton).
        var closeBtn = root.Find("CloseBtn");
        Assert.IsNotNull(closeBtn, "close button exists");

        var label = closeBtn.GetComponentInChildren<TMP_Text>(true);
        Assert.IsNotNull(label, "close button has label");
        Assert.AreEqual(UIStyle.GlyphClose, label.text, "close button uses the × glyph");

        Object.DestroyImmediate(canvas.gameObject);
    }

    [Test]
    public void ContextMenuUI_CloseButton_UsesCloseGlyph()
    {
        var canvas = CreateCanvas("ContextMenuAsciiCanvas");
        var ui = canvas.gameObject.AddComponent<ContextMenuUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("ContextMenu");
        Assert.IsNotNull(root, "panel root exists");

        var closeBtn = root.Find("CloseBtn");
        Assert.IsNotNull(closeBtn, "close button exists");

        var label = closeBtn.GetComponentInChildren<TMP_Text>(true);
        Assert.IsNotNull(label, "close button has label");
        Assert.AreEqual(UIStyle.GlyphClose, label.text, "close button uses the × glyph");

        Object.DestroyImmediate(canvas.gameObject);
    }
}
