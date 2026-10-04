using System.IO;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

// TMP отдаёт предпочтительную ширину в неактивной иерархии в десять раз меньше настоящей. Окна строят
// таблицу, пока скрыты, поэтому DataTable обязана пересчитать обрезание и tooltip при показе сама, а не
// ждать, что окно активируется раньше заполнения. Тест на каждое окно с DataTable: заполнить скрытым,
// показать, проверить tooltip и ширину.
public class DataTableHiddenFillTests
{
    private const string Long = "Очень длинное название которое заведомо не помещается ни в одну колонку окна ни при каком масштабе";

    private GameObject? _canvas;
    private string[]? _recentBackup;
    private string? _lastPath;
    private string? _file;

    [SetUp]
    public void SetUp()
    {
        LogAssert.ignoreFailingMessages = true;
        LevelRegistry.Reset();
        PartRegistry.Clear();
        _recentBackup = RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values;
        _lastPath = SaveLoadManager.LastPath;
        _canvas = UiTestCanvas.Create("HiddenFillCanvas");
    }

    [TearDown]
    public void TearDown()
    {
        UiTestCanvas.Release(_canvas);
        if (_recentBackup != null)
            RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = _recentBackup;
        SaveLoadManager.LastPath = _lastPath!;
        if (_file != null && File.Exists(_file)) File.Delete(_file);
        foreach (var e in Object.FindObjectsByType<KitchenElement>(FindObjectsSortMode.None))
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        ProjectWindows.Clear();
        LevelRegistry.Reset();
        LogAssert.ignoreFailingMessages = false;
    }

    private static bool HasTooltip(TMP_Text label) => label.GetComponent<EventTrigger>() != null;

    [Test]
    public void ATableFilledUnderAHiddenParent_GetsItsTooltipAndFullWidthMeasuredWhenShown()
    {
        var parent = UIFactory.CreateRect("Hidden", _canvas!.transform);
        parent.gameObject.SetActive(false);
        var table = DataTable.Create(parent, "T", new Vector2(400f, 200f), new[]
        {
            new DataColumn("n", "№", 40f, CellAlign.Right),
            new DataColumn("name", "Имя", 0f),
        });

        table.SetRows(new System.Collections.Generic.List<DataRow>
        {
            DataRow.Item("1", Long),
            DataRow.Item("2", "Коротко"),
        });
        parent.gameObject.SetActive(true);

        Assert.IsTrue(HasTooltip(table.CellLabel(0, 1)!),
            "обрезанный текст несёт tooltip, хотя строки построены скрытыми: таблица пересчитывает при показе");
        Assert.IsFalse(HasTooltip(table.CellLabel(1, 1)!), "короткому tooltip не нужен — и измеряется он по-настоящему, не в 10 раз меньше");
    }

    [Test]
    public void RowsBuiltWhileShown_AreNotBuiltTwice()
    {
        var table = DataTable.Create(_canvas!.transform, "T", new Vector2(400f, 200f), new[]
        {
            new DataColumn("name", "Имя", 0f),
        });
        int decorated = 0;
        table.RowDecorator = (_, _) => decorated++;

        table.SetRows(new System.Collections.Generic.List<DataRow> { DataRow.Item("a"), DataRow.Item("b") });

        Assert.AreEqual(2, decorated, "активная таблица строится один раз");
    }

    [Test]
    public void Specification_FilledHidden_ShowsTheTooltipOfALongName()
    {
        ElementFactory.CreatePart(new Vector3Int(564, 400, 18), new string('Ж', 90), Vector3.zero);
        var ui = _canvas!.AddComponent<SpecificationPanelUI>();
        ui.Build(_canvas.transform);

        ui.SetVisible(true);

        int row = ui.Table.ShownRows.ToList().FindIndex(r => r.Kind == DataRowKind.Item);
        Assert.IsTrue(HasTooltip(ui.Table.CellLabel(row, SpecificationRows.NameColumn)!));
    }

    [Test]
    public void Errors_FilledHidden_ShowsTheTooltipOfALongPartName()
    {
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), new string('Ж', 90), Vector3.zero);
        ElementFactory.CreatePart(new Vector3Int(600, 18, 500), new string('Ж', 90) + "2", Vector3.zero);
        var panel = _canvas!.AddComponent<ErrorPanelUI>();
        panel.Build(_canvas.transform);

        panel.SetVisible(true);

        Assume.That(panel.VisibleIssueCount, Is.GreaterThan(0));
        var long90 = Enumerable.Range(0, panel.VisibleIssueCount)
            .Select(i => panel.Table.CellLabel(i, IssueRows.PartColumn)!)
            .FirstOrDefault(l => l.text.Length > 60);
        Assert.IsNotNull(long90, "предпосылка: в колонке «Деталь» есть длинное имя");
        Assert.IsTrue(HasTooltip(long90!), "длинное имя детали несёт tooltip, окно заполнено скрытым");
    }

    [Test]
    public void OpenProject_FilledHidden_ShowsTheTooltip_AndTheBadgeIsAsWideAsItsText()
    {
        _file = Path.Combine(Application.temporaryCachePath, new string('z', 120) + ".kdproj");
        File.WriteAllText(_file, "{\"version\":1,\"appVersion\":\"99.0\"}");
        RecentProjectsMemory.For(System.Environment.GetCommandLineArgs()).Values = new[] { _file };
        SaveLoadManager.LastPath = "";
        var ui = _canvas!.AddComponent<LoadProjectWindowUI>();
        ui.Build(_canvas.transform);

        ui.SetVisible(true);

        Assert.IsTrue(HasTooltip(ui.Table.CellLabel(0, LoadProjectRows.NameColumn)!), "имя файла обрезано — полное в tooltip");
        var badge = (RectTransform)ui.Table.RowRect(0).Find(RowBadge.NodePrefix + "newer")!;
        Assert.Greater(badge.sizeDelta.x, 80f, "метка «новее программы» измерена по настоящему тексту");
    }
}
