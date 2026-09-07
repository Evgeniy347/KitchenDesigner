using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Демо-проект лежит в папке УСТАНОВКИ, и при per-user установке она
/// доступна на запись — значит защищает не файловая система, а код. Опасны
/// ровно два пути: автосохранение (пишет в открытый проект каждую минуту) и
/// «Сохранить» без выбора файла. Оба обязаны промахнуться мимо образца.
///
/// Второе, что здесь проверяется, — что режим ОДНОРАЗОВЫЙ: сохранение к себе
/// снимает его немедленно и навсегда, иначе пользователь остался бы с копией,
/// которую всё ещё нельзя править.</summary>
public class DemoModeSaveTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevAutoSave;
    private string? _prevLastPath;
    private string? _demoPath;
    private string? _myPath;

    private void Make(string name)
    {
        var go = new GameObject(name);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = new Vector3Int(800, 400, 18);
        PartRegistry.Register(element);
        _spawned.Add(go);
    }

    [SetUp]
    public void SetUp()
    {
        _prevAutoSave = KitchenSettings.Instance.AutoSave;
        _prevLastPath = SaveLoadManager.LastPath;

        _demoPath = Path.Combine(Application.temporaryCachePath, "demo_readonly.json");
        _myPath = Path.Combine(Application.temporaryCachePath, "demo_copy.json");
        Delete(_demoPath);
        Delete(_myPath);

        DemoMode.ResetCurrent();
    }

    [TearDown]
    public void TearDown()
    {
        DemoMode.ResetCurrent();
        if (KitchenSettings.Instance != null)
            KitchenSettings.Instance.AutoSave = _prevAutoSave;
        SaveLoadManager.LastPath = _prevLastPath!;

        foreach (var go in _spawned)
        {
            if (go == null) continue;
            PartRegistry.Unregister(go.GetComponent<KitchenElement>());
            Object.DestroyImmediate(go);
        }
        _spawned.Clear();

        Delete(_demoPath);
        Delete(_myPath);
    }

    private static void Delete(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path!)) File.Delete(path!);
    }

    [Test]
    public void SaveOnQuit_InDemoMode_WritesNothing()
    {
        KitchenSettings.Instance.AutoSave = true;
        SaveLoadManager.LastPath = _demoPath!;
        DemoMode.Current.Enter(_demoPath!);
        Make("DemoBoard");

        Assert.IsFalse(AutoSaveManager.SaveOnQuit(),
            "автосохранение целится в открытый проект — то есть прямо в образец в папке "
            + "установки; выключенным его обязан держать демо-режим, а не права на папку");
        Assert.IsFalse(File.Exists(_demoPath!),
            "ни одного байта в файл установки: перезапись образца пережила бы перезапуск "
            + "и досталась бы всем следующим запускам");
    }

    [Test]
    public void SaveToPath_OntoTheDemoFile_IsRefused()
    {
        DemoMode.Current.Enter(_demoPath!);
        Make("DemoBoard");

        Assert.IsFalse(SaveLoadManager.SaveToPath(_demoPath!),
            "«Сохранить» поверх образца — не сохранение к себе, а порча демонстрации");
        Assert.IsFalse(File.Exists(_demoPath!));
        Assert.IsTrue(DemoMode.Current.IsActive, "отказ не имеет права снять защиту");
    }

    [Test]
    public void SaveToPath_Elsewhere_LiftsTheModeAndKeepsTheNewPath()
    {
        DemoMode.Current.Enter(_demoPath!);
        Make("DemoBoard");

        Assert.IsTrue(SaveLoadManager.SaveToPath(_myPath!), "копия к себе сохраняется как обычно");

        Assert.IsFalse(DemoMode.Current.IsActive, "демо-режим снят сохранением, без перезапуска");
        Assert.AreEqual(_myPath!, SaveLoadManager.LastPath,
            "дальше правится уже новый путь — иначе следующее «Сохранить» вернулось бы к образцу");
        StringAssert.Contains("DemoBoard", File.ReadAllText(_myPath!));
    }

    [Test]
    public void AfterSavingACopy_AutoSaveWorksAgain()
    {
        KitchenSettings.Instance.AutoSave = true;
        DemoMode.Current.Enter(_demoPath!);
        Make("DemoBoard");
        Assert.IsTrue(SaveLoadManager.SaveToPath(_myPath!));

        Assert.IsTrue(AutoSaveManager.SaveOnQuit(),
            "своя копия автосохраняется как любой проект: демо-режим выключает автосохранение "
            + "на время, а не навсегда");
        Assert.IsFalse(File.Exists(_demoPath!), "и по-прежнему мимо образца");
    }
}
