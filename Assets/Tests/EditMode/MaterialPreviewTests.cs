using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Строка «Текстура» в меню свойств: высота пункта под длинные
/// названия и предпросмотр декора наведением.</summary>
public class MaterialPreviewTests
{
    private GameObject? _canvasGo;
    private GameObject? _ctxGo;
    private ContextMenuUI? _ctx;
    private SelectionManager? _selection;
    private KitchenElement? _element;

    [SetUp]
    public void Setup()
    {
        IgnoreMaterialLeakLog();
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

        var smGo = new GameObject("SelectionManager");
        smGo.transform.SetParent(_canvasGo!.transform);
        _selection = smGo.AddComponent<SelectionManager>();
        // В EditMode Awake не вызывается, а меню ищет менеджер через Instance.
        SetSelectionInstance(_selection);

        _ctxGo = new GameObject("CtxMenu");
        _ctxGo!.transform.SetParent(_canvasGo!.transform);
        _ctx = _ctxGo!.AddComponent<ContextMenuUI>();
        _ctx!.Build(_canvasGo!.transform);

        _element = CreateBoard("Деталь", new Vector3Int(400, 400, 18), Vector3.zero);
        MaterialManager.ApplyById(_element!, "white");
        _ctx!.Open(_element!);
    }

    [TearDown]
    public void Teardown()
    {
        IgnoreMaterialLeakLog();
        if (_ctx != null) _ctx!.Close();
        SetSelectionInstance(null);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es != null) Object.DestroyImmediate(es.gameObject);

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        LogAssert.ignoreFailingMessages = false;
    }

    // ── Высота пункта: перенос длинных названий ────────────────────────

    [Test]
    public void LinesFor_ShortName_FitsOneLine()
    {
        Assert.AreEqual(1, DropdownItemFit.LinesFor("Венге", 168f, 14));
    }

    [Test]
    public void LinesFor_LongName_WrapsToSeveralLines()
    {
        // 45 символов при ~23 символах в строке — заведомо больше одной.
        int lines = DropdownItemFit.LinesFor(
            "Дуб давенпорт натуральный светлый (H3359 ST32)", 168f, 14);
        Assert.GreaterOrEqual(lines, 2, "длинное название обязано переноситься");
    }

    [Test]
    public void LinesFor_WrapsByWords_NotByCharacters()
    {
        // Ширина ровно под «Дуб каселла» + пробел: следующее слово уходит вниз
        // целиком, а не рвётся посередине.
        const float width = 11 * 14 * DropdownItemFit.GlyphWidthFactor;
        Assert.AreEqual(2, DropdownItemFit.LinesFor("Дуб каселла натуральный", width, 14));
    }

    [Test]
    public void LinesFor_NeverExceedsMaxLines()
    {
        int lines = DropdownItemFit.LinesFor(new string('я', 500), 100f, 14);
        Assert.AreEqual(DropdownItemFit.MaxLines, lines,
            "пункт не должен расти без предела");
    }

    [Test]
    public void HeightFor_LongestOptionDecidesHeight()
    {
        var options = new List<string> { "Венге", "Дуб давенпорт натуральный светлый (H3359 ST32)" };
        float tall = DropdownItemFit.HeightFor(options, 168f, 14);
        float shortOnly = DropdownItemFit.HeightFor(new List<string> { "Венге" }, 168f, 14);

        Assert.AreEqual(UIStyle.DropdownItemH, shortOnly,
            "короткие названия не должны раздувать список");
        Assert.Greater(tall, shortOnly,
            "высота пункта считается по САМОМУ длинному названию");
    }

    [Test]
    public void FitDropdownItems_GrowsItem_ForLongOptions()
    {
        var root = new GameObject("FitRoot");
        try
        {
            var dd = UIFactory.CreateDropdown("Fit", root.transform,
                new List<string> { "А", "Б" }, Vector2.zero, new Vector2(202, 28), _ => { });
            var item = ItemRect(dd);
            Assert.AreEqual(UIStyle.DropdownItemH, item.sizeDelta.y,
                "короткий список — пункт в одну строку");

            dd.options = new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData("Дуб давенпорт натуральный светлый (H3359 ST32)"),
            };
            UIFactory.FitDropdownItems(dd);

            Assert.Greater(item.sizeDelta.y, UIStyle.DropdownItemH,
                "BUG: длинное название не влезает — пункт остался в одну строку");
            Assert.AreEqual(item.sizeDelta.y + 2f, ContentRect(dd).sizeDelta.y,
                "контент списка должен идти за высотой пункта");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void MaterialDropdown_ItemWraps_AndIsTallerThanOneLine()
    {
        var dd = Dropdown("_materialDropdown");
        var label = dd.itemText;

        Assert.IsTrue(label.enableWordWrapping,
            "BUG: в пункте выключен перенос — длинное название обрежется");
        Assert.Greater(ItemRect(dd).sizeDelta.y, UIStyle.DropdownItemH,
            "BUG: пункт списка «Текстура» не подрос под длинные названия декоров");
    }

    [Test]
    public void MaterialDropdown_Caption_StaysSingleLine()
    {
        // Свёрнутая строка — фиксированной высоты: там перенос ломал бы раскладку.
        var caption = Dropdown("_materialDropdown").captionText;
        Assert.IsFalse(caption.enableWordWrapping);
        Assert.AreEqual(TextOverflowModes.Ellipsis, caption.overflowMode);
    }

    // ── Предпросмотр наведением ────────────────────────────────────────

    [Test]
    public void Hover_ShowsMaterialOnElement()
    {
        IgnoreMaterialLeakLog();
        Preview(legs: false, id: "oak");

        Assert.AreEqual("oak", _element!.MaterialId,
            "BUG: наведение на пункт не показало декор на объекте");
        Assert.IsTrue(_ctx!.MaterialPreviewActive);
    }

    [Test]
    public void PointerAway_RestoresPreviousMaterial()
    {
        IgnoreMaterialLeakLog();
        Preview(legs: false, id: "oak");
        EndPreview();

        Assert.AreEqual("white", _element!.MaterialId,
            "BUG: курсор ушёл с пункта, а показанный декор остался");
        Assert.IsFalse(_ctx!.MaterialPreviewActive);
    }

    [Test]
    public void Hover_OverSeveralItems_RestoresTheOriginalOne()
    {
        IgnoreMaterialLeakLog();
        Preview(legs: false, id: "oak");
        Preview(legs: false, id: "wenge");
        EndPreview();

        Assert.AreEqual("white", _element!.MaterialId,
            "BUG: «до» перезаписалось соседним пунктом — вернулся не исходный декор");
    }

    [Test]
    public void Hover_RemovesSelectionHighlight()
    {
        IgnoreMaterialLeakLog();
        Assert.IsTrue(_selection!.IsSelected(_element!), "элемент выделен при открытии меню");

        Preview(legs: false, id: "oak");

        Assert.IsTrue(_selection!.IsHighlightSuppressed(_element!),
            "BUG: подсветка выделения осталась и портит вид текстуры");
        Assert.IsTrue(_selection!.IsSelected(_element!),
            "выделение снимать нельзя — снимается только его подсветка");
    }

    [Test]
    public void PointerAway_ReturnsSelectionHighlight()
    {
        IgnoreMaterialLeakLog();
        Preview(legs: false, id: "oak");
        EndPreview();

        Assert.IsFalse(_selection!.IsHighlightSuppressed(_element!),
            "BUG: подсветка не вернулась после ухода курсора");
        Assert.IsTrue(_selection!.HasSavedMaterialFor(_element!),
            "подсветка снова надета на элемент");
    }

    [Test]
    public void Choice_KeepsMaterial_WhenPointerLeavesAfterClick()
    {
        IgnoreMaterialLeakLog();
        // Клик по пункту: сначала onValueChanged, и только потом список
        // закрывается и DropdownHover шлёт onExit. Если выбор не гасит
        // предпросмотр, этот onExit вернёт декор, который был ДО наведения.
        Preview(legs: false, id: "oak");
        Choose(legs: false, id: "wenge");
        EndPreview();

        Assert.AreEqual("wenge", _element!.MaterialId,
            "BUG: выбранный декор откатился уходом курсора после клика");
        Assert.IsFalse(_ctx!.MaterialPreviewActive);
    }

    [Test]
    public void Choice_ReturnsSelectionHighlight()
    {
        IgnoreMaterialLeakLog();
        Preview(legs: false, id: "oak");
        Choose(legs: false, id: "wenge");

        Assert.IsFalse(_selection!.IsHighlightSuppressed(_element!),
            "BUG: после выбора текстуры выделение не вернулось");
        Assert.IsTrue(_selection!.HasSavedMaterialFor(_element!));
    }

    [Test]
    public void Close_EndsPreview()
    {
        IgnoreMaterialLeakLog();
        Preview(legs: false, id: "oak");
        _ctx!.Close();

        Assert.AreEqual("white", _element!.MaterialId,
            "BUG: меню закрыли с раскрытым списком — показанный декор пережил закрытие");
        Assert.IsFalse(_ctx!.MaterialPreviewActive);
    }

    [Test]
    public void OpenOtherElement_EndsPreviewOnPreviousOne()
    {
        IgnoreMaterialLeakLog();
        var other = CreateBoard("Деталь2", new Vector3Int(400, 400, 18), new Vector3(1f, 0f, 0f));
        MaterialManager.ApplyById(other, "white");

        Preview(legs: false, id: "oak");
        _ctx!.Open(other);

        Assert.AreEqual("white", _element!.MaterialId,
            "BUG: предпросмотр остался на прошлом элементе насовсем");
    }

    [Test]
    public void Preview_DoesNotTouchUndoStack()
    {
        IgnoreMaterialLeakLog();
        CommandStack.Clear();
        Preview(legs: false, id: "oak");
        Choose(legs: false, id: "wenge");

        // Сам выбор декора отменяться обязан (правило 2 UI-GUIDELINES), а вот
        // показанный наведением «Дуб сонома» — не правка, и своей записи в стеке
        // у него быть не должно: одна команда на весь сценарий, и её отмена
        // возвращает декор, который был ДО наведения, а не предпросмотр.
        Assert.AreEqual(1, CommandStack.UndoCount,
            "предпросмотр — показ, а не правка: в undo-стеке ему делать нечего");

        CommandStack.Undo();
        Assert.AreEqual("white", _element!.MaterialId,
            "отмена обязана вернуть исходный декор, а не предпросмотр");
    }

    [Test]
    public void TableLegs_PreviewAndRestore_TouchOnlyLegs()
    {
        IgnoreMaterialLeakLog();
        _ctx!.Close();
        var tableGo = ElementFactory.CreateTable(new Vector3Int(1200, 750, 700), "Стол", Vector3.zero);
        tableGo.transform.SetParent(_canvasGo!.transform);
        var table = tableGo.GetComponent<TableElement>();
        MaterialManager.ApplyTabletop(table, MaterialCatalog.Get("white"));
        MaterialManager.ApplyLegs(table, MaterialCatalog.Get("white"));
        _ctx!.Open(table);

        Preview(legs: true, id: "oak");
        Assert.AreEqual("oak", table.LegsMaterialId, "BUG: наведение на «Ножки» ничего не показало");
        Assert.AreEqual("white", table.TabletopMaterialId, "столешницу трогать нельзя");

        EndPreview();
        Assert.AreEqual("white", table.LegsMaterialId, "BUG: декор ножек не вернулся");
    }

    // ── helpers ────────────────────────────────────────────────────────

    /// <summary>Подсветка выделения зовёт renderer.material — в EditMode Unity
    /// пишет об этом ошибку («leak materials into the scene»), и тест падает не
    /// по существу. Флаг сбрасывается перед каждым телом теста, поэтому его
    /// ставят и в SetUp, и в самом тесте (так же поступает SelectionManagerTests).</summary>
    private static void IgnoreMaterialLeakLog() => LogAssert.ignoreFailingMessages = true;

    private static void SetSelectionInstance(SelectionManager? sm)
    {
        var prop = typeof(SelectionManager).GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prop, "SelectionManager.Instance должен существовать");
        prop!.GetSetMethod(nonPublic: true)!.Invoke(null, new object?[] { sm });
    }

    private KitchenElement CreateBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(dims, name, pos);
        go.transform.SetParent(_canvasGo!.transform);
        return go.GetComponent<KitchenElement>();
    }

    private static int IndexOfMaterial(string id)
    {
        var all = MaterialCatalog.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i].id == id) return i;
        Assert.Fail($"декор '{id}' отсутствует в каталоге");
        return -1;
    }

    private void Preview(bool legs, string id) => Invoke("PreviewMaterial", legs, IndexOfMaterial(id));

    private void Choose(bool legs, string id) => Invoke("ApplyMaterialChoice", legs, IndexOfMaterial(id));

    private void EndPreview()
    {
        var method = typeof(ContextMenuUI).GetMethod("EndMaterialPreview",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "EndMaterialPreview должен существовать");
        method!.Invoke(_ctx, null);
    }

    private void Invoke(string name, bool legs, int index)
    {
        var method = typeof(ContextMenuUI).GetMethod(name,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{name} должен существовать");
        method!.Invoke(_ctx, new object[] { legs, index });
    }

    private TMP_Dropdown Dropdown(string fieldName)
    {
        var field = typeof(ContextMenuUI).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"поле {fieldName} должно существовать");
        var dd = field!.GetValue(_ctx) as TMP_Dropdown;
        Assert.IsNotNull(dd, $"{fieldName} должен быть TMP_Dropdown");
        return dd!;
    }

    private static RectTransform ItemRect(TMP_Dropdown dd)
    {
        var rt = dd.template.Find("Viewport/Content/Item") as RectTransform;
        Assert.IsNotNull(rt, "шаблон списка должен содержать пункт");
        return rt!;
    }

    private static RectTransform ContentRect(TMP_Dropdown dd)
    {
        var rt = dd.template.Find("Viewport/Content") as RectTransform;
        Assert.IsNotNull(rt, "шаблон списка должен содержать контент");
        return rt!;
    }
}
