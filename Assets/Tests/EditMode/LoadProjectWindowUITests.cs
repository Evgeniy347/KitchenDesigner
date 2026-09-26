using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

public class LoadProjectWindowUITests
{
    private GameObject? _canvasGo;
    private string[]? _recentBackup;
    private string? _prevLastPath;
    private readonly System.Collections.Generic.List<string> _tempFiles = new();

    [SetUp]
    public void SetUp()
    {
        _recentBackup = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
        _prevLastPath = SaveLoadManager.LastPath;
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_recentBackup != null)
            RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = _recentBackup;
        SaveLoadManager.LastPath = _prevLastPath!;
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        _tempFiles.Clear();
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    private string MakeProjectFile(string name, string appVersion)
    {
        string path = Path.Combine(Application.temporaryCachePath, name);
        File.WriteAllText(path, "{\"version\":1,\"appVersion\":\"" + appVersion + "\"}");
        _tempFiles.Add(path);
        return path;
    }

    private LoadProjectWindowUI Build()
    {
        var ui = _canvasGo!.AddComponent<LoadProjectWindowUI>();
        ui.Build(_canvasGo.transform);
        return ui;
    }

    [Test]
    public void Build_RegistersWithProjectWindows()
    {
        var ui = Build();
        Assert.AreEqual("loadProject", ui.WindowId);
        CollectionAssert.Contains(new System.Collections.Generic.List<IProjectWindow>(ProjectWindows.All), ui);
    }

    [Test]
    public void SetVisible_True_ShowsTheWindow_AndFalse_HidesIt()
    {
        var ui = Build();
        Assert.IsFalse(ui.IsVisible);
        ui.SetVisible(true);
        Assert.IsTrue(ui.IsVisible);
        ui.SetVisible(false);
        Assert.IsFalse(ui.IsVisible);
    }

    [Test]
    public void Build_HasNewProjectAndLoadButtons_WithTheirLabels()
    {
        Build();
        var newBtn = _canvasGo!.transform.Find("LoadProjectWindow/LoadNewProject");
        var loadBtn = _canvasGo.transform.Find("LoadProjectWindow/LoadOpenFile");
        Assert.IsNotNull(newBtn);
        Assert.IsNotNull(loadBtn);
        Assert.AreEqual("Новый проект", newBtn!.GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual("Загрузить", loadBtn!.GetComponentInChildren<TMP_Text>().text);
    }

    [Test]
    public void SetVisible_HeightNeverExceedsHalfTheCanvas()
    {
        var rect = (RectTransform)_canvasGo!.transform;
        rect.sizeDelta = new Vector2(900, 500);

        var ui = Build();
        ui.SetVisible(true);

        var panel = (RectTransform)ui.WindowRect!;
        Assert.LessOrEqual(panel.sizeDelta.y, 500 * LoadWindowLayout.MaxScreenHeightFraction + 0.5f);
    }

    [Test]
    public void ClickingAnExistingRecentRow_LoadsThatProject_AndClosesTheWindow()
    {
        var path = MakeProjectFile("lpwui_existing.kdproj", BuildInfo.Version);
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { path };

        var ui = Build();
        ui.SetVisible(true);

        var row = _canvasGo!.transform.Find(
            "LoadProjectWindow/LoadBody/LoadBodyBody/LoadBodyBodyContent/Row");
        Assert.IsNotNull(row, "строка существующего проекта обязана построиться");
        row!.GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(path, SaveLoadManager.LastPath);
        Assert.IsFalse(ui.IsVisible,
            "успешная загрузка из окна обязана закрыть его — иначе поверх сцены остаётся "
            + "модальное окно выбора проекта");
    }

    [Test]
    public void ClickingARecentRow_WhenTheFileIsCorrupt_LoadFails_AndTheWindowStaysOpen()
    {
        var path = Path.Combine(Application.temporaryCachePath, "lpwui_corrupt.kdproj");
        File.WriteAllText(path, "not valid json {");
        _tempFiles.Add(path);
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { path };
        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex(@"^\[SaveLoad\] Load failed:"));

        var ui = Build();
        ui.SetVisible(true);

        var row = _canvasGo!.transform.Find(
            "LoadProjectWindow/LoadBody/LoadBodyBody/LoadBodyBodyContent/Row");
        Assert.IsNotNull(row, "файл существует на диске — строка обязана быть кликабельной");
        row!.GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(ui.IsVisible,
            "загрузка провалилась — окно обязано остаться открытым, а не закрыться на отказ");
    }

    [Test]
    public void ClickingNewProject_WhenTheNativeDialogIsCancelled_TheWindowStaysOpen()
    {
        var ui = Build();
        ui.SetVisible(true);

        var newBtn = _canvasGo!.transform.Find("LoadProjectWindow/LoadNewProject");
        Assert.IsNotNull(newBtn);
        LogAssert.Expect(LogType.Assert, new System.Text.RegularExpressions.Regex("^Cancelling FileDialog"));
        newBtn!.GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(ui.IsVisible,
            "диалог сохранения в batch-режиме редактора всегда возвращает отмену — окно "
            + "обязано остаться открытым, а не закрыться до того, как известен результат");
    }

    [Test]
    public void ClickingLoadFile_WhenTheNativeDialogIsCancelled_TheWindowStaysOpen()
    {
        var ui = Build();
        ui.SetVisible(true);

        var loadBtn = _canvasGo!.transform.Find("LoadProjectWindow/LoadOpenFile");
        Assert.IsNotNull(loadBtn);
        LogAssert.Expect(LogType.Assert, new System.Text.RegularExpressions.Regex("^Cancelling FileDialog"));
        loadBtn!.GetComponent<Button>().onClick.Invoke();

        Assert.IsTrue(ui.IsVisible,
            "диалог открытия в batch-режиме редактора всегда возвращает отмену — окно "
            + "обязано остаться открытым, а не закрыться до того, как известен результат");
    }

    [Test]
    public void EmptyRecentList_ButACurrentProjectIsOpen_ShowsItsRow()
    {
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new string[0];
        var path = MakeProjectFile("lpwui_seeded_current.kdproj", BuildInfo.Version);
        SaveLoadManager.LastPath = path;

        var ui = Build();
        ui.SetVisible(true);

        var row = _canvasGo!.transform.Find(
            "LoadProjectWindow/LoadBody/LoadBodyBody/LoadBodyBodyContent/Row");
        Assert.IsNotNull(row,
            "список недавних пуст, но пользователь уже работает в проекте — окно обязано "
            + "показать хотя бы его, а не «Недавних проектов пока нет»");
    }

    [Test]
    public void EmptyRecentList_ShowsAHint_NotABlankArea()
    {
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new string[0];
        SaveLoadManager.LastPath = "";

        var ui = Build();
        ui.SetVisible(true);

        var hint = _canvasGo!.transform.Find(
            "LoadProjectWindow/LoadBody/LoadBodyBody/LoadBodyBodyContent/LoadEmptyHint");
        Assert.IsNotNull(hint);
    }
}
