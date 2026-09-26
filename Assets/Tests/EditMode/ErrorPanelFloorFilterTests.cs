using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.UI;

/// <summary>L7 (обзор ui-mcp): фильтр «Этаж» окна «Ошибки» ключевал по
/// редактируемому, НЕ уникальному имени уровня (<c>LevelRegistry.LevelOf(...).name</c>).
/// Два этажа с одинаковым именем схлопывались в одну строку фильтра — выбор одного
/// показывал/скрывал находки СРАЗУ ОБОИХ. Ключ — id, имя остаётся только подписью
/// строки (<see cref="MultiSelectDropdown.SetOptionsWithLabels"/>).</summary>
public class ErrorPanelFloorFilterTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var g in _spawned)
            if (g != null) Object.DestroyImmediate(g);
        _spawned.Clear();
        LevelRegistry.Reset();
    }

    private KitchenElement MakeElement(string name, string levelId)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.LevelId = levelId;
        return e;
    }

    [Test]
    public void TwoLevelsWithTheSameName_StayTwoSeparateFilterOptions()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "Этаж", 0, 3000),
            new Level("2", "Этаж", 3000, 3000),
        });
        var onFirst = MakeElement("A", "1");
        var onSecond = MakeElement("B", "2");
        var issues = new List<AnalysisIssue>
        {
            new AnalysisIssue(IssueLevel.Warning, "X", "A", "m", onFirst),
            new AnalysisIssue(IssueLevel.Warning, "X", "B", "m", onSecond),
        };

        var ids = ErrorPanelUI.FloorIdsPresentIn(issues);

        Assert.AreEqual(2, ids.Count,
            "два разных этажа с ОДИНАКОВЫМ именем обязаны остаться двумя отдельными "
            + "вариантами фильтра — ключевание по имени схлопывало их в один, и выбор "
            + "одного фильтровал находки обоих сразу");
        CollectionAssert.AreEquivalent(new[] { "1", "2" }, ids);
    }

    [Test]
    public void FloorNameOf_LooksUpTheCurrentDisplayName_ById()
    {
        LevelRegistry.Set(new[]
        {
            new Level("1", "Подвал", 0, 3000),
            new Level("2", "Мансарда", 3000, 3000),
        });

        Assert.AreEqual("Подвал", ErrorPanelUI.FloorNameOf("1"));
        Assert.AreEqual("Мансарда", ErrorPanelUI.FloorNameOf("2"));
    }
}
