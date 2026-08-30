using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuGapSectionTests
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
        SideHighlighter.Hide();
        CommandStack.Clear();
        if (_menu != null) Object.DestroyImmediate(_menu!.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas!.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
    }

    private FacadeElement Facade()
    {
        var go = ElementFactory.CreateFacade(new Vector3Int(450, 700, 18), "Фасад", Vector3.zero,
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        _spawned.Add(go);
        return go.GetComponent<FacadeElement>();
    }

    private KitchenElement Board()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 300, 18), "Полка", Vector3.zero);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    private Transform Panel() => _canvas!.transform.Find("ContextMenu")!;

    private TMP_InputField GapField(GapSide side) =>
        Panel().Find($"F_gap{side}")!.GetComponent<TMP_InputField>();

    private string Counter() =>
        Panel().Find("CtxGaps")!.GetComponentInChildren<TMP_Text>().text;

    [Test]
    public void GapWidgets_KeepTheirNames()
    {
        Assert.NotNull(Panel().Find("CtxGaps"), "раскрывашка ищется по имени CtxGaps");
        foreach (var side in GapSides.All)
            Assert.NotNull(Panel().Find($"F_gap{side}"), $"поле F_gap{side} ищется по имени");
    }

    [Test]
    public void GapFields_AreOrderedByGapSidesAll()
    {
        var section = _menu!.Gaps;
        for (int i = 0; i < GapSides.All.Length; i++)
            Assert.AreSame(GapField(GapSides.All[i]), section.Fields[i],
                "поля хранятся в порядке GapSides.All: Open/Apply/Track ходят по ним по индексу, "
                + "и перепутанный порядок молча записал бы зазор не на ту сторону");
    }

    [Test]
    public void Open_WritesTheElementGapsIntoTheFields()
    {
        var facade = Facade();
        facade.GapLeft = 7;
        facade.GapTop = 11;
        _menu!.Open(facade);

        Assert.AreEqual("7", GapField(GapSide.Left).text.Replace("​", ""));
        Assert.AreEqual("11", GapField(GapSide.Top).text.Replace("​", ""));
    }

    [Test]
    public void Open_ElementWithoutGaps_ShowsZeroes()
    {
        var facade = Facade();
        facade.GapLeft = 7;
        _menu!.Open(facade);
        Assume.That(GapField(GapSide.Left).text.Replace("​", ""), Is.EqualTo("7"));

        _menu!.Open(Board());

        Assert.AreEqual("0", GapField(GapSide.Left).text.Replace("​", ""),
            "у детали без зазоров поля обнуляются — иначе они показывали бы чужие значения");
    }

    [Test]
    public void Counter_CountsSidesWithANonZeroGap()
    {
        var facade = Facade();
        facade.GapLeft = 5;
        facade.GapRight = 0;
        facade.GapTop = 0;
        facade.GapBottom = 0;
        facade.GapFront = 0;
        facade.GapBack = 0;
        _menu!.Open(facade);

        StringAssert.Contains("(1)", Counter(), "ненулевой зазор ровно на одной стороне");
    }

    [Test]
    public void Counter_FollowsTheFields_NotTheElement()
    {
        var facade = Facade();
        facade.GapLeft = 0;
        facade.GapRight = 0;
        facade.GapTop = 0;
        facade.GapBottom = 0;
        facade.GapFront = 0;
        facade.GapBack = 0;
        _menu!.Open(facade);
        Assume.That(Counter(), Does.Contain("(0)"));

        GapField(GapSide.Left).text = "5";
        _menu!.Gaps.RefreshCounter();

        StringAssert.Contains("(1)", Counter(),
            "счётчик считает по ПОЛЯМ: пока курсор в поле, введённое значение ещё не применено, "
            + "а заголовок должен идти за ним");
    }

    [Test]
    public void ApplyFromField_WritesTheGapOntoTheElement_AndIsUndoable()
    {
        var facade = Facade();
        _menu!.Open(facade);

        GapField(GapSide.Left).text = "9";
        GapField(GapSide.Left).onEndEdit.Invoke("9");

        Assert.AreEqual(9, facade.GapLeft, "правка поля зазора применяется сразу (правило 2)");

        CommandStack.Undo();
        Assert.AreEqual(3, facade.GapLeft,
            "зазор помечен [Undoable] — откат приезжает сам, одним шагом");
    }

    [Test]
    public void FieldHover_HighlightsThatSideOnTheElement_AndExitHidesIt()
    {
        var facade = Facade();
        _menu!.Open(facade);

        _menu!.Gaps.Hover(GapSide.Front, entered: true);
        Assert.IsTrue(SideHighlighter.IsGapSideShown(facade, GapSide.Front),
            "«спереди» ничего не говорит о том, где это в сцене у повёрнутой детали — "
            + "наведение показывает сторону на самом элементе");

        _menu!.Gaps.Hover(GapSide.Front, entered: false);
        Assert.IsFalse(SideHighlighter.IsGapSideShown(facade, GapSide.Front));
    }

    [Test]
    public void OpeningAnElement_ShowsTheGapListCollapsed()
    {
        var facade = Facade();
        _menu!.Open(facade);
        _menu!.Gaps.Toggle();
        Assume.That(GapField(GapSide.Left).gameObject.activeSelf, Is.True);

        _menu!.Open(facade);

        Assert.IsFalse(GapField(GapSide.Left).gameObject.activeSelf,
            "панель открывается со свёрнутой секцией зазоров");
    }

    [Test]
    public void GapField_RejectsLetters()
    {
        var field = GapField(GapSide.Back);
        Assert.AreEqual('\0', field.onValidateInput("", 0, 'ы'),
            "в поле зазора буквы не набираются вовсе — иначе поле показывало бы одно, "
            + "а применилось бы другое");
        Assert.AreEqual('7', field.onValidateInput("", 0, '7'));
    }
}
