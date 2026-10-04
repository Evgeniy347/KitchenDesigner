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
        CollectionAssert.Contains(headerTexts, "Название");
        CollectionAssert.Contains(headerTexts, "Отметка");
        CollectionAssert.Contains(headerTexts, "Высота");

        var elevationField = ui.GetComponentsInChildren<TMP_InputField>(true)
            .First(f => f.gameObject.name == "LvElevation_1");
        var suffix = elevationField.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t => t.gameObject.name != elevationField.gameObject.name && t.text == "мм");
        Assert.NotNull(suffix, "числовое поле отметки обязано показывать суффикс «мм» внутри поля (§1)");
    }

    /// <summary>L2 (review-ui-mcp): с седьмого уровня последняя строка залезала на кнопку «+» —
    /// окно не росло вместе с содержимым. Сравнение в мировых координатах, а не по
    /// anchoredPosition: у строк теперь якорь сверху слева, у кнопки он тоже, но одна ошибка
    /// в якоре не должна прятать наложение.</summary>
    [Test]
    public void SevenLevels_DoNotOverlapTheAddButton()
    {
        var levels = new Level[7];
        for (int i = 0; i < 7; i++) levels[i] = new Level((i + 1).ToString(), $"{i + 1} этаж", i * 3000, 3000);
        LevelRegistry.Set(levels);
        var ui = Build();
        ui.SetVisible(true);

        float addTop = WorldBounds(ui.GetComponentsInChildren<Button>(true).First(b => b.gameObject.name == "LvAdd")
            .GetComponent<RectTransform>()).yMax;
        float lastRowBottom = ui.GetComponentsInChildren<TMP_InputField>(true)
            .Where(f => f.gameObject.name.StartsWith("LvName_"))
            .Min(f => WorldBounds(f.GetComponent<RectTransform>()).yMin);

        Assert.LessOrEqual(addTop, lastRowBottom,
            "кнопка «+ Этаж» обязана остаться НИЖЕ последней строки — окно должно вырасти под 7 этажей");
    }

    private static Rect WorldBounds(RectTransform rt)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    private static TMP_Text Glyph(Component button) => button.GetComponentInChildren<TMP_Text>(true);

    [Test]
    public void TheWindow_IsAToolPanelOfTheLevelsWidth_WithAQuietCloseAndNoFooter()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);

        var panel = ui.WindowRect!;
        Assert.AreEqual(UIStyle.LevelsW, panel.sizeDelta.x, "ширина окна «Этажи» — токен D5 LevelsW");
        Assert.IsNotNull(panel.Find(WindowChrome.CloseButtonName), "закрытие — тихий × шапки");
        Assert.IsNull(panel.Find("LevelsWindowFooter"),
            "у окна нет футера: «+ Этаж» стоит под таблицей, а не в футере, потому что строит список");
    }

    [Test]
    public void OnlyTheCurrentLevel_CarriesTheSelectionMarker_AndItFollowsTheCurrentId()
    {
        SetTwoLevels();
        LevelRegistry.CurrentId = "1";
        var ui = Build();
        ui.SetVisible(true);

        var markers = ui.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith(LevelsListView.CurrentMarkerNode)).Select(t => t.name).ToList();
        CollectionAssert.AreEqual(new[] { LevelsListView.CurrentMarkerNode + "1" }, markers,
            "точка стоит ровно у текущего этажа (§ tool-panels: текущий этаж — точка SelectionBar)");

        var dot = ui.GetComponentsInChildren<Transform>(true).First(t => t.name == "Dot").GetComponent<Image>();
        Assert.AreEqual(UIStyle.SelectionBar, dot.color, "цвет точки — токен SelectionBar");

        LevelRegistry.CurrentId = "2";
        ui.Refresh();
        markers = ui.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith(LevelsListView.CurrentMarkerNode)).Select(t => t.name).ToList();
        CollectionAssert.AreEqual(new[] { LevelsListView.CurrentMarkerNode + "2" }, markers,
            "смена текущего этажа переставляет точку");
    }

    [Test]
    public void DeleteButton_IsQuietAtRest_AndDangerWhenArmed()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);
        var button = ui.GetComponentsInChildren<Button>(true).First(b => b.gameObject.name == "LvDelete_2");
        var image = button.GetComponent<Image>();

        Assert.AreEqual(UIStyle.GlyphClose, Glyph(button).text, "в покое — «×»");
        Assert.AreEqual(UIStyle.TextSecondary, Glyph(button).color, "в покое × тихий: вторичный цвет текста");
        Assert.AreEqual(0f, button.colors.normalColor.a, 1e-4f, "в покое у кнопки нет заливки (D5/D8: тихий ×)");

        button.onClick.Invoke();
        Assert.AreEqual(UIStyle.GlyphConfirm, Glyph(button).text, "взвод показывает «?!»");
        Assert.AreEqual(UIStyle.Danger, image.color, "взведённая кнопка заливается Danger");
        Assert.AreEqual(UIStyle.TextOnAccent, Glyph(button).color);

        ConfirmDeleteButton.DisarmAll();
        Assert.AreEqual(UIStyle.GlyphClose, Glyph(button).text);
        Assert.AreEqual(UIStyle.TextSecondary, Glyph(button).color, "снятие взвода возвращает тихий вид");
        Assert.AreEqual(0f, button.colors.normalColor.a, 1e-4f);
    }

    [Test]
    public void TheOnlyLevelsDeleteButton_IsDimmedAsWellAsDisabled()
    {
        LevelRegistry.Set(new[] { new Level("1", "1 этаж", 0, 3000) });
        var ui = Build();
        ui.SetVisible(true);
        var button = ui.GetComponentsInChildren<Button>(true).First(b => b.gameObject.name == "LvDelete_1");

        Assert.IsFalse(button.interactable);
        Assert.AreEqual(UIStyle.TextDisabled, Glyph(button).color,
            "выключенный × гаснет — иначе он выглядит рабочим (§9)");
    }

    [Test]
    public void TheColumns_FitTheBody_InOrder_WithoutOverlap_AndNumbersAlignRight()
    {
        SetTwoLevels();
        var ui = Build();
        ui.SetVisible(true);
        var body = ui.WindowRect!.Find("LevelsWindowBody");
        Assert.IsNotNull(body, "тело окна собрано WindowChrome.CreateBody");

        string[] order = { "LvName_2", "LvElevation_2", "LvHeight_2", "LvDelete_2" };
        var rects = order.Select(n => WorldBounds(ui.GetComponentsInChildren<RectTransform>(true)
            .First(r => r.name == n))).ToList();
        for (int i = 1; i < rects.Count; i++)
            Assert.Greater(rects[i].xMin - rects[i - 1].xMax, UIStyle.Space1,
                $"{order[i - 1]} и {order[i]} не касаются и идут слева направо");

        var viewport = WorldBounds((RectTransform)body);
        Assert.LessOrEqual(rects[3].xMax, viewport.xMax + 0.5f, "× не выходит за тело окна");

        foreach (var n in new[] { "LvElevation_2", "LvHeight_2" })
        {
            var field = ui.GetComponentsInChildren<TMP_InputField>(true).First(f => f.name == n);
            Assert.AreEqual(TextAlignmentOptions.Right, field.textComponent!.alignment,
                "числа отметки и высоты — вправо (D8)");
        }
        foreach (var n in new[] { "LvHeaderElevation", "LvHeaderHeight" })
        {
            var header = ui.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == n);
            Assert.AreEqual(TextAlignmentOptions.Right, header.alignment, "шапка числового столбца — вправо");
            Assert.AreEqual(UIStyle.FontCaption, header.fontSize, "шапка колонки — FontCaption (D3)");
        }
    }

    [Test]
    public void ElevationField_ShowsTheTypographyOfAnIntegerInput_AndTakesBothMinusSigns()
    {
        LevelRegistry.Set(new[] { new Level("1", "Подвал", -2700, 2500), new Level("2", "1 этаж", 0, 3000) });
        var ui = Build();
        ui.SetVisible(true);
        var field = ui.GetComponentsInChildren<TMP_InputField>(true).First(f => f.name == "LvElevation_2");

        field.text = "−300";
        field.onEndEdit.Invoke("−300");

        Assert.AreEqual(-300, LevelRegistry.Items.First(l => l.id == "2").floorElevationMm,
            "типографский минус «−» читается так же, как «-» (NumberFormat.TryParseInt)");
        Assert.AreEqual("-300", field.text, "в поле вводимое значение — ASCII-минус, его читает и калькулятор");
    }
}
