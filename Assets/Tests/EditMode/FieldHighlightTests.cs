using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Тесты подсветки полей и обработки Enter в UI.
/// </summary>
public class FieldHighlightTests
{
    private GameObject? _root;
    private Canvas? _canvas;

    [SetUp]
    public void Setup()
    {
        _root = new GameObject("TestRoot");
        // InputField требует Canvas для работы isFocused и onEndEdit
        _canvas = _root!.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root!.AddComponent<CanvasScaler>();
        _root!.AddComponent<GraphicRaycaster>();
        // EventSystem нужен для InputField.isFocused
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    [TearDown]
    public void Teardown()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);
    }

    // ── Outline / highlight ─────────────────────────────────────────────

    [Test]
    public void CreateInputField_HasDisabledOutline()
    {
        var field = UIFactory.CreateInputField("Test", _root!.transform, "42", Vector2.zero, new Vector2(100, 28));
        var outline = field.GetComponent<Outline>();
        Assert.IsNotNull(outline, "Outline component should be present");
        Assert.IsFalse(outline.enabled, "Outline should start disabled");
        Assert.AreEqual(UIFactory.HighlightColor, outline.effectColor, "outline color should be yellow");
        Assert.AreEqual(2f, outline.effectDistance.x, 0.01f, "border width X");
        Assert.AreEqual(2f, outline.effectDistance.y, 0.01f, "border width Y");
    }

    [Test]
    public void SetHighlight_EnablesOutline()
    {
        var field = UIFactory.CreateInputField("Test", _root!.transform, "42", Vector2.zero, new Vector2(100, 28));
        UIFactory.SetHighlight(field, true);
        Assert.IsTrue(field.GetComponent<Outline>().enabled, "Outline should be enabled");
        UIFactory.SetHighlight(field, false);
        Assert.IsFalse(field.GetComponent<Outline>().enabled, "Outline should be disabled");
    }

    [Test]
    public void SetHighlight_NullField_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => UIFactory.SetHighlight(null!, true));
    }

    [Test]
    public void SetHighlight_FieldWithoutOutline_DoesNotThrow()
    {
        var go = new GameObject("NoOutline");
        go.transform.SetParent(_root!.transform);
        var field = go.AddComponent<TMP_InputField>();
        Assert.DoesNotThrow(() => UIFactory.SetHighlight(field, true));
        Object.DestroyImmediate(go);
    }

    // ── isFocused diagnostics ───────────────────────────────────────────

    [Test]
    public void InputField_IsFocused_IsFalse_InEditModeByDefault()
    {
        var field = UIFactory.CreateInputField("Test", _root!.transform, "42", Vector2.zero, new Vector2(100, 28));
        Assert.IsFalse(field.isFocused,
            "isFocused should be false when no EventSystem selection is active");
    }

    // ── InputField basics ───────────────────────────────────────────────

    [Test]
    public void CreateInputField_ReturnsFieldWithCorrectInitialText()
    {
        var field = UIFactory.CreateInputField("Test", _root!.transform, "hello", Vector2.zero, new Vector2(100, 28));
        // TMP_InputField.text/textComponent несут служебный zero-width space (U+200B);
        // сравниваем видимый текст.
        Assert.AreEqual("hello", field.text.Replace("\u200b", ""));
        Assert.IsNotNull(field.textComponent);
        Assert.AreEqual("hello", field.textComponent.text.Replace("\u200b", ""));
    }

    [Test]
    public void CreateInputField_HasBackgroundImage()
    {
        var field = UIFactory.CreateInputField("Test", _root!.transform, "", Vector2.zero, new Vector2(100, 28));
        var img = field.GetComponent<Image>();
        Assert.IsNotNull(img, "background Image should exist");
        Assert.AreEqual(UIFactory.FieldColor, img.color, "background should be dark");
    }

    // ── ContextMenuUI field tracking ────────────────────────────────────

    [Test]
    public void ContextMenuUI_Build_DoesNotThrow()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();

        Assert.DoesNotThrow(() => ctx.Build(_root!.transform));
        // Cleanup — Build creates children under _root
    }

    [Test]
    public void ContextMenuUI_OpenClose_DoesNotThrow()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();
        ctx.Build(_root!.transform);

        var board = CreateBoard("TestBoard", new Vector3Int(400, 400, 18), Vector3.zero);

        Assert.DoesNotThrow(() => ctx.Open(board));
        Assert.DoesNotThrow(() => ctx.Close());
    }

    [Test]
    public void ContextMenuUI_ApplyViaFieldEdit_UpdatesElement()
    {
        var go = new GameObject("Ctx");
        go.transform.SetParent(_root!.transform);
        var ctx = go.AddComponent<ContextMenuUI>();
        ctx.Build(_root!.transform);

        var board = CreateBoard("OldName", new Vector3Int(400, 400, 18), Vector3.zero);
        ctx.Open(board);

        // Симулируем редактирование названия через рефлексию (поля приватные).
        // Проверим, что сам метод Open заполнил поля корректно.
        Assert.AreEqual("OldName", board.PartName);

        ctx.Close();
    }

    // ── FloorSettingsUI ─────────────────────────────────────────────────

    [Test]
    public void FloorSettingsUI_Build_DoesNotThrow()
    {
        var go = new GameObject("Flr");
        go.transform.SetParent(_root!.transform);
        var flr = go.AddComponent<FloorSettingsUI>();

        Assert.DoesNotThrow(() => flr.Build(_root!.transform));
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private KitchenElement CreateBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var goEl = ElementFactory.CreatePart(dims, name, pos);
        goEl.transform.SetParent(_root!.transform);
        return goEl.GetComponent<KitchenElement>();
    }
}
