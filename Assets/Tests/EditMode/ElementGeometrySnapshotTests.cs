using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Снимок геометрии для ядра: что в него попадает и в какой позе.
///
/// Здесь живут причины, которые раньше были комментариями в
/// ElementGeometryExtensions.cs: выключенные объекты отсеиваются НА ГРАНИЦЕ, а
/// снимок для гипотетической позиции саму деталь не двигает.</summary>
public class ElementGeometrySnapshotTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
    }

    private KitchenElement Board(string name, Vector3 pos)
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 18, 400), name, pos);
        _spawned.Add(go);
        return go.GetComponent<KitchenElement>();
    }

    [Test]
    public void HiddenElements_NeverReachTheCore()
    {
        var visible = Board("Видимая", Vector3.zero);
        var hidden = Board("Скрытая", new Vector3(1f, 0f, 0f));
        hidden.gameObject.SetActive(false);

        var geometry = new List<KitchenElement> { visible, hidden }.ToGeometry();

        Assert.AreEqual(1, geometry.Count,
            "ядро сцены не видит и об «выключенности» знать не может — отсев обязан "
            + "случиться здесь, на границе, иначе скрытая деталь ловила бы снэп и "
            + "нарушения так же, как видимая");
        Assert.AreEqual(visible.GetInstanceID(), geometry[0].Id, "уцелела именно видимая деталь");
    }

    [Test]
    public void VisibleElements_AllReachTheCore()
    {
        var a = Board("A", Vector3.zero);
        var b = Board("B", new Vector3(1f, 0f, 0f));

        Assert.AreEqual(2, new List<KitchenElement> { a, b }.ToGeometry().Count,
            "положительный контроль: включённые детали отсев не трогает");
    }

    [Test]
    public void SnapshotAtAHypotheticalPosition_LeavesThePartWhereItStands()
    {
        var board = Board("Полка", new Vector3(0.2f, 0.3f, 0.4f));
        var stoodAt = board.transform.position;
        var probe = new Vector3(3f, 3f, 3f);

        var here = board.ToGeometry();
        var there = board.ToGeometryAt(probe);

        Assert.AreEqual(stoodAt, board.transform.position,
            "примерка НЕ двигает деталь: раньше это делали записью в transform.position "
            + "с возвратом назад, а каждая такая запись грязнит поддерево трансформов, "
            + "и пересчёт оплачивал тот, кто следующим читал геометрию");

        var moved = (there.Min + there.Max) * 0.5f - (here.Min + here.Max) * 0.5f;
        Assert.AreEqual((probe - stoodAt).x, moved.x, 1e-4f, "а снимок при этом взят в примеряемой позе");
        Assert.AreEqual((probe - stoodAt).z, moved.z, 1e-4f);
    }
}
