using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>При закрытии программы автосохранение должно писать сцену в файл,
/// только если включена настройка AutoSave.</summary>
public class AutoSaveQuitTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevAutoSave;
    private string _autoSavePath;
    private byte[] _autoSaveBackup; // содержимое реального autosave, чтобы не затереть

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        BoardRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    [SetUp]
    public void Setup()
    {
        Assume.That(KitchenSettings.Instance, Is.Not.Null, "нужен Resources/KitchenSettings");
        _prevAutoSave = KitchenSettings.Instance.AutoSave;
        _autoSavePath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        _autoSaveBackup = File.Exists(_autoSavePath) ? File.ReadAllBytes(_autoSavePath) : null;
        if (File.Exists(_autoSavePath)) File.Delete(_autoSavePath);
    }

    [TearDown]
    public void Teardown()
    {
        if (KitchenSettings.Instance != null)
            KitchenSettings.Instance.AutoSave = _prevAutoSave;

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            BoardRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        // Восстанавливаем реальный autosave пользователя (или убираем тестовый).
        if (_autoSaveBackup != null) File.WriteAllBytes(_autoSavePath, _autoSaveBackup);
        else if (File.Exists(_autoSavePath)) File.Delete(_autoSavePath);
    }

    [Test]
    public void SaveOnQuit_WhenAutoSaveEnabled_WritesFile()
    {
        KitchenSettings.Instance.AutoSave = true;
        Make("QuitBoard", new Vector3Int(800, 400, 18), new Vector3(0.4f, 0.2f, 0.1f));

        Assert.IsTrue(AutoSaveManager.SaveOnQuit(), "при включённом автосохранении выход пишет файл");
        Assert.IsTrue(File.Exists(_autoSavePath), "файл автосохранения создан");

        var json = File.ReadAllText(_autoSavePath);
        Assert.IsTrue(json.Contains("QuitBoard"), "в файл попала текущая сцена");
    }

    [Test]
    public void SaveOnQuit_WhenAutoSaveDisabled_DoesNotWrite()
    {
        KitchenSettings.Instance.AutoSave = false;
        Make("QuitBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        Assert.IsFalse(AutoSaveManager.SaveOnQuit(), "при выключенном автосохранении — не пишем");
        Assert.IsFalse(File.Exists(_autoSavePath), "файл не создан");
    }
}
