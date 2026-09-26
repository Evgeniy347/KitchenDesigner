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

    /// <summary>H4 (review-ui-mcp): раньше поле имени писало напрямую в поле объекта Level
    /// мимо CommandStack — правка применялась, но Ctrl+Z не делал ничего. Теперь то же
    /// применение обязано лечь ровно одним шагом отмены.</summary>
    [Test]
    public void EditingTheNameField_LiveAppliesToTheLevel_AndUndoRestoresIt()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var nameField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvName_1");
        nameField.text = "Подвал";
        nameField.onEndEdit.Invoke("Подвал");

        Assert.AreEqual("Подвал", LevelRegistry.Items.First(l => l.id == "1").name);
        Assert.AreEqual(1, CommandStack.UndoCount,
            "переименование обязано лечь в стек РОВНО одним шагом");

        CommandStack.Undo();
        Assert.AreEqual("1 этаж", LevelRegistry.Items.First(l => l.id == "1").name,
            "Ctrl+Z обязан вернуть прежнее имя — раньше правка шла мимо CommandStack");
    }

    /// <summary>H4 + addendum#1 (review-ui-mcp, review-persistence): смена отметки была
    /// прямой записью в поле без отмены. Теперь это команда, и — раз этаж переехал — его
    /// детали обязаны переехать вместе с ним, одним шагом.</summary>
    [Test]
    public void EditingTheElevationField_LiveAppliesToTheLevel_MovesItsElements_AndUndoRestoresBoth()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var element = ElementFactory.CreatePart(new Vector3Int(600, 18, 500), "Board", Vector3.zero)
            .GetComponent<KitchenElement>();
        element.LevelId = "2";
        PartRegistry.Register(element);
        var yBefore = element.transform.position.y;

        var elevationField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvElevation_2");
        elevationField.text = "3200";
        elevationField.onEndEdit.Invoke("3200");

        Assert.AreEqual(3200, LevelRegistry.Items.First(l => l.id == "2").floorElevationMm);
        Assert.AreEqual(1, CommandStack.UndoCount, "отметка обязана лечь одним шагом отмены");
        Assert.AreEqual(yBefore + 0.2f, element.transform.position.y, 1e-4f,
            "деталь второго этажа обязана переехать вместе с отметкой (200 мм = 0,2 м)");

        CommandStack.Undo();
        Assert.AreEqual(3000, LevelRegistry.Items.First(l => l.id == "2").floorElevationMm);
        Assert.AreEqual(yBefore, element.transform.position.y, 1e-4f,
            "отмена обязана вернуть и отметку, и деталь на прежнее место");

        PartRegistry.Unregister(element);
        Object.DestroyImmediate(element.gameObject);
    }

    [Test]
    public void EditingTheHeightField_AppliesTheChange_InOneUndoStep()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var heightField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvHeight_2");
        heightField.text = "3100";
        heightField.onEndEdit.Invoke("3100");

        Assert.AreEqual(3100, LevelRegistry.Items.First(l => l.id == "2").heightMm);
        Assert.AreEqual(1, CommandStack.UndoCount);

        CommandStack.Undo();
        Assert.AreEqual(2800, LevelRegistry.Items.First(l => l.id == "2").heightMm);
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

    /// <summary>H3 (review-ui-mcp): на пустом реестре Snapshot() отдаёт ВИРТУАЛЬНЫЙ первый
    /// уровень, которого нет в LevelRegistry.Items. «+» считал его настоящим и добавлял
    /// только второй — первый этаж после этого не существовал вовсе. Теперь «+» на пустом
    /// реестре заводит оба уровня одним шагом отмены.</summary>
    [Test]
    public void AddButton_OnAnEmptyRegistry_SeedsTheGroundFloorToo()
    {
        Assume.That(LevelRegistry.Items, Is.Empty, "предусловие: реестр пуст, как при старте без демо-проекта");
        var ui = Build();
        ui.SetVisible(true);

        var addButton = ui.GetComponentsInChildren<Button>(true).First(b => b.gameObject.name == "LvAdd");
        addButton.onClick.Invoke();

        Assert.AreEqual(2, LevelRegistry.Items.Count,
            "обязаны появиться ОБА уровня — виртуальный первый и новый второй");
        Assert.IsTrue(LevelRegistry.Items.Any(l => l.id == "1"),
            "первый этаж не должен пропасть: до правки он существовал только виртуально");

        CommandStack.Undo();
        Assert.IsEmpty(LevelRegistry.Items, "отмена обязана вернуть реестр в исходное пустое состояние");
    }

    /// <summary>H5 (review-ui-mcp): окно не подписывалось ни на что, и строки переживали
    /// Ctrl+Z снаружи — второй клик по «×» такой строки вызывал DeleteLevelCommand с
    /// несуществующим id и падал ArgumentException внутри обработчика кнопки. Теперь окно
    /// перестраивает строки на любое внешнее изменение реестра, пока оно открыто.</summary>
    [Test]
    public void UndoOutsideTheWindow_RefreshesTheOpenRows()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);
        CommandStack.Execute(new CreateLevelCommand(new Level("3", "3 этаж", 6000, 3000)));
        Assert.IsTrue(ui.GetComponentsInChildren<TMP_InputField>(true)
            .Any(f => f.gameObject.name == "LvName_3"), "предусловие: строка третьего этажа отрисована");

        CommandStack.Undo();

        Assert.IsFalse(ui.GetComponentsInChildren<TMP_InputField>(true)
                .Any(f => f.gameObject.name == "LvName_3"),
            "строка отменённого уровня обязана исчезнуть без ручного Refresh()");
    }

    /// <summary>Авто-Refresh выше делает живую «зависшую» строку недостижимой из UI — она
    /// уничтожается вместе с перестройкой. Остаётся прямая проверка защитной калитки в
    /// DeleteLevel: если строка всё-таки держит Level с id, которого больше нет в реестре
    /// (например, окно было закрыто в момент внешнего изменения — OnLevelRegistryChanged
    /// тогда Refresh() не зовёт), клик не обязан ронять ArgumentException из
    /// DeleteLevelCommand — только его конструктор.</summary>
    [Test]
    public void DeleteLevel_GuardsAgainstAnIdNoLongerInTheRegistry_InsteadOfThrowing()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);
        var dangling = new Level("2", "2 этаж", 3000, 2800);
        LevelRegistry.Remove("2");

        Assert.DoesNotThrow(() => ui.DeleteLevel(dangling),
            "деталь окна не обязана падать ArgumentException, если строка ссылается на " +
            "уровень, которого уже нет в реестре");
    }

    /// <summary>L2 (review-ui-mcp): у столбцов не было подписей, а числовые поля отметки и
    /// высоты создавались с суффиксом "" вместо "мм" (UI-GUIDELINES §1/§5).</summary>
    [Test]
    public void Build_HasColumnHeaders_AndNumberFieldsShowMillimetreSuffix()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var headerTexts = ui.GetComponentsInChildren<TMP_Text>(true)
            .Where(t => t.gameObject.name.StartsWith("LvHeader"))
            .Select(t => t.text).ToList();
        CollectionAssert.Contains(headerTexts, "Имя");
        CollectionAssert.Contains(headerTexts, "Отметка");
        CollectionAssert.Contains(headerTexts, "Высота");

        var elevationField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvElevation_1");
        var suffix = elevationField.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t => t.gameObject.name != elevationField.gameObject.name && t.text == "мм");
        Assert.NotNull(suffix, "числовое поле отметки обязано показывать суффикс «мм» внутри поля (§1)");
    }

    /// <summary>L2 (review-ui-mcp): с седьмого уровня последняя строка (y=-126) залезала на
    /// кнопку «+» (-150..-120) — окно не росло вместе с содержимым.</summary>
    [Test]
    public void SevenLevels_DoNotOverlapTheAddButton()
    {
        var levels = new Level[7];
        for (int i = 0; i < 7; i++) levels[i] = new Level((i + 1).ToString(), $"{i + 1} этаж", i * 3000, 3000);
        LevelRegistry.Set(levels);
        var ui = Build();
        ui.SetVisible(true);

        var addButton = ui.GetComponentsInChildren<Button>(true).First(b => b.gameObject.name == "LvAdd");
        var addRect = addButton.GetComponent<RectTransform>();
        float addTop = addRect.anchoredPosition.y + addRect.rect.height * 0.5f;

        var lastRow = ui.GetComponentsInChildren<TMP_InputField>(true)
            .Where(f => f.gameObject.name.StartsWith("LvName_"))
            .OrderBy(f => f.GetComponent<RectTransform>().anchoredPosition.y)
            .First();
        float lastRowBottom = lastRow.GetComponent<RectTransform>().anchoredPosition.y
            - lastRow.GetComponent<RectTransform>().rect.height * 0.5f;

        Assert.LessOrEqual(addTop, lastRowBottom,
            "кнопка «+» обязана остаться НИЖЕ последней строки — окно должно вырасти под 7 этажей");
    }
}
