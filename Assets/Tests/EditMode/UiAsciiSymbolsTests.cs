using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using TMPro;

/// <summary>
/// Гарантирует, что UI-элементы, отрисовываемые шрифтом LiberationSans SDF,
/// используют только ASCII-символы. Unicode-стрелки и иконки отсутствуют в SDF,
/// поэтому в WebGL вызывают warning и заменяются на □.
/// </summary>
public class UiAsciiSymbolsTests
{
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
    public void UIFactory_DropdownArrow_IsAsciiV()
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
        Assert.AreEqual("v", text.text, "dropdown arrow uses ASCII 'v'");

        Object.DestroyImmediate(canvas.gameObject);
    }

    [Test]
    public void FloorSettingsUI_CloseButton_IsAsciiX()
    {
        var canvas = CreateCanvas("FloorSettingsAsciiCanvas");
        var ui = canvas.gameObject.AddComponent<FloorSettingsUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("FloorSettings");
        Assert.IsNotNull(root, "panel root exists");

        var closeBtn = root.Find("FlrClose");
        Assert.IsNotNull(closeBtn, "close button exists");

        var label = closeBtn.GetComponentInChildren<TMP_Text>(true);
        Assert.IsNotNull(label, "close button has label");
        Assert.AreEqual("X", label.text, "close button uses ASCII 'X'");

        Object.DestroyImmediate(canvas.gameObject);
    }

    [Test]
    public void ContextMenuUI_CloseButton_IsAsciiX()
    {
        var canvas = CreateCanvas("ContextMenuAsciiCanvas");
        var ui = canvas.gameObject.AddComponent<ContextMenuUI>();
        ui.Build(canvas.transform);

        var root = canvas.transform.Find("ContextMenu");
        Assert.IsNotNull(root, "panel root exists");

        var closeBtn = root.Find("CtxClose");
        Assert.IsNotNull(closeBtn, "close button exists");

        var label = closeBtn.GetComponentInChildren<TMP_Text>(true);
        Assert.IsNotNull(label, "close button has label");
        Assert.AreEqual("X", label.text, "close button uses ASCII 'X'");

        Object.DestroyImmediate(canvas.gameObject);
    }
}
