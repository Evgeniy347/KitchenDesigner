using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Стены не несли признака «несущая». Пользователь решил: все стены
/// несущие по умолчанию, пока не сказано иное, — этот файл проверяет ровно
/// это решение: дефолт, отмену переключателя и то, что старый проект без
/// поля читается как несущая (миграция бесплатна за счёт инициализатора поля
/// в <see cref="ElementData"/> — см. conventions/SERIALIZATION.md).</summary>
public class WallLoadBearingTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private GameObject MakeWall(string name = "Стена")
    {
        var go = ElementFactory.CreateWall(new Vector3Int(2000, 2500, 100), name, Vector3.zero);
        _spawned.Add(go);
        return go;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
    }

    [Test]
    public void NewWall_DefaultsToLoadBearing()
    {
        var wall = MakeWall().GetComponent<Wall>();
        Assert.IsTrue(wall.LoadBearing, "все стены несущие по умолчанию, пока не сказано иное");
    }

    [Test]
    public void SetWallLoadBearingCommand_Undo_RestoresPreviousValue()
    {
        var wall = MakeWall().GetComponent<Wall>();
        Assert.IsTrue(wall.LoadBearing, "стартовое состояние — несущая");

        CommandStack.Execute(new SetWallLoadBearingCommand(wall, wall.LoadBearing, false));
        Assert.IsFalse(wall.LoadBearing, "команда обязана применить новое значение");

        CommandStack.Undo();
        Assert.IsTrue(wall.LoadBearing, "отмена обязана вернуть стену в несущую");

        CommandStack.Redo();
        Assert.IsFalse(wall.LoadBearing, "повтор обязан вернуть выключенное состояние");
    }

    [Test]
    public void SaveLoadRoundTrip_PreservesLoadBearing_BothWays()
    {
        var bearingWall = MakeWall("BearingWall").GetComponent<Wall>();
        var partitionWall = MakeWall("PartitionWall").GetComponent<Wall>();
        partitionWall.LoadBearing = false;
        string bearingName = bearingWall.GetComponent<KitchenElement>().PartName;
        string partitionName = partitionWall.GetComponent<KitchenElement>().PartName;
        PartRegistry.Register(bearingWall.GetComponent<KitchenElement>());
        PartRegistry.Register(partitionWall.GetComponent<KitchenElement>());

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var restored = SaveLoadManager.RestoreScene(SaveLoadManager.Deserialize(json)!)
            .Select(g => g.GetComponent<Wall>())
            .Where(w => w != null)
            .ToList();
        foreach (var go in restored.Select(w => w!.gameObject)) _spawned.Add(go);

        var restoredBearing = restored.First(w => w!.GetComponent<KitchenElement>().PartName == bearingName);
        var restoredPartition = restored.First(w => w!.GetComponent<KitchenElement>().PartName == partitionName);

        Assert.IsTrue(restoredBearing!.LoadBearing, "несущая стена обязана остаться несущей после круга");
        Assert.IsFalse(restoredPartition!.LoadBearing, "перегородка обязана остаться не несущей после круга");
    }

    [Test]
    public void OldProjectJson_WithoutLoadBearingField_LoadsAsLoadBearing()
    {
        var wall = MakeWall().GetComponent<KitchenElement>();
        PartRegistry.Register(wall);

        var json = SaveLoadManager.Serialize(SaveLoadManager.CaptureScene(PartRegistry.GetAll()));
        Assert.IsTrue(json.Contains("wallLoadBearing"),
            "проверка ничего не доказывает, если поле и так отсутствует в текущем формате");

        // Симулируем старый файл, сохранённый до появления этого поля: вырезаем его целиком.
        string oldJson = System.Text.RegularExpressions.Regex.Replace(
            json, "\"wallLoadBearing\"\\s*:\\s*(true|false),?", "");

        foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        PartRegistry.Clear();

        var data = SaveLoadManager.Deserialize(oldJson);
        Assert.IsNotNull(data, "старый файл обязан десериализоваться");
        var restored = SaveLoadManager.RestoreScene(data!)
            .Select(g => g.GetComponent<Wall>())
            .Where(w => w != null)
            .ToList();
        foreach (var go in restored.Select(w => w!.gameObject)) _spawned.Add(go);

        Assert.AreEqual(1, restored.Count);
        Assert.IsTrue(restored[0]!.LoadBearing,
            "старый проект без поля обязан читаться как несущая — миграция бесплатна за счёт "
            + "инициализатора поля в ElementData");
    }
}
