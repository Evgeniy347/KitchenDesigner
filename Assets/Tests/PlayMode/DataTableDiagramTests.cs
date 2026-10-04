using System.Collections;
using System.Collections.Generic;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using UnityEngine;
using UnityEngine.TestTools;

// Кадр DataTable в окне WindowChrome на данных макета mockups/specification.png: шапка, раздел-группа,
// числа вправо, подытог, многоточие, выбранная строка. Окна T7 переезжают на этот компонент; PNG
// смотрят глазами при каждой правке таблицы.
public class DataTableDiagramTests
{
    private UiCaptureStage? _stage;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _stage?.Dispose();
        _stage = null;
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpecificationLikeTable_SavesPng()
    {
        _stage = new UiCaptureStage(760, 420);
        var chrome = WindowChrome.Create(_stage.Canvas, "Spec", "Спецификация", new Vector2(720f, 380f),
            new WindowChromeOptions { OnClose = () => { }, HasFooter = true, RuledHeader = true });
        var table = DataTable.Create(chrome.Panel, "SpecTable",
            new Vector2(chrome.BodyWidth, 380f - chrome.BodyTop - chrome.BodyBottom), new[]
            {
                new DataColumn("n", "№", 36f, CellAlign.Right),
                new DataColumn("name", "Наименование", 0f, CellAlign.Left, sortable: true),
                new DataColumn("mat", "Материал", 140f),
                new DataColumn("w", "Ш, мм", 64f, CellAlign.Right, sortable: true),
                new DataColumn("h", "В, мм", 64f, CellAlign.Right),
                new DataColumn("q", "Кол-во", 72f, CellAlign.Right),
                new DataColumn("u", "Ед.", 40f),
            }, selectable: true);
        var rt = table.Root;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(chrome.BodyPad, -chrome.BodyTop);

        var rows = new List<DataRow>
        {
            DataRow.Group("Мебель"),
            DataRow.Item("1", "Комплект GTV AXIS PRO с длинным названием", "—", "400", "115", "1", "шт"),
            DataRow.Item("2", "Дно", "Антрацит (GTV)", "325", "16", NumberFormat.Fixed(0.11, 2), "м²"),
            DataRow.Item("3", "Задник", "Антрацит (GTV)", "313", "84", NumberFormat.Fixed(0.03, 2), "м²"),
            new DataRow(DataRowKind.Subtotal, "", "Итого, Антрацит (GTV)", "", "", "", NumberFormat.Fixed(0.14, 2), "м²"),
            DataRow.Group("Сантехника"),
            DataRow.Item("4", "Смеситель для кухни", "—", "50", "320", "1", "шт"),
            new DataRow(DataRowKind.Total, "", "Всего позиций: 4"),
        };
        table.SetRows(rows);
        table.Select(rows[2]);
        chrome.Footer!.AddLeftText("Count", "4 позиции · 2 раздела");
        chrome.Footer.AddPrimary("Export", "Экспорт CSV…", () => { });

        yield return _stage.Capture("data_table_specification.png");
    }
}
