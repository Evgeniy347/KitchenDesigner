using System.Collections;
using System.Collections.Generic;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using UnityEngine;
using UnityEngine.TestTools;

// Кадры компонентов T4a рядом с макетами: «Музыка» (mockups/tool-panels.png) целиком на
// WindowChrome + FormRows, и витрина строк в плотностях инспектора и настроек
// (mockups/inspector.png, settings.png) — её смотрят глазами при правке фабрики строк, пока окна
// T5/T6 не переехали на неё сами.
public class FormRowsDiagramTests
{
    private UiCaptureStage? _stage;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        _stage?.Dispose();
        _stage = null;
        ProjectWindows.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator MusicPanel_OnFormRows_SavesPng()
    {
        _stage = new UiCaptureStage(400, 220);
        var ui = _stage.Host.AddComponent<MusicPanelUI>();
        ui.Build(_stage.Canvas);
        ui.SetVisible(true);
        var panel = ui.WindowRect!;
        UIFactory.AnchorCenter(panel);
        panel.anchoredPosition = Vector2.zero;

        yield return _stage.Capture("music_panel.png");
    }

    [UnityTest]
    public IEnumerator RowGallery_CompactAndRegular_SavesPng()
    {
        _stage = new UiCaptureStage(1000, 560);

        var inspector = WindowChrome.Create(_stage.Canvas, "Inspector", "Деталь — Detal_800x400",
            new Vector2(UIStyle.InspectorW, 500f), new WindowChromeOptions
            {
                Kind = WindowKind.Tool, OnClose = () => { }, HasFooter = true,
            });
        inspector.Panel.anchoredPosition = new Vector2(-300f, 0f);
        var body = inspector.CreateBody();
        var c = new FormRows(body.Content, RowDensity.Compact);
        c.Dropdown("Type", "Тип", new List<string> { "Деталь" }, 0, _ => { });
        c.Text("Name", "Название").field.text = "Detal_800x400";
        c.Section("Size", "Размеры");
        c.Number("W", "Ширина", "мм").field.text = "800";
        c.Number("D", "Глубина", "мм", hint: "settings.project.grid").field.text = "-18";
        c.ReadOnly("H", "Высота", "400 мм");
        c.Section("Grooves", "Пазы", actionCaption: "+ Добавить", onAction: () => { }).SetCount("0");
        c.Section("Pos", "Положение");
        c.Vector("Pos", "Позиция, мм");
        c.Vector("Rot", "Поворот, °", new[] { "X", "Y", "Z" }, _ => { });
        c.Section("Props", "Свойства");
        c.Switch("Transparent", "Прозрачный", false, _ => { });
        c.Switch("Lock", "Закрепить", true, _ => { });
        c.Relayout();
        body.Fit();
        inspector.Footer!.AddLeft("Dup", "Дублировать", () => { });
        inspector.Footer.AddPrimary("Del", "Удалить", () => { });

        var settings = WindowChrome.Create(_stage.Canvas, "Settings", "Настройки", new Vector2(600f, 360f),
            new WindowChromeOptions { OnClose = () => { } });
        settings.Panel.anchoredPosition = new Vector2(190f, 60f);
        var sbody = settings.CreateBody();
        var r = new FormRows(sbody.Content, RowDensity.Regular);
        r.Section("Grid", "Сетка и привязка");
        r.Switch("Grid", "Сетка", true, _ => { }, hint: "settings.project.grid");
        r.Number("Step", "Шаг сетки", "мм", indent: 1).field.text = "18";
        r.Slider("Sens", "Чувствительность мыши", 0.5f, 2f, 1f, v => NumberFormat.Fixed(v, 1) + "×", _ => { });
        r.Segmented("Quality", "Качество", new[] { "Низкое", "Среднее", "Высокое" }, 2, _ => { });
        r.Dropdown("Lang", "Язык", new List<string> { "Русский" }, 0, _ => { });
        r.Relayout();
        sbody.Fit();

        yield return _stage.Capture("form_rows_gallery.png");
    }
}
