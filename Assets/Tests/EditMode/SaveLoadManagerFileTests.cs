using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Покрытие файлового IO SaveLoadManager (проекты в SavesDirectory,
/// произвольные пути, бэкап, список файлов).</summary>
public class SaveLoadManagerFileTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private string? _prevLastPath;
    private const string ProjName = "sl_filetests_proj";

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void Setup() => _prevLastPath = SaveLoadManager.LastPath;

    [TearDown]
    public void Teardown()
    {
        SaveLoadManager.LastPath = _prevLastPath!;

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        // детали, созданные RestoreScene/LoadProject.
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        var path = SaveLoadManager.PathForName(ProjName);
        if (File.Exists(path)) File.Delete(path);
    }

    [Test]
    public void PathForName_ReturnsJsonInSavesDirectory()
    {
        var path = SaveLoadManager.PathForName("foo");
        // persistentDataPath отдаёт прямые слэши, GetDirectoryName — обратные: нормализуем.
        var dir = Path.GetDirectoryName(path).Replace('\\', '/');
        Assert.AreEqual(SaveLoadManager.SavesDirectory.Replace('\\', '/'), dir);
        Assert.AreEqual(".json", Path.GetExtension(path));
        Assert.AreEqual("foo.json", Path.GetFileName(path));
    }

    [Test]
    public void SaveProject_ThenLoadProject_RoundTrips()
    {
        Make("Board", new Vector3Int(800, 400, 18), new Vector3(0.4f, 0.2f, 0.1f));
        Assert.IsTrue(SaveLoadManager.SaveProject(ProjName, backup: false));
        Assert.IsTrue(File.Exists(SaveLoadManager.PathForName(ProjName)));

        // Чистим сцену и грузим обратно.
        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        Assert.IsTrue(SaveLoadManager.LoadProject(ProjName));
        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);
        Assert.AreEqual("Board", restored[0].PartName);
    }

    [Test]
    public void SaveProject_Twice_CreatesBackupZip()
    {
        Make("Board", new Vector3Int(600, 400, 18), Vector3.zero);
        Assert.IsTrue(SaveLoadManager.SaveProject(ProjName, backup: false)); // первый файл
        Assert.IsTrue(SaveLoadManager.SaveProject(ProjName, backup: true));  // второй → бэкап

        var backupDir = Path.Combine(SaveLoadManager.SavesDirectory, "backups");
        Assert.IsTrue(Directory.Exists(backupDir));
        Assert.Greater(Directory.GetFiles(backupDir, ProjName + "_*.zip").Length, 0);
    }

    [Test]
    public void GetSaveFiles_ContainsSavedProject()
    {
        Make("Board", new Vector3Int(800, 400, 18), Vector3.zero);
        SaveLoadManager.SaveProject(ProjName, backup: false);

        var files = new List<string>(SaveLoadManager.GetSaveFiles());
        Assert.Contains(ProjName, files);
    }

    [Test]
    public void SaveToPath_SetsLastPath_AndLoadFromPath_Restores()
    {
        Make("Board", new Vector3Int(800, 400, 18), new Vector3(0.1f, 0.2f, 0.3f));
        var path = Path.Combine(Application.temporaryCachePath, "sl_topath.json");
        if (File.Exists(path)) File.Delete(path);

        Assert.IsTrue(SaveLoadManager.SaveToPath(path));
        Assert.IsTrue(SaveLoadManager.HasLastPath);
        Assert.AreEqual(path, SaveLoadManager.LastPath);

        // SaveToLastPath пишет туда же.
        Assert.IsTrue(SaveLoadManager.SaveToLastPath());

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        Assert.IsTrue(SaveLoadManager.LoadFromPath(path));
        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);

        File.Delete(path);
    }

    [Test]
    public void CaptureCurrentJson_ReflectsScene()
    {
        Make("UniquePartName", new Vector3Int(800, 400, 18), Vector3.zero);
        var json = SaveLoadManager.CaptureCurrentJson();
        Assert.IsTrue(json.Contains("UniquePartName"));
    }

    [Test]
    public void ClearBoards_RemovesBoards_KeepsBasePlate()
    {
        var plate = Make("BasePlate", new Vector3Int(3000, 18, 3000), Vector3.zero);
        plate.gameObject.AddComponent<BasePlate>();
        var board = Make("Board", new Vector3Int(800, 400, 18), new Vector3(1, 0, 0));

        var list = new List<KitchenElement> { plate, board };
        SaveLoadManager.ClearBoards(list);

        Assert.IsTrue(plate != null && plate.gameObject != null && plate.gameObject.activeSelf);
        Assert.IsTrue(board == null || board.Equals(null), "деталь удалена");
    }

    [Test]
    public void LastDirectory_WhenNoLastPath_IsSavesDirectory()
    {
        SaveLoadManager.LastPath = "";
        Assert.IsFalse(SaveLoadManager.HasLastPath);
        Assert.AreEqual(SaveLoadManager.SavesDirectory, SaveLoadManager.LastDirectory);
    }
}
