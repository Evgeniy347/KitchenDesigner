using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// T4a: одна фабрика строк для инспектора, настроек и панелей-инструментов (D6, D7, D8, §9, §13).
// Две фабрики со своими магическими числами (ContextMenuRowFactory: 103, 126, 65, 202, 27…;
// SettingsRowFactory: 300, 150, 20, 6) дали подписи с трёх разных x и значения с двух — глаз
// скачет. Здесь каждое число — токен, и одна колонка на всё окно.
public class FormRowsTests
{
    private GameObject? _canvas;
    private RectTransform? _host;

    [SetUp]
    public void SetUp()
    {
        _canvas = UiTestCanvas.Create("FormRowsCanvas");
        _host = UIFactory.CreateRect("Host", _canvas.transform);
        _host.sizeDelta = new Vector2(600f, 800f);
        CollapsibleSection.ForgetSessionState();
    }

    [TearDown]
    public void TearDown()
    {
        CollapsibleSection.ForgetSessionState();
        UiTestCanvas.Release(_canvas);
    }

    private static float LeftIn(RectTransform host, RectTransform rt) =>
        host.InverseTransformPoint(rt.TransformPoint(rt.rect.min)).x - host.rect.xMin;

    private static float TopIn(RectTransform host, RectTransform rt) =>
        host.rect.yMax - host.InverseTransformPoint(rt.TransformPoint(rt.rect.max)).y;

    private FormRows FullForm(RowDensity density)
    {
        var rows = new FormRows(_host!, density);
        rows.Number("W", "Ширина", "мм");
        rows.Dropdown("Type", "Тип", new List<string> { "Деталь", "Полка" }, 0, _ => { });
        rows.Switch("Lock", "Закрепить", false, _ => { });
        rows.Segmented("Quality", "Качество", new[] { "Низкое", "Среднее", "Высокое" }, 1, _ => { });
        rows.Slider("Speed", "Скорость", 0.5f, 2f, 1f, v => NumberFormat.Fixed(v, 1) + "×", _ => { });
        rows.ReadOnly("Depth", "Глубина", "350 мм");
        rows.Text("Name", "Название");
        rows.Relayout();
        Canvas.ForceUpdateCanvases();
        return rows;
    }

    [TestCase(RowDensity.Regular)]
    [TestCase(RowDensity.Compact)]
    [TestCase(RowDensity.Tool)]
    public void EveryLabel_StartsAtOneX_EveryValue_AtAnother(RowDensity density)
    {
        var rows = FullForm(density);
        var m = rows.Metrics;
        var labelX = rows.Rows.Where(r => r.Label != null).Select(r => LeftIn(_host!, r.Label!.rectTransform)).Distinct().ToList();
        var valueX = rows.Rows.Where(r => r.Control != null)
            .Select(r => LeftIn(_host!, (RectTransform)r.Control!.transform))
            .Select(x => Mathf.Round(x)).Distinct().ToList();

        CollectionAssert.AreEqual(new[] { 0f }, labelX,
            "подписи с одного x (D7): сегодня в инспекторе 10, 16 и 32 px в одной панели");
        var segmentX = m.ValueX + UIStyle.SegmentPad;
        Assert.IsTrue(valueX.All(x => Mathf.Approximately(x, m.ValueX) || Mathf.Approximately(x, Mathf.Round(segmentX))),
            $"значения с одного x = {m.ValueX} (подпись {m.LabelW} + зазор {m.Gap}); нашлось: {string.Join(", ", valueX)}");
    }

    [Test]
    public void Metrics_ComeFromTheTokens()
    {
        var regular = RowMetrics.For(RowDensity.Regular);
        Assert.AreEqual(UIStyle.SettingsLabelW, regular.LabelW);
        Assert.AreEqual(UIStyle.SettingsColumnGap, regular.Gap);
        Assert.AreEqual(UIStyle.SettingsControlW, regular.ValueW);
        Assert.AreEqual(UIStyle.ControlH, regular.ControlH);
        Assert.AreEqual(UIStyle.NumberFieldW, regular.NumberW, "в настройках число — 120, список — 240 (D6)");

        var compact = RowMetrics.For(RowDensity.Compact);
        Assert.AreEqual(UIStyle.InspectorLabelW, compact.LabelW);
        Assert.AreEqual(UIStyle.InspectorColumnGap, compact.Gap);
        Assert.AreEqual(UIStyle.InspectorValueW, compact.ValueW);
        Assert.AreEqual(UIStyle.ControlHCompact, compact.ControlH);
        Assert.AreEqual(UIStyle.RowStep, compact.RowStep, "инспектор: контрол 28, шаг 32 (D7)");
        Assert.AreEqual(UIStyle.InspectorW - 2f * UIStyle.ToolPanelPad, compact.Width,
            "колонки 136 | 12 | 188 ровно заполняют инспектор 360 с паддингом 12");

        var tool = RowMetrics.For(RowDensity.Tool);
        Assert.AreEqual(UIStyle.ToolLabelW, tool.LabelW);
        Assert.AreEqual(UIStyle.ToolPanelW - 2f * UIStyle.ToolPanelPad, tool.Width);
    }

    [Test]
    public void Rows_StackAtTheRowStep_FromTheTop()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        var (a, _) = rows.Number("A", "А", "мм");
        var (b, _) = rows.Number("B", "Б", "мм");
        rows.Relayout();
        Canvas.ForceUpdateCanvases();
        Assert.AreEqual(UIStyle.RowStep, TopIn(_host!, b.rectTransform) - TopIn(_host!, a.rectTransform), 0.5f);
        Assert.AreEqual(UIStyle.ControlHCompact * 2f + (UIStyle.RowStep - UIStyle.ControlHCompact), rows.Height, 0.5f,
            "высота формы — от первой строки до низа последней, без хвостового зазора");
    }

    [Test]
    public void DisabledControl_DimsItsLabelToo_ThroughTheRegistry()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        var (label, field) = rows.Number("H", "Высота", "мм");
        rows.Relayout();

        UIRowEnabled.SetControlEnabled(field, false);
        rows.SyncEnabledState();
        Assert.AreEqual(UIStyle.TextDisabled, label.color,
            "§9: нередактируемая строка гаснет целиком — подпись выводится из контрола, а не красится по месту");
        UIRowEnabled.SetControlEnabled(field, true);
        rows.SyncEnabledState();
        Assert.AreEqual(UIStyle.Text, label.color, "парный контроль: рабочая строка остаётся яркой");
    }

    [Test]
    public void Hint_IsAChildOfTheLabel_AndTakesItsLaneFromTheLabel()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        var (label, field) = rows.Number("H", "Высота", "мм", hint: "settings.project.grid");
        rows.Relayout();
        Canvas.ForceUpdateCanvases();

        var badge = label.GetComponentInChildren<HintBadge>(true);
        Assert.IsNotNull(badge, "«i» — ребёнок подписи (§13): переезжает и гаснет вместе с ней");
        float badgeRight = LeftIn(_host!, (RectTransform)badge!.transform) + UIStyle.HintBadgeSize;
        Assert.LessOrEqual(badgeRight, LeftIn(_host!, (RectTransform)field.transform) + 0.5f,
            "дорожка «i» отнимается у подписи, а не у значения: значок не налезает на поле");
    }

    [Test]
    public void ReadOnlyRow_IsTextWithoutAFrame_UnderADimmedLabel()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        var (label, value) = rows.ReadOnly("Oven", "Ширина", "600 мм");
        Assert.AreEqual(UIStyle.TextDisabled, label.color, "вычисляемое — подпись погашена (§9, D7)");
        Assert.AreEqual(UIStyle.TextSecondary, value.color);
        Assert.IsNull(value.GetComponentInParent<TMP_InputField>(), "только чтение — текст, а не поле, в которое тянет печатать");
    }

    [Test]
    public void Section_CollapsesItsRows_AndRemembersItForTheSession()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        var section = rows.Section("Pos", "Положение", memoryKey: "Part/Pos");
        var (label, _) = rows.Number("X", "X", "мм");
        rows.EndSection();
        var (after, _) = rows.Number("After", "После", "мм");
        rows.Relayout();
        float expandedTop = TopIn(_host!, after.rectTransform);

        section.Toggle();
        Canvas.ForceUpdateCanvases();
        Assert.IsFalse(label.gameObject.activeInHierarchy, "свёрнутая секция прячет свои строки");
        Assert.AreEqual(UIStyle.GlyphCollapsed, section.transform.Find(CollapsibleSection.ChevronNode).GetComponent<TMP_Text>().text);
        Assert.Less(TopIn(_host!, after.rectTransform), expandedTop, "строки под секцией поднимаются на её место");

        var again = new FormRows(_host!, RowDensity.Compact).Section("Pos2", "Положение", memoryKey: "Part/Pos");
        Assert.IsFalse(again.Expanded, "свёрнутость помнится по ключу (типу элемента) на сессию (D7)");
    }

    [Test]
    public void SectionHeader_IsBoldSecondaryWithCountAndActionLink()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        int added = 0;
        var section = rows.Section("Grooves", "Пазы", actionCaption: "+ Добавить", onAction: () => added++);
        section.SetCount("0");
        Assert.AreEqual(FontStyles.Bold, section.Title!.fontStyle);
        Assert.AreEqual(UIStyle.FontSection, (int)section.Title.fontSize);
        Assert.AreEqual(UIStyle.TextSecondary, section.Title.color);
        Assert.AreEqual(UIStyle.TextDisabled, section.Count!.color);
        Assert.AreEqual(UIStyle.AccentText, section.ActionButton!.GetComponentInChildren<TMP_Text>().color,
            "«+ Добавить» — ссылка справа, а не кнопка-полоса во всю ширину (D7)");
        section.ActionButton.onClick.Invoke();
        Assert.AreEqual(1, added);
    }

    [Test]
    public void Switch_IsFortyByTwenty_AndPaintsItsState()
    {
        var rows = new FormRows(_host!, RowDensity.Regular);
        bool last = false;
        var (_, toggle) = rows.Switch("Grid", "Сетка", false, v => last = v);
        var rt = (RectTransform)toggle.transform;
        Assert.AreEqual(new Vector2(UIStyle.SwitchW, UIStyle.SwitchH), rt.sizeDelta);
        var track = rt.Find(SwitchControl.TrackNode).GetComponent<Image>();
        Assert.AreEqual(UIStyle.Field, track.color, "выключен — поле с рамкой, видимое на тёмном (D6)");

        toggle.isOn = true;
        Assert.IsTrue(last);
        Assert.AreEqual(UIStyle.Accent, track.color, "включён — Accent");
        Assert.Greater(rt.Find(SwitchControl.TrackNode + "/" + SwitchControl.KnobNode).GetComponent<RectTransform>().anchoredPosition.x, 0f,
            "бегунок уехал вправо");
    }

    [Test]
    public void Segmented_IsOneControl_SelectedSegmentRaised()
    {
        var rows = new FormRows(_host!, RowDensity.Regular);
        int chosen = -1;
        var (_, seg) = rows.Segmented("Q", "Качество", new[] { "Низкое", "Среднее", "Высокое" }, 0, i => chosen = i);
        seg.Segments[2].onClick.Invoke();
        Assert.AreEqual(2, seg.Value);
        Assert.AreEqual(2, chosen);
        Assert.AreEqual(UIStyle.SurfaceActive, ((Image)seg.Segments[2].targetGraphic).color);
        Assert.AreEqual(UIStyle.Transparent, ((Image)seg.Segments[0].targetGraphic).color);
        Assert.AreEqual(rows.Metrics.ValueW, ((RectTransform)seg.transform).sizeDelta.x, "сегмент занимает колонку значения");
    }

    [Test]
    public void Vector_ThreeFieldsAcrossTheRow_AxisPrefixInside_RotateAtTheEnd()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        int rotated = -1;
        var vector = rows.Vector("Rot", "Поворот, °", new[] { "X", "Y", "Z" }, axis => rotated = axis);
        rows.Relayout();
        Canvas.ForceUpdateCanvases();

        Assert.AreEqual(3, vector.Fields.Count);
        float w = ((RectTransform)vector.Fields[0].transform).sizeDelta.x;
        Assert.AreEqual((rows.Metrics.Width - 2f * UIStyle.Space1) / 3f, w, 0.01f,
            "три поля на всю ширину через 4 (D7): полоса «X 90° | Y 90° | Z 90°» уходит");
        for (int i = 0; i < 3; i++)
            Assert.AreEqual(VectorField.AxisNames[i],
                vector.Fields[i].transform.Find(VectorField.PrefixNode).GetComponent<TMP_Text>().text,
                "префикс оси внутри поля (Figma)");
        vector.RotateButtons[1].onClick.Invoke();
        Assert.AreEqual(1, rotated, "↻90 у поля своей оси");
        Assert.AreEqual(0f, LeftIn(_host!, vector.Root), 0.5f);
    }

    [Test]
    public void Slider_ShowsItsValueThroughTheFormatter_InTheValueColumn()
    {
        var rows = new FormRows(_host!, RowDensity.Regular);
        var (_, slider, readout) = rows.Slider("S", "Чувствительность", 0.5f, 2f, 1f,
            v => NumberFormat.Fixed(v, 1, ",") + "×", _ => { });
        Assert.AreEqual("1,0×", readout.text);
        slider.value = 1.5f;
        Assert.AreEqual("1,5×", readout.text);
        Assert.AreEqual(UIStyle.SliderValueW, readout.rectTransform.sizeDelta.x, "значение — 64 справа (D6)");
        Assert.AreEqual(UIStyle.SliderThumb, ((RectTransform)slider.handleRect).sizeDelta.x, "бегунок 16, круглый");
    }

    [Test]
    public void Note_WrapsAcrossTheWholeRow()
    {
        var rows = new FormRows(_host!, RowDensity.Compact);
        var note = rows.Note("Hint", "Клик по стороне: авто → есть → убрать, и ещё длинный текст, который обязан перенестись");
        Assert.AreEqual(rows.Metrics.Width, note.rectTransform.sizeDelta.x);
        Assert.Greater(note.rectTransform.sizeDelta.y, UIStyle.FontSmall * 1.5f, "длинное пояснение переносится, высота по тексту");
    }
}
