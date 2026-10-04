using System.Collections.Generic;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using TMPro;
using UnityEngine;

// T12 / D12: в арабском интерфейсе зеркалится РАСКЛАДКА общих компонентов, а не каждое окно своим
// «if (rtl)». До T12 шапка WindowChrome оставалась левосторонней: заголовок у левого края, × справа —
// подписи формы уже стояли справа, и глаз шёл от заголовка через всё окно к своим строкам. Таблица
// держала полосу выделения слева и числа справа, то есть в начале арабской строки, а не в её конце.
// Каждый тест меряет геометрию в координатах окна: «справа от» и «у правого края», а не флаги.
// Числа и единицы не зеркалятся — это сторожит ScriptRenderingScreenshotTests на отрисованном тексте.
public class RightToLeftMirrorTests
{
    private GameObject? _canvas;

    [SetUp]
    public void SetUp()
    {
        Loc.SetLanguage("ar-TN");
        Assert.IsTrue(LayoutDirection.IsRtl, "ar-TN объявлен @rtl — без него проверять нечего");
        _canvas = UiTestCanvas.Create("RtlCanvas");
    }

    [TearDown]
    public void TearDown()
    {
        Loc.SetLanguage("ru");
        EditModeManager.Reset();
        UiTestCanvas.Release(_canvas);
    }

    private static float LeftIn(RectTransform host, RectTransform rt) =>
        host.InverseTransformPoint(rt.TransformPoint(rt.rect.min)).x - host.rect.xMin;

    private static float RightIn(RectTransform host, RectTransform rt) =>
        host.InverseTransformPoint(rt.TransformPoint(rt.rect.max)).x - host.rect.xMin;

    private WindowChrome Chrome(bool footer = false) =>
        WindowChrome.Create(_canvas!.transform, "Probe", "نافذة", new Vector2(640f, 480f),
            new WindowChromeOptions { OnClose = () => { }, HasFooter = footer });

    [Test]
    public void WindowChrome_PutsTheCloseTopLeft_AndTheTitleAgainstTheRightEdge()
    {
        var chrome = Chrome();
        var panel = chrome.Panel;
        var close = (RectTransform)chrome.CloseButton!.transform;
        var title = chrome.Title.rectTransform;

        Assert.AreEqual(UIStyle.CloseBtnInset, LeftIn(panel, close), 0.5f,
            "× в левом верхнем углу (D12): справа он стоял поверх начала арабского заголовка");
        Assert.AreEqual(panel.rect.width - UIStyle.WindowTitleInset, RightIn(panel, title), 0.5f,
            "заголовок прижат к правому краю на том же отступе 16, что и слева в русском");
        Assert.Greater(LeftIn(panel, title), RightIn(panel, close),
            "заголовок не заходит под ×: его ширина урезана на ту же кнопку, только с другой стороны");
    }

    [Test]
    public void WindowChrome_HeaderActions_StandBesideTheClose_OnTheLeft()
    {
        var chrome = Chrome();
        var panel = chrome.Panel;
        var first = (RectTransform)chrome.AddHeaderAction("Add", IconFactory.Pin, "x", () => { }).transform;
        var second = (RectTransform)chrome.AddHeaderAction("More", IconFactory.Gear, "y", () => { }).transform;
        var close = (RectTransform)chrome.CloseButton!.transform;
        var title = chrome.Title.rectTransform;

        Assert.Greater(LeftIn(panel, first), RightIn(panel, close) - 0.5f, "первое действие — сразу за ×, к центру окна");
        Assert.Greater(LeftIn(panel, second), RightIn(panel, first) - 0.5f, "второе — за первым");
        Assert.GreaterOrEqual(LeftIn(panel, title), RightIn(panel, second) - 0.5f,
            "заголовок не наезжает на иконные действия шапки");
    }

    [Test]
    public void WindowFooter_PutsThePrimaryLeftmost_AndTheSecondaryActionsOnTheRight()
    {
        var footer = Chrome(footer: true).Footer!;
        var cancel = (RectTransform)footer.AddSecondary("Cancel", "إلغاء", () => { }).transform;
        var save = (RectTransform)footer.AddPrimary("Save", "حفظ", () => { }).transform;
        var reset = (RectTransform)footer.AddLeft("Reset", "إعادة", () => { }, ButtonRole.Link).transform;
        var root = footer.Root;

        Assert.AreEqual(UIStyle.Space4, LeftIn(root, save), 0.5f, "основная — крайняя слева (D12), зеркало «Отмена | Сохранить»");
        Assert.Greater(LeftIn(root, cancel), RightIn(root, save), "«Отмена» правее основной");
        Assert.AreEqual(root.rect.width - UIStyle.Space4, RightIn(root, reset), 0.5f, "вторичное действие — у правого края");
    }

    [Test]
    public void FormRows_PutTheLabelColumnOnTheRight_AndTheValueColumnOnTheLeft()
    {
        var host = UIFactory.CreateRect("Host", _canvas!.transform);
        host.sizeDelta = new Vector2(600f, 400f);
        var rows = new FormRows(host, RowDensity.Regular);
        rows.Number("W", "العرض", "мм");
        rows.Relayout();
        var row = (RectTransform)host.Find("Row_W");
        var label = (RectTransform)row.Find("L_W");
        var field = row.GetComponentInChildren<TMP_InputField>();

        Assert.Greater(LeftIn(row, label), RightIn(row, (RectTransform)field.transform) - 0.5f,
            "подпись справа от поля (D12) — колонка подписей зеркалится целиком");
        Assert.AreEqual(row.rect.width, RightIn(row, label), 1f, "подпись прижата к правому краю строки");
    }

    private static readonly DataColumn[] Columns =
    {
        new DataColumn("name", "الاسم", 0f, CellAlign.Left),
        new DataColumn("w", "мм", 70f, CellAlign.Right),
    };

    [Test]
    public void DataTable_StartsAtTheRight_MirrorsTheCellAlignment_AndTheSelectionBar()
    {
        var table = DataTable.Create(_canvas!.transform, "Table", new Vector2(400f, 300f), Columns, selectable: true);
        table.SetRows(new List<DataRow> { DataRow.Item("رف", "1325") });
        var name = table.CellLabel(0, 0)!;
        var width = table.CellLabel(0, 1)!;
        var row = (RectTransform)name.transform.parent;

        Assert.Greater(LeftIn(row, name.rectTransform), RightIn(row, width.rectTransform) - 0.5f,
            "первая колонка — справа: арабская строка таблицы начинается у правого края");
        Assert.AreEqual(TextAlignmentOptions.Right, name.alignment, "текст прижат к началу своей колонки — вправо");
        Assert.AreEqual(TextAlignmentOptions.Left, width.alignment,
            "числа прижаты к КОНЦУ колонки, как в русском, только конец теперь слева; цифры при этом не переставлены");
        Assert.AreEqual(TextAlignmentOptions.Left, table.HeaderLabel(1).alignment, "шапка выровнена как её колонка");
        Assert.AreEqual("1325", width.text, "число остаётся числом — сама строка не зеркалится");

        var bar = (RectTransform)row.Find("SelectionBar");
        Assert.AreEqual(row.rect.width, RightIn(row, bar), 0.5f, "полоса выделения — у правого края, в начале строки");
    }

    [Test]
    public void SettingsWindow_PutsTheNavigationOnTheRight_OfThePages()
    {
        var ui = _canvas!.AddComponent<SettingsPanelUI>();
        ui.Build(_canvas.transform);
        ui.SetVisible(true);
        var panel = (RectTransform)_canvas.transform.Find(SettingsWindowPaths.Panel);
        var nav = (RectTransform)_canvas.transform.Find(SettingsWindowPaths.Nav);
        var viewport = (RectTransform)panel.Find("SettingsPanelBody");

        Assert.Greater(LeftIn(panel, nav), RightIn(panel, viewport) - 0.5f, "навигация настроек справа от страниц (D12)");

        var item = (RectTransform)nav.Find("NavItem_" + SettingsPanelUI.PageOrder[0]);
        var bar = (RectTransform)item.Find(SettingsNav.BarNode);
        Assert.AreEqual(item.rect.width, RightIn(item, bar), 0.5f,
            "полоса текущего раздела — у правого края пункта, со стороны начала строки; слева она висела у страниц");
    }

    [Test]
    public void SectionHeader_PutsTheChevronAndTitleOnTheRight_TheActionOnTheLeft_AndPointsCollapsedLeft()
    {
        var section = CollapsibleSection.Create("Sec", _canvas!.transform, "الأخاديد", 300f, null, "+ إضافة", () => { });
        var root = (RectTransform)section.transform;
        var chevron = (RectTransform)root.Find(CollapsibleSection.ChevronNode);
        var title = section.Title!.rectTransform;
        var action = (RectTransform)section.ActionButton!.transform;

        Assert.AreEqual(root.rect.width, RightIn(root, chevron), 0.5f, "шеврон — у правого края, в начале строки");
        Assert.Less(RightIn(root, title), LeftIn(root, chevron) + 0.5f, "заголовок — сразу левее шеврона");
        Assert.AreEqual(0f, LeftIn(root, action), 0.5f, "действие секции («+ Добавить») — у левого края");

        section.SetExpanded(false, notify: false);
        Assert.AreEqual(UIStyle.GlyphCollapsedRtl, root.Find(CollapsibleSection.ChevronNode).GetComponent<TMP_Text>().text,
            "свёрнутая секция указывает ◄ — туда, куда читается строка (D12: направленные глифы зеркалятся)");
        Assert.IsTrue(UIFactory.FontAsset!.HasCharacter(UIStyle.GlyphCollapsedRtl[0], false, true), "◄ входит в WGL4 и в LiberationSans");
    }

    [Test]
    public void Dropdown_PutsTheArrowAtTheLeft_AndKeepsTheCaptionClearOfIt()
    {
        var dropdown = UIFactory.CreateDropdown("Dd", _canvas!.transform, new List<string> { "العربية (تونس)" },
            Vector2.zero, new Vector2(200f, 28f), _ => { });
        var root = (RectTransform)dropdown.transform;
        var arrow = (RectTransform)root.Find("Dd_Arrow");
        var caption = dropdown.captionText.rectTransform;

        Assert.Less(LeftIn(root, arrow), UIStyle.Space2, "▼ у левого края закрытого списка (D12)");
        Assert.AreEqual(root.rect.width - RightIn(root, caption), 8f, 0.5f, "у правого края — тот же отступ 8, что слева в русском");
        Assert.AreEqual(18f, LeftIn(root, caption), 0.5f,
            "слева подпись оставляет ▼ те же 18, что справа в русском: на снимке «▼العربية» стояли вплотную");
    }
}
