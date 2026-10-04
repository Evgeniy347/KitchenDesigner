using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;

/// <summary>M9/addendum#3 (review-ui-mcp, review-persistence): <c>KitchenElement.LevelId</c>
/// была помечена <c>[NotUndoable("... идёт своей командой MoveToLevelCommand")]</c>, а эта
/// команда была объявлена и покрыта тестами (<see cref="LevelCommandsTests"/>), но ни одна
/// строка UI её не вызывала — переместить деталь на другой этаж можно было только через MCP
/// (edit_elements level_id), напрямую в обход команды и без отмены. Строка «Этаж» в панели
/// свойств — тот самый недостающий путь.</summary>
public class ContextMenuLevelSectionTests
{
    private GameObject? _root;
    private ContextMenuUI? _menu;
    private KitchenElement? _element;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("CtxLevelRoot");
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _root.AddComponent<CanvasScaler>();
        _root.AddComponent<GraphicRaycaster>();

        var go = new GameObject("Ctx");
        go.transform.SetParent(_root.transform);
        _menu = go.AddComponent<ContextMenuUI>();
        _menu.Build(_root.transform);

        LevelRegistry.Set(new[]
        {
            new Level("1", "1 этаж", 0, 3000),
            new Level("2", "2 этаж", 3000, 3000),
        });
        CommandStack.Clear();

        var part = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero);
        _element = part.GetComponent<KitchenElement>();
        _element.LevelId = "1";
        PartRegistry.Register(_element);
    }

    [TearDown]
    public void TearDown()
    {
        _menu?.Close();
        if (_element != null) PartRegistry.Unregister(_element);
        if (_element != null) Object.DestroyImmediate(_element.gameObject);
        if (_root != null) Object.DestroyImmediate(_root);
        PartRegistry.Clear();
        CommandStack.Clear();
        LevelRegistry.Reset();
    }

    // Панель контекстного меню строится как ребёнок КАНВАСА (_root), а не GameObject'а
    // самого _menu — UIFactory.CreatePanel в Build(canvas) вешает "ContextMenu" на canvas
    // напрямую, поэтому обход дерева ведётся от _root, а не от _menu.
    private TMP_Dropdown LevelDropdown() =>
        _root!.GetComponentsInChildren<TMP_Dropdown>(true).First(d => d.gameObject.name == "CtxLevel");

    [Test]
    public void Open_ShowsTheElementsCurrentLevel_Selected()
    {
        _element!.LevelId = "2";
        _menu!.Open(_element);

        var dropdown = LevelDropdown();
        Assert.AreEqual("2 этаж", dropdown.options[dropdown.value].text);
    }

    [Test]
    public void PickingAnotherLevel_MovesTheElement_InOneUndoStep_AndUndoRestoresIt()
    {
        _menu!.Open(_element!);
        var dropdown = LevelDropdown();
        int targetIndex = Enumerable.Range(0, dropdown.options.Count)
            .First(i => dropdown.options[i].text == "2 этаж");

        dropdown.value = targetIndex;

        Assert.AreEqual("2", _element!.LevelId,
            "выбор в строке «Этаж» обязан реально перевести деталь на другой уровень");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "перевод на этаж обязан лечь в стек РОВНО одним шагом (MoveToLevelCommand)");

        CommandStack.Undo();
        Assert.AreEqual("1", _element.LevelId, "отмена обязана вернуть деталь на прежний этаж");
    }
}
