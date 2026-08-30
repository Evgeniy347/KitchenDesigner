using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuGrooveSectionTests
{
    private Canvas? _canvas;
    private ContextMenuUI? _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu!.Build(_canvas!.transform);
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 300, 18), name, Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_Dropdown Dd(string node) =>
        Panel().Find(node)!.GetComponent<TMP_Dropdown>();

    private void AddGroove(GrooveSide side, GrooveKind kind)
    {
        Dd("CtxGrooveSide").SetValueWithoutNotify((int)side);
        Dd("CtxGrooveKind").SetValueWithoutNotify((int)kind);
        Panel().Find("CtxGrooveAdd")!.GetComponent<Button>().onClick.Invoke();
    }

    [Test]
    public void GrooveWidgets_KeepTheirNames()
    {
        foreach (var node in new[] { "CtxGrooves", "CtxGrooveHint", "CtxGrooveSide",
                     "CtxGrooveKind", "CtxGrooveAdd", "CtxGrooveSide0", "CtxGrooveKind0",
                     "CtxGrooveDel0" })
            Assert.NotNull(Panel().Find(node), $"виджет {node} ищется тестами по имени");
    }

    [Test]
    public void SideDropdownOptions_FollowTheGrooveSideEnumOrder()
    {
        var options = Dd("CtxGrooveSide").options;
        Assert.AreEqual(System.Enum.GetValues(typeof(GrooveSide)).Length, options.Count,
            "у каждого значения GrooveSide обязан быть пункт: индекс пункта кастуется в enum напрямую");
        Assert.AreEqual("Верх", options[(int)GrooveSide.Top].text,
            "порядок пунктов = порядок значений GrooveSide, иначе «Верх» создало бы паз снизу");
        Assert.AreEqual("Право", options[(int)GrooveSide.Right].text);
    }

    [Test]
    public void KindDropdownOptions_FollowTheGrooveKindEnumOrder()
    {
        var options = Dd("CtxGrooveKind").options;
        Assert.AreEqual(System.Enum.GetValues(typeof(GrooveKind)).Length, options.Count);
        Assert.AreEqual("Сквозной", options[(int)GrooveKind.Through].text,
            "порядок пунктов = порядок значений GrooveKind");
    }

    [Test]
    public void Add_PutsTheGrooveOnTheBoard_AndIsUndoable()
    {
        var board = Board("Полка");
        _menu!.Open(board);

        AddGroove(GrooveSide.Left, GrooveKind.Through);

        Assert.AreEqual(1, board.Grooves.Count, "«Добавить» кладёт паз на деталь");
        Assert.AreEqual(GrooveSide.Left, board.Grooves[0].side);

        CommandStack.Undo();
        Assert.AreEqual(0, board.Grooves.Count,
            "любая правка набора пазов идёт через SetGroovesCommand (правило 2 UI-GUIDELINES)");
    }

    [Test]
    public void Add_SameGrooveTwice_IsRefused()
    {
        var board = Board("Полка");
        _menu!.Open(board);

        AddGroove(GrooveSide.Top, GrooveKind.Through);
        AddGroove(GrooveSide.Top, GrooveKind.Through);

        Assert.AreEqual(1, board.Grooves.Count,
            "два одинаковых паза на одной стороне — это один паз, а не два");
    }

    [Test]
    public void EditRow_ChangesThatGrooveInPlace()
    {
        var board = Board("Полка");
        _menu!.Open(board);
        AddGroove(GrooveSide.Left, GrooveKind.Through);

        var sideDd = Dd("CtxGrooveSide0");
        sideDd.value = (int)GrooveSide.Right;

        Assert.AreEqual(GrooveSide.Right, board.Grooves[0].side,
            "правка строки — прямое действие: отдельного режима «редактирования» нет");
        Assert.AreEqual(1, board.Grooves.Count, "правка не плодит новый паз");
    }

    [Test]
    public void EditRow_IntoAnExistingGroove_IsRefusedAndTheRowSnapsBack()
    {
        var board = Board("Полка");
        _menu!.Open(board);
        AddGroove(GrooveSide.Left, GrooveKind.Through);
        AddGroove(GrooveSide.Right, GrooveKind.Through);

        Dd("CtxGrooveSide1").value = (int)GrooveSide.Left;

        Assert.AreEqual(GrooveSide.Right, board.Grooves[1].side,
            "дубль не создаётся");
        Assert.AreEqual((int)GrooveSide.Right, Dd("CtxGrooveSide1").value,
            "отказ обязан вернуть дропдаун к фактическому набору — иначе строка показывала бы "
            + "сторону, которой у паза нет");
    }

    [Test]
    public void RemoveRow_DropsThatGroove()
    {
        var board = Board("Полка");
        _menu!.Open(board);
        AddGroove(GrooveSide.Left, GrooveKind.Through);
        AddGroove(GrooveSide.Right, GrooveKind.Through);

        var del = Panel().Find("CtxGrooveDel0")!.GetComponent<Button>();
        del.onClick.Invoke();
        del.onClick.Invoke();

        Assert.AreEqual(1, board.Grooves.Count, "удаление в два клика (правило 3 UI-GUIDELINES)");
        Assert.AreEqual(GrooveSide.Right, board.Grooves[0].side, "удалилась именно первая строка");
    }

    [Test]
    public void GroovesChangedOutsideTheMenu_IsNoticed()
    {
        var board = Board("Полка");
        _menu!.Open(board);
        AddGroove(GrooveSide.Left, GrooveKind.Through);

        CommandStack.Undo();

        Assert.IsTrue(_menu!.Grooves.ChangedOutsideTheMenu(),
            "undo/redo и MCP правят пазы мимо панели — без отпечатка строки показывали бы "
            + "устаревший набор");
    }

    [Test]
    public void OpeningAnElement_ShowsTheGrooveListCollapsed()
    {
        var board = Board("Полка");
        _menu!.Open(board);
        AddGroove(GrooveSide.Left, GrooveKind.Through);
        Assume.That(Panel().Find("CtxGrooveSide0")!.gameObject.activeSelf, Is.True);

        _menu!.Open(board);

        Assert.IsFalse(Panel().Find("CtxGrooveSide0")!.gameObject.activeSelf,
            "панель открывается со свёрнутым списком пазов");
    }
}
