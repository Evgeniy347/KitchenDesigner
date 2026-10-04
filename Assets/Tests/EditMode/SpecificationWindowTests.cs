using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// T7 — окно «Спецификация» на DataTable и WindowChrome: настоящая таблица вместо TMP-текста с <pos>,
// шапка прибита, числа вправо, подытоги и итоги — строками, футер «Позиций … | Копировать | Экспорт CSV…»,
// «Закрыть» рядом с × нет, пустое состояние D9.
public class SpecificationWindowTests
{
    private GameObject? _canvas;
    private SpecificationPanelUI? _ui;

    [SetUp]
    public void SetUp()
    {
        LevelRegistry.Reset();
        PartRegistry.Clear();
        _canvas = UiTestCanvas.Create("SpecCanvas");
        _ui = _canvas.AddComponent<SpecificationPanelUI>();
        _ui.Build(_canvas.transform);
    }

    [TearDown]
    public void TearDown()
    {
        UiTestCanvas.Release(_canvas);
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();
    }

    private Transform Panel => _canvas!.transform.Find("SpecPanel")!;

    private Button FooterButton(string node) => Panel.Find("SpecPanelFooter/" + node)!.GetComponent<Button>();

    private static void SpawnShelf(string name, float z) =>
        ElementFactory.CreatePart(new Vector3Int(564, 400, 18), name, new Vector3(0f, 0.5f, z));

    [Test]
    public void Build_UsesTheSpecificationSizeToken_TitleLeft_AndNothingAboveTheTable()
    {
        var panel = (RectTransform)Panel;

        Assert.AreEqual(UIStyle.SpecificationSize, panel.sizeDelta, "960×640 из D5, а не 860×640 по месту");
        var title = Panel.Find("SpecPanelTitle")!.GetComponent<TMP_Text>();
        Assert.AreEqual("Спецификация", title.text);
        Assert.AreEqual(TextAlignmentOptions.Left, title.alignment, "заголовок слева (D5)");
        Assert.IsNull(Panel.Find("SpecHeaders"), "шапка — часть DataTable, а не отдельный TMP-текст с <pos>");
        Assert.IsNull(Panel.Find("SpecContent"), "тело — строки таблицы, а не один TMP-текст");
    }

    [Test]
    public void TheTable_HasTheNineColumns_WithTheUnitsInTheHeaders()
    {
        var table = _ui!.Table;

        string[] headers = table.Columns.Select(c => c.Header).ToArray();
        CollectionAssert.AreEqual(
            new[] { "№", "Наименование", "Материал", "Ш, мм", "В, мм", "Г, мм", "Дет.", "Кол-во", "Ед." }, headers);
        Assert.AreEqual(UIStyle.TableHeaderH, table.Header.sizeDelta.y, "шапка прибита и 32 высотой");
        Assert.IsFalse(table.Header.IsChildOf(table.Body.Content), "прокручивается только тело");
    }

    [Test]
    public void EmptyScene_ShowsTheEmptyState_DisablesExportAndCopy_AndCountsZero()
    {
        _ui!.SetVisible(true);

        Assert.AreEqual(0, _ui.Table.ShownRows.Count);
        Assert.IsTrue(_ui.Table.Empty!.Root.gameObject.activeSelf);
        Assert.AreEqual("Спецификация пуста", _ui.Table.Empty.Title.text);
        Assert.IsFalse(FooterButton(SpecificationPanelUI.ExportNode).interactable, "выгружать нечего");
        Assert.IsFalse(FooterButton(SpecificationPanelUI.CopyNode).interactable, "копировать нечего");
        Assert.AreEqual("Позиций: 0 · Разделов: 0", _ui.Count.text);
    }

    [Test]
    public void FilledScene_FillsTheTable_EnablesTheButtons_AndCountsPositionsAndSections()
    {
        SpawnShelf("Полка А", 0f);
        SpawnShelf("Полка Б", -1f);
        _ui!.SetVisible(true);

        int positions = _ui.Table.ShownRows.Count(r => r.Kind == DataRowKind.Item);
        int sections = _ui.Table.ShownRows.Count(r => r.Kind == DataRowKind.Group);
        Assert.Greater(positions, 0, "предпосылка: детали попали в спецификацию");
        Assert.IsFalse(_ui.Table.Empty!.Root.gameObject.activeSelf);
        Assert.IsTrue(FooterButton(SpecificationPanelUI.ExportNode).interactable);
        Assert.IsTrue(FooterButton(SpecificationPanelUI.CopyNode).interactable);
        Assert.AreEqual($"Позиций: {positions} · Разделов: {sections}", _ui.Count.text,
            "счётчик футера — число пронумерованных позиций и строк-групп таблицы");
    }

    [Test]
    public void ALongName_GetsItsTooltipAtOpening_BecauseTheRowsAreMeasuredInAnActiveWindow()
    {
        SpawnShelf(new string('Ж', 90), 0f);

        _ui!.SetVisible(true);

        int row = _ui.Table.ShownRows.ToList().FindIndex(r => r.Kind == DataRowKind.Item);
        var name = _ui.Table.CellLabel(row, SpecificationRows.NameColumn)!;
        Assert.IsNotNull(name.GetComponent<UnityEngine.EventSystems.EventTrigger>(),
            "обрезанное имя несёт tooltip с полным текстом: ширина текста меряется после активации окна, "
            + "в неактивной иерархии TMP отдаёт 10 % от настоящей и обрезание не замечается");
    }

    [Test]
    public void TheFooter_CountOnTheLeft_CopySecondary_ExportPrimaryOnTheFarRight()
    {
        var count = (RectTransform)Panel.Find("SpecPanelFooter/" + SpecificationPanelUI.CountNode)!;
        var copy = (RectTransform)FooterButton(SpecificationPanelUI.CopyNode).transform;
        var export = (RectTransform)FooterButton(SpecificationPanelUI.ExportNode).transform;

        Assert.AreEqual(0f, count.anchorMin.x, "счётчик слева");
        Assert.AreEqual(1f, export.anchorMin.x, "основная кнопка — крайняя справа");
        Assert.AreEqual(1f, copy.anchorMin.x);
        Assert.Less(copy.anchoredPosition.x, export.anchoredPosition.x - export.sizeDelta.x + 0.5f,
            "«Копировать» слева от «Экспорта» (D5: Отмена | Основная)");
        Assert.AreEqual(UIStyle.Accent, export.GetComponent<Image>().color);
        Assert.AreEqual("Экспорт CSV…", export.GetComponentInChildren<TMP_Text>().text, "открывает диалог — многоточие");
    }

    [Test]
    public void ThereIsNoCloseButtonNextToTheCross_AndTheCrossCloses()
    {
        _ui!.SetVisible(true);
        string close = Loc.T("common.close");
        var captions = Panel.GetComponentsInChildren<Button>(true)
            .Where(b => b.name != WindowChrome.CloseButtonName)
            .SelectMany(b => b.GetComponentsInChildren<TMP_Text>(true))
            .Select(t => t.text);
        CollectionAssert.DoesNotContain(captions, close, "две двери с одним смыслом (D5, NN/g)");

        Panel.Find(WindowChrome.CloseButtonName)!.GetComponent<Button>().onClick.Invoke();
        Assert.IsFalse(_ui.IsVisible);
    }

    [Test]
    public void TheTable_FillsTheBodyBetweenTheTitleBarAndTheFooter_WithoutRowsUnderTheButtons()
    {
        var table = (RectTransform)Panel.Find(SpecificationPanelUI.TableNode)!;
        var panel = (RectTransform)Panel;

        Assert.AreEqual(UIStyle.TitleBarH, -table.anchoredPosition.y, 0.5f);
        Assert.AreEqual(panel.sizeDelta.y - UIStyle.FooterH, -table.anchoredPosition.y + table.sizeDelta.y, 0.5f,
            "таблица кончается там, где начинается футер: нижние строки уже не уходят под кнопки (аудит)");
    }
}
