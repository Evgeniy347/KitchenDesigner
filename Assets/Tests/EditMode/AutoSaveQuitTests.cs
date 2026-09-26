using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Автосохранение при выходе: пишет сцену в файл только если включён
/// AutoSave, и именно в АКТИВНУЮ цель — открытый пользователем файл (его грузит
/// старт), иначе в проект autosave.</summary>
public class AutoSaveQuitTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevAutoSave;
    private string? _prevLastPath;
    private string[]? _recentBackup;
    private string? _autoSavePath;
    private byte[]? _autoSaveBackup;
    private string? _openFilePath;

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
    public void Setup()
    {
        _prevAutoSave = KitchenSettings.Instance.AutoSave;
        _prevLastPath = SaveLoadManager.LastPath;
        _recentBackup = RecentProjectsTestBackup.Capture();
        SaveLoadManager.LastPath = ""; // по умолчанию «файл не открыт»

        _autoSavePath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
        _autoSaveBackup = File.Exists(_autoSavePath!) ? File.ReadAllBytes(_autoSavePath!) : null;
        if (File.Exists(_autoSavePath!)) File.Delete(_autoSavePath!);

        _openFilePath = Path.Combine(Application.temporaryCachePath, "autosave_openfile.json");
        if (File.Exists(_openFilePath!)) File.Delete(_openFilePath!);
    }

    [TearDown]
    public void Teardown()
    {
        if (KitchenSettings.Instance != null)
            KitchenSettings.Instance.AutoSave = _prevAutoSave;
        SaveLoadManager.LastPath = _prevLastPath!;
        RecentProjectsTestBackup.Restore(_recentBackup!);

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        if (_autoSaveBackup != null) File.WriteAllBytes(_autoSavePath!, _autoSaveBackup);
        else if (File.Exists(_autoSavePath!)) File.Delete(_autoSavePath!);

        if (File.Exists(_openFilePath!)) File.Delete(_openFilePath!);
    }

    [Test]
    public void SaveOnQuit_Enabled_NoOpenFile_WritesAutosaveProject()
    {
        KitchenSettings.Instance.AutoSave = true;
        SaveLoadManager.LastPath = "";
        Make("QuitBoard", new Vector3Int(800, 400, 18), new Vector3(0.4f, 0.2f, 0.1f));

        Assert.IsTrue(AutoSaveManager.SaveOnQuit(), "без открытого файла пишем в autosave");
        Assert.IsTrue(File.Exists(_autoSavePath!), "файл autosave создан");
        StringAssert.Contains("QuitBoard", File.ReadAllText(_autoSavePath!));
    }

    // Ключевой тест на исправленный баг: при открытом файле выход пишет именно в
    // него (а не в autosave), чтобы старт — грузящий LastPath — увидел правки.
    [Test]
    public void SaveOnQuit_Enabled_WithOpenFile_WritesThatFile_NotAutosave()
    {
        KitchenSettings.Instance.AutoSave = true;
        SaveLoadManager.LastPath = _openFilePath!;
        RecentProjectsTestBackup.Restore(new string[0]);
        Make("OpenFileBoard", new Vector3Int(800, 400, 18), new Vector3(1f, 0.2f, 0.3f));

        Assert.IsTrue(AutoSaveManager.SaveOnQuit());
        Assert.IsTrue(File.Exists(_openFilePath!), "правки ушли в открытый файл");
        StringAssert.Contains("OpenFileBoard", File.ReadAllText(_openFilePath!));
        Assert.IsFalse(File.Exists(_autoSavePath!), "в отдельный autosave НЕ писали");
        Assert.AreEqual(_openFilePath!, SaveLoadManager.LastPath, "открытый файл остался активным");
        CollectionAssert.Contains(RecentProjects.Paths(), _openFilePath!,
            "автосохранение целится в уже открытый проект — он обязан оставаться в «Загрузить», "
            + "даже если список был пуст на момент автосохранения");
    }

    // Найдено при работе над регистрацией недавних проектов: LoadLastSession без LastPath
    // грузила autosave.json через LoadProject, который LastPath не трогает вовсе — значит
    // восстановленный проект не попадал ни в «Загрузить», ни в цель следующего «Сохранить»
    // (оно ушло бы в quicksave.json, разветвив работу пользователя на два файла молча).
    [Test]
    public void LoadLastSession_RecoveringFromAutosave_AdoptsItAsTheCurrentProject()
    {
        KitchenSettings.Instance.AutoSave = true;
        SaveLoadManager.LastPath = "";
        Make("RecoveredBoard", new Vector3Int(800, 400, 18), new Vector3(0.2f, 0.1f, 0.3f));
        Assert.IsTrue(AutoSaveManager.SaveOnQuit(), "предпосылка: autosave.json записан");

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();
        SaveLoadManager.LastPath = "";
        RecentProjectsTestBackup.Restore(new string[0]);

        bool loaded = SaveLoadManager.LoadLastSession();

        Assert.IsTrue(loaded, "запуск без LastPath, но с autosave.json на диске обязан восстановиться");
        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);
        Assert.AreEqual("RecoveredBoard", restored[0].PartName);
        Assert.AreEqual(_autoSavePath, SaveLoadManager.LastPath,
            "восстановленный из autosave проект обязан стать ТЕКУЩИМ, иначе следующее "
            + "«Сохранить» уйдёт в другой файл (quicksave.json) и молча разветвит работу");
        CollectionAssert.Contains(RecentProjects.Paths(), _autoSavePath,
            "восстановленный проект обязан появиться в «Загрузить» так же, как любое другое открытие");

        foreach (var e in restored)
            if (e != null) Object.DestroyImmediate(e.gameObject);
    }

    [Test]
    public void SaveOnQuit_Disabled_DoesNotWrite()
    {
        KitchenSettings.Instance.AutoSave = false;
        SaveLoadManager.LastPath = "";
        Make("QuitBoard", new Vector3Int(800, 400, 18), Vector3.zero);

        Assert.IsFalse(AutoSaveManager.SaveOnQuit(), "при выключенном автосохранении — не пишем");
        Assert.IsFalse(File.Exists(_autoSavePath!), "файл не создан");
    }

    // Полный круг бага: открыли файл, поменяли сцену, «закрыли» (SaveOnQuit),
    // снова загрузили этот файл — правки на месте.
    [Test]
    public void OpenFile_ChangeScene_QuitSave_Reload_SeesChanges()
    {
        KitchenSettings.Instance.AutoSave = true;

        // «Открыли» файл с деталью в позиции A.
        var board = Make("RoundTrip", new Vector3Int(800, 400, 18), new Vector3(0f, 0.2f, 0f));
        Assert.IsTrue(SaveLoadManager.SaveToPath(_openFilePath!));
        Assert.AreEqual(_openFilePath!, SaveLoadManager.LastPath);

        // Подвинули деталь в позицию B и «закрыли» программу.
        board.transform.position = new Vector3(1.5f, 0.2f, 0f);
        Assert.IsTrue(AutoSaveManager.SaveOnQuit());

        // Имитация перезапуска: чистим сцену и грузим тот же файл (как делает старт).
        PartRegistry.Unregister(board);
        Object.DestroyImmediate(board.gameObject);
        _spawned.Clear();

        Assert.IsTrue(SaveLoadManager.LoadFromPath(_openFilePath!));
        var restored = Object.FindObjectsByType<KitchenElement>();
        Assert.AreEqual(1, restored.Length);
        Assert.AreEqual(1.5f, restored[0].transform.position.x, 0.001f, "правка сохранилась и загрузилась");

        Object.DestroyImmediate(restored[0].gameObject);
    }
}
