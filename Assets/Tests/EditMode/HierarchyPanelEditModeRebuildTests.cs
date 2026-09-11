using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Перестроение списка окна «Сцена» ВНЕ play mode — путь, которого до сведения
/// голого <c>Destroy(</c> к <c>DestroyNow.The</c> не существовало вовсе.
///
/// <para>Голый <c>UnityEngine.Object.Destroy</c> вне play mode бросает
/// <c>InvalidOperationException</c>, поэтому второй проход <c>Refresh</c> — тот, где есть
/// что сносить, — в EditMode падал, и всё окно проверялось только из PlayMode
/// (<c>HierarchyPanelTests</c>, чей собственный заголовок это и объясняет: «PlayMode —
/// перестроение списка сносит старые строки через Object.Destroy»).</para>
///
/// <para>Второе, что открылось: уничтожение стало НЕМЕДЛЕННЫМ. Отложенный
/// <c>Destroy</c> снимает объект только в конце кадра, а в EditMode кадра нет вообще —
/// так что «старых строк больше нет» здесь нельзя было ни дождаться, ни утверждать.
/// Именно это и проверяют тесты ниже: не «новые строки появились», а «старые исчезли,
/// и список не удвоился».</para></summary>
public class HierarchyPanelEditModeRebuildTests
{
    private GameObject _canvasGo = null!;
    private GameObject _panelHost = null!;
    private HierarchyPanelUI _panel = null!;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();

        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();

        _panelHost = new GameObject("HierarchyPanelHost");
        _panel = _panelHost.AddComponent<HierarchyPanelUI>();
        _panel.Build(_canvasGo.transform);
    }

    [TearDown]
    public void Teardown()
    {
        if (_panelHost != null) UnityEngine.Object.DestroyImmediate(_panelHost);
        if (_canvasGo != null) UnityEngine.Object.DestroyImmediate(_canvasGo);
        foreach (var go in _spawned)
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();
        GroupManager.Clear();
        ProjectWindows.Clear();
    }

    private KitchenElement MakeElement(string name)
    {
        var go = new GameObject(name);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(600, 400, 18);
        PartRegistry.Register(element);
        _spawned.Add(go);
        return element;
    }

    private Transform Panel => _canvasGo.transform.Find("HierarchyPanel");

    private Transform Content => Panel.Find("HierViewport/HierContent");

    private static string LabelOf(Transform row)
    {
        var text = row.Find("Main").GetComponentInChildren<TMP_Text>();
        return text != null ? text.text : "";
    }

    private List<string> RowLabels()
    {
        var labels = new List<string>();
        foreach (Transform row in Content) labels.Add(LabelOf(row));
        return labels;
    }

    private List<GameObject> RowObjects()
    {
        var rows = new List<GameObject>();
        foreach (Transform row in Content) rows.Add(row.gameObject);
        return rows;
    }

    [Test]
    public void HierarchyPanel_Refresh_BuildsTheRowsOutsidePlayMode()
    {
        Assume.That(Application.isPlaying, Is.False,
            "весь смысл этих тестов — путь ВНЕ play mode; под PlayMode они ничего не "
            + "доказывают, потому что там голый Destroy и не падал");
        MakeElement("Полка");
        MakeElement("Боковина");

        _panel.SetVisible(true);

        var labels = RowLabels();
        Assert.Contains("Полка", labels,
            "перестроение списка вне play mode обязано быть достижимо: пока снос старых "
            + "строк шёл голым Destroy, второй Refresh бросал InvalidOperationException, "
            + "и окно «Сцена» проверялось только тяжёлым PlayMode-прогоном");
        Assert.Contains("Боковина", labels);
    }

    [Test]
    public void HierarchyPanel_SecondRefresh_TakesTheOldRowsDownImmediately()
    {
        MakeElement("Полка");
        _panel.SetVisible(true);
        var oldRows = RowObjects();
        int firstCount = oldRows.Count;
        Assume.That(firstCount, Is.GreaterThan(0), "первый проход построил хоть одну строку");

        MakeElement("Боковина");
        _panel.SetVisible(true);

        Assert.AreEqual(firstCount + 1, Content.childCount,
            "список перестраивается, а не дописывается: отложенный Destroy снимает "
            + "объекты в конце кадра, поэтому старые строки прожили бы весь Refresh "
            + "рядом с новыми и список удвоился бы");
        foreach (var row in oldRows)
            Assert.IsTrue(row == null,
                "старая строка обязана быть уничтожена ЗДЕСЬ И СЕЙЧАС: в EditMode кадра "
                + "нет вовсе, так что отложенного уничтожения было бы не дождаться — "
                + "сравнение через == пользуется перегрузкой Unity, Assert.IsNull "
                + "уничтоженный объект нулём не считает");
    }

    [Test]
    public void HierarchyPanel_Refresh_DropsTheRowOfAnElementThatLeftTheScene()
    {
        var shelf = MakeElement("Полка");
        MakeElement("Боковина");
        _panel.SetVisible(true);
        Assume.That(RowLabels(), Has.Member("Полка"), "строка удаляемой детали была");

        PartRegistry.Unregister(shelf);
        _panel.SetVisible(true);

        Assert.That(RowLabels(), Has.No.Member("Полка"),
            "ушедшая из сцены деталь пропадает из окна «Сцена» тем же перестроением: "
            + "строка, пережившая свой элемент, кликается и выделяет мертвеца");
        Assert.Contains("Боковина", RowLabels(),
            "и это именно перестроение, а не очистка: соседняя строка остаётся на месте");
    }
}
