using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class ContextMenuLayoutTests
{
    private Canvas _canvas;
    private ContextMenuUI _menu;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        UIFactory.EnsureEventSystem();
        _canvas = UIFactory.CreateCanvas("TestCanvas");
        var go = new GameObject("CtxMenu");
        _menu = go.AddComponent<ContextMenuUI>();
        _menu.Build(_canvas.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_menu != null) Object.DestroyImmediate(_menu.gameObject);
        if (_canvas != null) Object.DestroyImmediate(_canvas.gameObject);
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        BoardRegistry.Clear();
    }

    private FacadeElement MakeFacade(string name)
    {
        var go = new GameObject(name);
        var facade = go.AddComponent<FacadeElement>();
        facade.BoardName = name;
        facade.DimensionsMM = new Vector3Int(400, 300, 18);
        _spawned.Add(go);
        return facade;
    }

    private KitchenElement MakeBoard(string name)
    {
        var go = new GameObject(name);
        var el = go.AddComponent<KitchenElement>();
        el.BoardName = name;
        el.DimensionsMM = new Vector3Int(800, 400, 18);
        _spawned.Add(go);
        return el;
    }

    [Test]
    public void Facade_GapSectionBottomAboveNextRow()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");
        Assert.NotNull(panel);

        var gapSection = panel.Find("_GapSection");
        var xField = panel.Find("F_X, м");
        Assert.NotNull(gapSection, "gap section must exist for facade");
        Assert.NotNull(xField, "X position field must exist");

        var gapRt = gapSection.GetComponent<RectTransform>();
        var xRt = xField.GetComponent<RectTransform>();

        float gapBottom = gapRt.anchoredPosition.y - gapRt.sizeDelta.y / 2f;
        float xTop = xRt.anchoredPosition.y + xRt.sizeDelta.y / 2f;

        Assert.GreaterOrEqual(gapBottom, xTop,
            "gap section bottom must be above the first position row");
    }

    [Test]
    public void Board_GapSectionInactive_PositionRowsShiftedUp()
    {
        var panel = _canvas.transform.Find("ContextMenu");
        Assert.NotNull(panel);
        var gapSection = panel.Find("_GapSection");
        Assert.NotNull(gapSection);

        var board = MakeBoard("B1");
        _menu.Open(board);

        Assert.IsFalse(gapSection.gameObject.activeSelf,
            "gap section must be hidden for regular board");
    }

    [Test]
    public void Facade_NoOverlap_BetweenGapAndNextRow()
    {
        var facade = MakeFacade("F1");
        _menu.Open(facade);
        var panel = _canvas.transform.Find("ContextMenu");

        var gapSection = panel.Find("_GapSection");
        var xInput = panel.Find("F_X, м");
        Assert.NotNull(gapSection);
        Assert.NotNull(xInput);

        var gapRt = gapSection.GetComponent<RectTransform>();
        var xRt = xInput.GetComponent<RectTransform>();

        float gapSectionBottom = gapRt.anchoredPosition.y - gapRt.sizeDelta.y / 2f;
        float xTop = xRt.anchoredPosition.y + xRt.sizeDelta.y / 2f;

        Assert.GreaterOrEqual(gapSectionBottom, xTop,
            "gap section bottom must be above the first position row");

        foreach (var childName in new[] { "Gap_Ширина X, мм", "F_gap_Ширина X, мм",
                                          "Gap_Высота Y, мм", "F_gap_Высота Y, мм" })
        {
            var child = gapSection.Find(childName);
            Assert.NotNull(child, $"missing {childName}");
            var childRt = child.GetComponent<RectTransform>();
            float childBottomPanel = gapRt.anchoredPosition.y + childRt.anchoredPosition.y
                                     - childRt.sizeDelta.y / 2f;
            Assert.GreaterOrEqual(childBottomPanel, xTop,
                $"{childName} must not overlap the X position row");
        }
    }

    [Test]
    public void GapSection_AddNewRow_AdjustsHeightAutomatically()
    {
        var panel = _canvas.transform.Find("ContextMenu");
        var gapSection = panel.Find("_GapSection");
        var gapRt = gapSection.GetComponent<RectTransform>();
        float h = gapRt.sizeDelta.y;

        Assert.Greater(h, 0);
        Assert.Less(h, 200, "section should not be unreasonably tall");
    }
}
