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

        // Позиция показывается в мм (правило 1 UI-GUIDELINES): 1,5 м = 1500 мм.
        Assert.AreEqual(1500, int.Parse(FieldText("_x")),
            $"x field: expected 1500 мм, got '{FieldText("_x")}'");
        Assert.AreEqual(2500, int.Parse(FieldText("_y")),
            $"y field: expected 2500 мм, got '{FieldText("_y")}'");
        Assert.AreEqual(3500, int.Parse(FieldText("_z")),
            $"z field: expected 3500 мм, got '{FieldText("_z")}'");
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

        var shelfGo = ElementFactory.CreateRadialShelf(600, 400, 18, 200, "Shelf", Vector3.zero);
        shelfGo.transform.SetParent(_canvasGo!.transform);
        var shelf = shelfGo.GetComponent<RadialShelfElement>();
        _ctx!.Open(shelf);

        Assert.AreEqual("200", FieldText("_radius"), "initial corner radius");

        shelf.CornerRadius = 350;
        CallRefreshTransformFields();

        Assert.AreEqual("350", FieldText("_radius"),
            $"BUG: corner radius stays '{FieldText("_radius")}' instead of '350'");
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

    // ── БАГ: список фасадов ящика не обновляется при открытии дропдауна ──
    // RED: до фикса фасады в дропдауне не обновлялись после Open().
    // GREEN: хук на OnEnable template перестраивает список при каждом открытии.

    [Test]
    public void DrawerFacadeDropdown_Rebuilds_WhenNewFacadeAppears()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Yashik", new Vector3(0f, 0.043f, 0f));
        _ctx!.Open(drawer);

        var dd = GetDrawerFacadeDropdown();
        Assert.AreEqual(1, dd.options.Count, "только '(нет фасада)' до появления фасадов");

        // Создаём фасад в контакте с ящиком (позиция из DrawerFacadeContactTests).
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(400, 86, 18), "F1", new Vector3(0f, 0.043f, 0.184f));
        facadeGo.transform.SetParent(_canvasGo!.transform);

        // Вызываем RebuildDrawerFacadeOptions напрямую — именно это делает хук.
        CallRebuildDrawerFacadeOptions();

        Assert.AreEqual(2, dd.options.Count,
            "BUG: новый фасад не появился в списке после обновления дропдауна");
        Assert.AreEqual("F1", dd.options[1].text);
    }

    [Test]
    public void OrphanedFacade_StaysInList_WhenFacadeDestroyed()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Yashik", new Vector3(0f, 0.043f, 0f));
        CreateFacade("F1", new Vector3(0f, 0.043f, 0.184f));

        drawer.AttachedFacadeName = "F1";
        _ctx!.Open(drawer);

        var dd = GetDrawerFacadeDropdown();
        Assert.AreEqual(2, dd.options.Count, "опции: '(нет фасада)' + 'F1'");
        Assert.AreEqual("F1", dd.options[1].text);

        // Удаляем фасад из сцены.
        var facadeEl = FindElementByName("F1");
        Assert.IsNotNull(facadeEl, "фасад F1 должен существовать");
        Object.DestroyImmediate(facadeEl!.gameObject);
        PartRegistry.Clear(); // гарантия, что в реестре чисто

        // Перестраиваем список — осиротевший фасад должен остаться.
        CallRebuildDrawerFacadeOptions();

        Assert.AreEqual(2, dd.options.Count,
            "BUG: осиротевший фасад исчез из списка после удаления");
        Assert.AreEqual("F1", dd.options[1].text,
            "BUG: имя осиротевшего фасада не сохранилось в списке");
    }

    [Test]
    public void OrphanedFacade_CaptionTurnsRed()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Yashik", new Vector3(0f, 0.043f, 0f));
        CreateFacade("F1", new Vector3(0f, 0.043f, 0.184f));

        drawer.AttachedFacadeName = "F1";
        _ctx!.Open(drawer);

        // Удаляем фасад — он становится осиротевшим.
        var facadeEl = FindElementByName("F1");
        Assert.IsNotNull(facadeEl);
        Object.DestroyImmediate(facadeEl!.gameObject);
        PartRegistry.Clear();

        // Перестраиваем и устанавливаем значение — caption должен стать красным.
        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("F1");

        var dd = GetDrawerFacadeDropdown();
        Assert.IsNotNull(dd.captionText, "captionText should exist");
        Assert.AreEqual(Color.red, dd.captionText.color,
            "BUG: caption осиротевшего фасада не покраснел");
    }

    [Test]
    public void ValidFacadeInContact_CaptionUsesNormalColor()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Yashik", new Vector3(0f, 0.043f, 0f));
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(400, 86, 18), "F1", new Vector3(0f, 0.043f, 0.184f));
        facadeGo.transform.SetParent(_canvasGo!.transform);

        drawer.AttachedFacadeName = "F1";
        _ctx!.Open(drawer);

        var dd = GetDrawerFacadeDropdown();
        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("F1");

        Assert.AreEqual(UIStyle.Text, dd.captionText.color,
            "BUG: caption валидного фасада не использует нормальный цвет UIStyle.Text");
    }

    [Test]
    public void DrawerFacadeDropdown_ClearingResetsColor()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Yashik", new Vector3(0f, 0.043f, 0f));
        CreateFacade("F1", new Vector3(0f, 0.043f, 0.184f));

        drawer.AttachedFacadeName = "F1";
        _ctx!.Open(drawer);

        // Удаляем фасад — осиротел.
        var facadeEl = FindElementByName("F1");
        Assert.IsNotNull(facadeEl);
        Object.DestroyImmediate(facadeEl!.gameObject);
        PartRegistry.Clear();

        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("F1");

        var dd = GetDrawerFacadeDropdown();
        Assert.AreEqual(Color.red, dd.captionText.color, "caption должен быть красным");

        // Сбрасываем выбор на «(нет фасада)».
        CallOnDrawerFacadeSelected(0);

        Assert.AreEqual(UIStyle.Text, dd.captionText.color,
            "BUG: после сброса на '(нет фасада)' caption не вернулся к нормальному цвету");
    }

    [Test]
    public void FacadeMovedAway_CaptionTurnsRed()
    {
        _ctx!.Close();
        DestroyAllElements();

        var drawer = CreateDrawer("Yashik", new Vector3(0f, 0.043f, 0f));
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(400, 86, 18), "F1", new Vector3(0f, 0.043f, 0.184f));
        facadeGo.transform.SetParent(_canvasGo!.transform);

        drawer.AttachedFacadeName = "F1";
        _ctx!.Open(drawer);

        // Отодвигаем фасад далеко — он больше не в контакте, но в реестре есть.
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.transform.position = new Vector3(10f, 0.043f, 0.184f);

        CallRebuildDrawerFacadeOptions();
        CallSetDrawerFacadeValue("F1");

        var dd = GetDrawerFacadeDropdown();
        Assert.AreEqual(Color.red, dd.captionText.color,
            "BUG: фасад отодвинут (не в контакте), но caption не красный");
    }

    // ── helpers (existing) ──────────────────────────────────────────────

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

    // ── drawer facade helpers ───────────────────────────────────────────

    private DrawerElement CreateDrawer(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateDrawer(DrawerType.A, 350, DrawerColor.Anthracite, 400, name, pos);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<DrawerElement>();
    }

    private FacadeElement CreateFacade(string name, Vector3 pos)
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(400, 86, 18), name, pos, 2, 2, 2, 2);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<FacadeElement>();
    }

    private KitchenElement? FindElementByName(string name)
    {
        foreach (var el in PartRegistry.GetAll())
            if (el.PartName == name) return el;
        return null;
    }

    private TMP_Dropdown GetDrawerFacadeDropdown()
    {
        var field = typeof(ContextMenuUI).GetField("_drawerFacadeDropdown",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "_drawerFacadeDropdown field should exist");
        var dd = field.GetValue(_ctx) as TMP_Dropdown;
        Assert.IsNotNull(dd, "_drawerFacadeDropdown should be a TMP_Dropdown");
        return dd!;
    }

    private void CallRebuildDrawerFacadeOptions()
    {
        var method = typeof(ContextMenuUI).GetMethod("RebuildDrawerFacadeOptions",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "RebuildDrawerFacadeOptions method should exist");
        method.Invoke(_ctx, null);
    }

    private void CallSetDrawerFacadeValue(string name)
    {
        var method = typeof(ContextMenuUI).GetMethod("SetDrawerFacadeValue",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "SetDrawerFacadeValue method should exist");
        method.Invoke(_ctx, new object[] { name });
    }

    private void CallOnDrawerFacadeSelected(int index)
    {
        var method = typeof(ContextMenuUI).GetMethod("OnDrawerFacadeSelected",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "OnDrawerFacadeSelected method should exist");
        method.Invoke(_ctx, new object[] { index });
    }
}
