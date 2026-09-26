using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>Баг-репорт (release v0.1963): пользователь работает в проекте и открывает
/// «Загрузить» — «Недавних проектов пока нет». Корень: первое «Сохранить» БЕЗ ранее
/// открытого файла шло веткой quicksave (SaveProject + присваивание LastPath напрямую) и
/// никогда не регистрировало путь в списке недавних — единственная ветка ProjectFileActions,
/// которая этого не делала. <see cref="SaveCurrent_NoLastPath_QuickSaves_AndRegistersInRecentProjects"/>
/// красный на коде ДО фикса (ProjectFileActions.cs, ветка SaveProject(QuickSaveName)) и зелёный
/// после перехода на SaveLoadManager.AdoptCurrentPath.</summary>
public class ProjectFileActionsTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private string? _prevLastPath;
    private string[]? _recentBackup;

    [SetUp]
    public void SetUp()
    {
        DemoMode.ResetCurrent();
        _prevLastPath = SaveLoadManager.LastPath;
        _recentBackup = RecentProjectsTestBackup.Capture();
        SaveLoadManager.LastPath = "";
        RecentProjectsTestBackup.Restore(new string[0]);
    }

    [TearDown]
    public void TearDown()
    {
        DemoMode.ResetCurrent();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        var quickPath = SaveLoadManager.PathForName(ProjectFileActions.QuickSaveName);
        if (File.Exists(quickPath)) File.Delete(quickPath);

        SaveLoadManager.LastPath = _prevLastPath!;
        RecentProjectsTestBackup.Restore(_recentBackup!);
    }

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

    [Test]
    public void SaveCurrent_NoLastPath_QuickSaves_AndRegistersInRecentProjects()
    {
        Make("QuickBoard", new Vector3Int(600, 300, 18), Vector3.zero);
        Assert.IsFalse(SaveLoadManager.HasLastPath, "предпосылка: файл ещё не открывался");

        new ProjectFileActions().SaveCurrent();

        Assert.IsTrue(SaveLoadManager.HasLastPath, "первое «Сохранить» обязано сделать файл текущим");
        CollectionAssert.Contains(RecentProjects.Paths(), SaveLoadManager.LastPath,
            "первое «Сохранить» без ранее открытого файла обязано попасть в «Загрузить» — "
            + "это ровно путь из отчёта об ошибке");
    }

    [Test]
    public void SaveCurrent_WithLastPath_KeepsItInRecentProjects()
    {
        var path = Path.Combine(Application.temporaryCachePath, "pfa_savecurrent_lastpath.json");
        Make("Board", new Vector3Int(600, 300, 18), Vector3.zero);
        Assert.IsTrue(SaveLoadManager.SaveToPath(path));
        RecentProjectsTestBackup.Restore(new string[0]);

        new ProjectFileActions().SaveCurrent();

        CollectionAssert.Contains(RecentProjects.Paths(), path,
            "«Сохранить» в уже открытый файл обязано держать его в списке недавних");
        File.Delete(path);
    }
}
