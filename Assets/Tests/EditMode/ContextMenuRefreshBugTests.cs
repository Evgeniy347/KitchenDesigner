using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Тесты на баг: при внешнем изменении элемента (ресайз через ручки, переименование)
/// поля в ContextMenuUI не обновляются — нужна пере-выборка элемента.
/// </summary>
public class ContextMenuRefreshBugTests
{
    private GameObject? _canvasGo;
    private GameObject? _ctxGo;
    private ContextMenuUI? _ctx;
    private KitchenElement? _element;

    [SetUp]
    public void Setup()
    {
        _canvasGo = new GameObject("Canvas");
        var canvas = _canvasGo!.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGo!.AddComponent<CanvasScaler>();
        _canvasGo!.AddComponent<GraphicRaycaster>();

        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        _ctxGo = new GameObject("CtxMenu");
        _ctxGo!.transform.SetParent(_canvasGo!.transform);
        _ctx = _ctxGo!.AddComponent<ContextMenuUI>();
        _ctx!.Build(_canvasGo!.transform);

        _element = CreateBoard("Test", new Vector3Int(400, 400, 18), Vector3.zero);
        _ctx!.Open(_element);
    }

    [TearDown]
    public void Teardown()
    {
        if (_ctx != null && _element != null)
            _ctx!.Close();

        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    // ── БАГ: поля размеров не обновляются при внешнем ресайзе ───────────

    [Test]
    public void Dimensions_DoNotUpdate_WhenElementResizedExternally()
    {
        _element!.DimensionsMM = new Vector3Int(800, 600, 18);
        CallRefreshTransformFields();

        Assert.AreEqual("800", FieldText("_w"),
            $"BUG: width stays '{FieldText("_w")}' instead of '800' after external resize");
        Assert.AreEqual("600", FieldText("_h"),
            $"BUG: height stays '{FieldText("_h")}' instead of '600' after external resize");
    }

    [Test]
    public void Dimensions_InitiallyCorrectAfterOpen()
    {
        Assert.AreEqual("400", FieldText("_w"), "initial width");
        Assert.AreEqual("400", FieldText("_h"), "initial height");
        Assert.AreEqual("18", FieldText("_d"), "initial depth");
    }

    [Test]
    public void Position_Updates_WhenElementMovedExternally()
    {
        _element!.transform.position = new Vector3(1.5f, 2.5f, 3.5f);
        CallRefreshTransformFields();

        // Сравниваем численно — локаль ОС может давать запятую вместо точки
        Assert.AreEqual(1.5f, float.Parse(FieldText("_x")), 0.001f,
            $"x field: expected 1.5, got '{FieldText("_x")}'");
        Assert.AreEqual(2.5f, float.Parse(FieldText("_y")), 0.001f,
            $"y field: expected 2.5, got '{FieldText("_y")}'");
        Assert.AreEqual(3.5f, float.Parse(FieldText("_z")), 0.001f,
            $"z field: expected 3.5, got '{FieldText("_z")}'");
    }

    [Test]
    public void Name_DoesNotUpdate_WhenElementRenamedExternally()
    {
        _element!.PartName = "NewName";
        CallRefreshTransformFields();

        Assert.AreEqual("NewName", FieldText("_name"),
            $"BUG: name stays '{FieldText("_name")}' instead of 'NewName' after rename");
    }

    [Test]
    public void Radius_DoesNotUpdate_WhenRadialShelfResizedExternally()
    {
        _ctx!.Close();
        DestroyAllElements();

        var shelfGo = ElementFactory.CreateRadialShelf(300, 18, "Shelf", Vector3.zero);
        shelfGo.transform.SetParent(_canvasGo!.transform);
        var shelf = shelfGo.GetComponent<RadialShelfElement>();
        _ctx!.Open(shelf);

        Assert.AreEqual("300", FieldText("_radius"), "initial radius");

        shelf.Radius = 500;
        CallRefreshTransformFields();

        Assert.AreEqual("500", FieldText("_radius"),
            $"BUG: radius stays '{FieldText("_radius")}' instead of '500'");
    }

    [Test]
    public void Gaps_DoNotUpdate_WhenFacadeGapsChangedExternally()
    {
        _ctx!.Close();
        DestroyAllElements();

        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "Facade", Vector3.zero,
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        facadeGo.transform.SetParent(_canvasGo!.transform);
        var facade = facadeGo.GetComponent<FacadeElement>();
        _ctx!.Open(facade);

        facade.GapLeft = 10;
        facade.GapRight = 20;
        facade.GapTop = 30;
        facade.GapBottom = 40;
        CallRefreshTransformFields();

        Assert.AreEqual("10", FieldText("_gapLeft"),
            $"BUG: gapLeft stays '{FieldText("_gapLeft")}' instead of '10'");
        Assert.AreEqual("20", FieldText("_gapRight"),
            $"BUG: gapRight stays '{FieldText("_gapRight")}' instead of '20'");
        Assert.AreEqual("30", FieldText("_gapTop"),
            $"BUG: gapTop stays '{FieldText("_gapTop")}' instead of '30'");
        Assert.AreEqual("40", FieldText("_gapBottom"),
            $"BUG: gapBottom stays '{FieldText("_gapBottom")}' instead of '40'");
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private void CallRefreshTransformFields()
    {
        var method = typeof(ContextMenuUI).GetMethod("RefreshTransformFields",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "RefreshTransformFields method should exist");
        method.Invoke(_ctx, null);
    }

    private string FieldText(string fieldName)
    {
        var field = typeof(ContextMenuUI).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' should exist");
        var inputField = field.GetValue(_ctx) as TMP_InputField;
        Assert.IsNotNull(inputField, $"Field '{fieldName}' should be a TMP_InputField");
        // TMP_InputField.text содержит служебный zero-width space (U+200B) —
        // приложение читает «чистое» значение отдельно, тесты сравнивают видимый текст.
        return inputField!.text.Replace("\u200b", "");
    }

    private KitchenElement CreateBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<KitchenElement>();
    }

    private void DestroyAllElements()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
    }
}
