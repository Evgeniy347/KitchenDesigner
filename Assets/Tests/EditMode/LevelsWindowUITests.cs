using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// L6b (план LEVELS): окно «Этажи» — имя/отметка/высота живьём применяются к
/// LevelRegistry, «+» добавляет уровень сверху (LevelPlacement.NextAbove, как и
/// CreateLevelCommand.AboveTop в L4), удаление — через ConfirmDeleteButton и
/// запрещено для последнего оставшегося уровня.
/// </summary>
public class LevelsWindowUITests
{
    private GameObject? _canvasGo;

    [SetUp]
    public void SetUp()
    {
        _canvasGo = new GameObject("Canvas");
        _canvasGo.AddComponent<Canvas>();
        LevelRegistry.Reset();
        CommandStack.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        LevelRegistry.Reset();
        CommandStack.Clear();
    }

    private LevelsWindowUI Build()
    {
        var ui = _canvasGo!.AddComponent<LevelsWindowUI>();
        ui.Build(_canvasGo.transform);
        return ui;
    }

    private static void SetTwoLevels() => LevelRegistry.Set(new[]
    {
        new Level("1", "1 этаж", 0, 3000),
        new Level("2", "2 этаж", 3000, 2800),
    });

    [Test]
    public void Build_RegistersWithProjectWindows()
    {
        var ui = Build();
        Assert.AreEqual("levels", ui.WindowId);
        CollectionAssert.Contains(new List<IProjectWindow>(ProjectWindows.All), ui);
    }

    [Test]
    public void SetVisible_True_ShowsTheWindow_AndRefreshesRows()
    {
        SetTwoLevels();
        var ui = Build();

        Assert.IsFalse(ui.IsVisible);
        ui.SetVisible(true);
        Assert.IsTrue(ui.IsVisible);

        var nameFields = ui.GetComponentsInChildren<TMP_InputField>(true)
            .Where(f => f.gameObject.name.StartsWith("LvName_")).ToList();
        Assert.AreEqual(2, nameFields.Count, "по одному полю имени на уровень");
    }

    [Test]
    public void Refresh_ShowsEachLevelsNameElevationAndHeight()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var byName = ui.GetComponentsInChildren<TMP_InputField>(true)
            .ToDictionary(f => f.gameObject.name, f => f.text);

        Assert.AreEqual("1 этаж", byName["LvName_1"]);
        Assert.AreEqual("0", byName["LvElevation_1"]);
        Assert.AreEqual("3000", byName["LvHeight_1"]);
        Assert.AreEqual("2 этаж", byName["LvName_2"]);
        Assert.AreEqual("3000", byName["LvElevation_2"]);
        Assert.AreEqual("2800", byName["LvHeight_2"]);
    }

    [Test]
    public void EditingTheNameField_LiveAppliesToTheLevel()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var nameField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvName_1");
        nameField.text = "Подвал";
        nameField.onEndEdit.Invoke("Подвал");

        Assert.AreEqual("Подвал", LevelRegistry.Items.First(l => l.id == "1").name);
    }

    [Test]
    public void EditingTheElevationField_LiveAppliesToTheLevel()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var elevationField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvElevation_2");
        elevationField.text = "3200";
        elevationField.onEndEdit.Invoke("3200");

        Assert.AreEqual(3200, LevelRegistry.Items.First(l => l.id == "2").floorElevationMm);
    }

    [Test]
    public void AddButton_CreatesANewLevel_AboveTheTop()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var addButton = ui.GetComponentsInChildren<Button>(true).First(b => b.gameObject.name == "LvAdd");
        addButton.onClick.Invoke();

        Assert.AreEqual(3, LevelRegistry.Items.Count);
        var newLevel = LevelRegistry.Items.OrderByDescending(l => l.floorElevationMm).First();
        Assert.AreEqual(3000 + 2800, newLevel.floorElevationMm,
            "новый уровень встаёт на отметку верхнего плюс его высоту");
    }

    [Test]
    public void DeleteButton_IsDisabled_WhenOnlyOneLevelIsLeft()
    {
        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000) });
        var ui = Build();
        ui.SetVisible(true);

        var deleteButton = ui.GetComponentsInChildren<Button>(true)
            .First(b => b.gameObject.name == "LvDelete_1");
        Assert.IsFalse(deleteButton.interactable,
            "последний оставшийся уровень нельзя удалить — проекту нужен хотя бы один");
    }

    [Test]
    public void DeleteButton_SecondClickRemovesTheLevel()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var deleteButton = ui.GetComponentsInChildren<Button>(true)
            .First(b => b.gameObject.name == "LvDelete_2");
        Assert.IsTrue(deleteButton.interactable);

        deleteButton.onClick.Invoke();
        Assert.AreEqual(2, LevelRegistry.Items.Count, "первый клик только взводит подтверждение");

        deleteButton.onClick.Invoke();
        Assert.AreEqual(1, LevelRegistry.Items.Count);
        Assert.IsFalse(LevelRegistry.Items.Any(l => l.id == "2"));
    }
}
