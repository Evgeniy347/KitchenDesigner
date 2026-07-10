using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;

public class SaveLoadManagerTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private KitchenElement CreateElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var element = go.AddComponent<KitchenElement>();
        element.BoardName = name;
        element.DimensionsMM = dims;
        _spawned.Add(go);
        return element;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        // Подчистить созданные RestoreScene/Factory доски.
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void CaptureScene_ExcludesBasePlate_KeepsBoards()
    {
        var plate = CreateElement("BasePlate", new Vector3Int(3000, 18, 3000), Vector3.zero);
        plate.gameObject.AddComponent<BasePlate>();
        CreateElement("Board", new Vector3Int(800, 400, 18), new Vector3(1, 2, 3));

        var data = SaveLoadManager.CaptureScene(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        Assert.AreEqual(1, data.elements.Length);
        Assert.AreEqual("Board", data.elements[0].name);
        Assert.AreEqual(new[] { 800, 400, 18 }, data.elements[0].dimensionsMM);
        Assert.AreEqual(1f, data.elements[0].position[0], 0.0001f);
        Assert.AreEqual(AppConstants.SAVE_FORMAT_VERSION, data.version);
    }

    [Test]
    public void Serialize_Deserialize_RoundTrips()
    {
        CreateElement("A", new Vector3Int(800, 400, 18), new Vector3(0.5f, 0.2f, -0.3f));
        var data = SaveLoadManager.CaptureScene(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        var json = SaveLoadManager.Serialize(data);
        var restored = SaveLoadManager.Deserialize(json);

        Assert.AreEqual(data.version, restored.version);
        Assert.AreEqual(1, restored.elements.Length);
        Assert.AreEqual("A", restored.elements[0].name);
        Assert.AreEqual(new[] { 800, 400, 18 }, restored.elements[0].dimensionsMM);
        Assert.AreEqual(-0.3f, restored.elements[0].position[2], 0.0001f);
    }

    [Test]
    public void SaveToFile_LoadFromFile_RoundTrips()
    {
        CreateElement("A", new Vector3Int(600, 400, 18), Vector3.zero);
        var data = SaveLoadManager.CaptureScene(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        var path = Path.Combine(Application.temporaryCachePath, "sl_roundtrip.json");
        Assert.IsTrue(SaveLoadManager.SaveToFile(path, data));
        Assert.IsTrue(File.Exists(path));

        var loaded = SaveLoadManager.LoadFromFile(path);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, loaded.elements.Length);
        Assert.AreEqual("A", loaded.elements[0].name);

        File.Delete(path);
    }

    [Test]
    public void LoadFromFile_Missing_ReturnsNullAndLogsError()
    {
        var path = Path.Combine(Application.temporaryCachePath, "does_not_exist_xyz.json");
        if (File.Exists(path)) File.Delete(path);

        LogAssert.Expect(LogType.Error, new Regex("File not found"));
        var loaded = SaveLoadManager.LoadFromFile(path);

        Assert.IsNull(loaded);
    }

    [Test]
    public void RestoreScene_CreatesBoardsWithDimensions()
    {
        var data = new ProjectData(new[]
        {
            new ElementData
            {
                name = "Restored",
                dimensionsMM = new[] { 600, 600, 18 },
                position = new[] { 0.1f, 0.2f, 0.3f },
                rotation = new[] { 0f, 0f, 0f, 1f }
            }
        });

        var created = SaveLoadManager.RestoreScene(data);

        Assert.AreEqual(1, created.Count);
        var element = created[0].GetComponent<KitchenElement>();
        Assert.AreEqual(new Vector3Int(600, 600, 18), element.DimensionsMM);
        Assert.AreEqual(0.2f, created[0].transform.position.y, 0.0001f);
        // RestoreScene-объекты подчистит TearDown.
    }

    [Test]
    public void IsVersionCompatible_MatchingVersion_True()
    {
        var data = new ProjectData { version = AppConstants.SAVE_FORMAT_VERSION };
        Assert.IsTrue(SaveLoadManager.IsVersionCompatible(data));

        data.version = AppConstants.SAVE_FORMAT_VERSION + 99;
        Assert.IsFalse(SaveLoadManager.IsVersionCompatible(data));
    }

    [Test]
    public void Movable_RoundTripsThroughSaveAndRestore()
    {
        var el = CreateElement("Locked", new Vector3Int(600, 400, 18), Vector3.zero);
        el.Movable = false;
        var data = SaveLoadManager.CaptureScene(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));
        Assert.IsFalse(data.elements[0].movable);

        var restored = SaveLoadManager.Deserialize(SaveLoadManager.Serialize(data));
        var created = SaveLoadManager.RestoreScene(restored);

        Assert.AreEqual(1, created.Count);
        Assert.IsFalse(created[0].GetComponent<KitchenElement>().Movable);
    }

    [Test]
    public void HandleMode_RoundTripsThroughCaptureAndRestore()
    {
        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Move);
        var data = SaveLoadManager.CaptureScene(new List<KitchenElement>());
        Assert.AreEqual("Move", data.handleMode);

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);

        SaveLoadManager.RestoreScene(data);
        Assert.AreEqual(ResizeHandleManager.HandleMode.Move, ResizeHandleManager.Mode);

        ResizeHandleManager.SetMode(ResizeHandleManager.HandleMode.Resize);
    }

    [Test]
    public void BasePlate_PositionAndSize_RoundTripThroughCaptureAndRestore()
    {
        var floor = CreateElement("BasePlate", new Vector3Int(3000, 18, 3000), new Vector3(0, -0.009f, 0));
        floor.gameObject.AddComponent<BasePlate>();
        floor.gameObject.tag = "Floor";
        CreateElement("Board", new Vector3Int(800, 400, 18), new Vector3(0.5f, 0.2f, 0.3f));

        var data = SaveLoadManager.CaptureScene(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));

        Assert.IsNotNull(data.basePlate, "BasePlate должен быть в сохранении");
        Assert.AreEqual(new[] { 3000, 18, 3000 }, data.basePlate.dimensionsMM);
        Assert.AreEqual(0f, data.basePlate.position[0], 0.0001f);
        Assert.AreEqual(-0.009f, data.basePlate.position[1], 0.001f);
        Assert.AreEqual(0f, data.basePlate.position[2], 0.0001f);

        floor.transform.position = new Vector3(1f, 2f, 3f);

        SaveLoadManager.RestoreScene(data);
        Assert.AreEqual(0f, floor.transform.position.x, 0.0001f);
        Assert.AreEqual(-0.009f, floor.transform.position.y, 0.001f);
        Assert.AreEqual(0f, floor.transform.position.z, 0.0001f);
    }

    [Test]
    public void LoadLastSession_RestoresBoards_FromLastPath()
    {
        CreateElement("Saved", new Vector3Int(800, 400, 18), new Vector3(0.5f, 0.2f, 0.3f));
        var data = SaveLoadManager.CaptureScene(_spawned.ConvertAll(g => g.GetComponent<KitchenElement>()));
        var path = Path.Combine(Application.temporaryCachePath, "sl_lastsession.json");
        Assert.IsTrue(SaveLoadManager.SaveToFile(path, data));

        // Имитируем новый запуск приложения: чистим сцену, задаём последний путь.
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        string prevLast = SaveLoadManager.LastPath;
        SaveLoadManager.LastPath = path;

        bool loaded = SaveLoadManager.LoadLastSession();

        Assert.IsTrue(loaded);
        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);
        Assert.AreEqual("Saved", restored[0].BoardName);

        SaveLoadManager.LastPath = prevLast;
        File.Delete(path);
    }

    [Test]
    public void Facade_SaveAndRestore_PreservesComponentAndGaps()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(600, 400, 18), "MyFacade", Vector3.zero, 3, 5, 7, 9);
        _spawned.Add(go);
        var facade = go.GetComponent<FacadeElement>();
        BoardRegistry.Register(facade);

        var data = SaveLoadManager.CaptureScene(
            new List<KitchenElement> { facade });

        Assert.IsTrue(data.elements[0].isFacade);
        Assert.AreEqual(3, data.elements[0].gapLeft);
        Assert.AreEqual(5, data.elements[0].gapRight);
        Assert.AreEqual(7, data.elements[0].gapTop);
        Assert.AreEqual(9, data.elements[0].gapBottom);

        foreach (var sp in _spawned)
            if (sp != null) Object.DestroyImmediate(sp);
        _spawned.Clear();
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        BoardRegistry.Clear();

        var restoredData = SaveLoadManager.Deserialize(
            SaveLoadManager.Serialize(data));
        var created = SaveLoadManager.RestoreScene(restoredData);

        Assert.AreEqual(1, created.Count);
        var restoredFacade = created[0].GetComponent<FacadeElement>();
        Assert.IsNotNull(restoredFacade, "Restored element should be FacadeElement");
        Assert.AreEqual("MyFacade", restoredFacade.BoardName);
        Assert.AreEqual(3, restoredFacade.GapLeft);
        Assert.AreEqual(5, restoredFacade.GapRight);
        Assert.AreEqual(7, restoredFacade.GapTop);
        Assert.AreEqual(9, restoredFacade.GapBottom);
        Assert.AreEqual(new Vector3Int(600, 400, 18), restoredFacade.DimensionsMM);
    }
}
