using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;
using KitchenDesigner.Tests;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Левая навигация «Настроек» (D6) заменила ленту вкладок, и у неё те же три риска, что были у ленты:
/// страница, до которой из навигации не добраться; пункт не той высоты или не того кегля; подпись, не
/// влезающая в свой пункт на каком-то из десяти языков. Прежний сторож ленты
/// (<c>SettingsTabStripGuardTests</c>) стерёг «подпись вкладки — всегда <see cref="UIStyle.FontTab"/>»;
/// у навигации то же правило звучит как «пункт — <see cref="UIStyle.FontBody"/>, и не влезает — шире
/// навигация, не мельче шрифт»: <see cref="SettingsNav.ItemInnerWidth"/> = NavW − 2 × Space3.
/// </summary>
public class SettingsNavGuardTests
{
    private static readonly string[] Languages =
        { "ru", "en", "de", "es", "fr", "it", "pt", "ja", "zh-Hans", "ar-TN" };

    private GameObject? _canvasGo;

    [TearDown]
    public void TearDown()
    {
        Loc.SetLanguage("ru");
        EditModeManager.Reset();
        UiTestCanvas.Release(_canvasGo);
    }

    private SettingsPanelUI BuildPanel(string language = "ru")
    {
        Loc.SetLanguage(language);
        _canvasGo = UiTestCanvas.Create("NavGuardCanvas");
        var ui = _canvasGo.AddComponent<SettingsPanelUI>();
        ui.Build(_canvasGo.transform);
        ui.SetVisible(true);
        return ui;
    }

    private List<Button> Items() =>
        _canvasGo!.transform.Find(SettingsWindowPaths.Nav).Cast<Transform>()
            .Where(t => t.name.StartsWith(SettingsNav.ItemPrefix, System.StringComparison.Ordinal))
            .Select(t => t.GetComponent<Button>()).ToList();

    private static List<string> Offenders(IEnumerable<Button> items)
    {
        var offenders = new List<string>();
        foreach (var item in items)
        {
            var caption = item.GetComponentInChildren<TMP_Text>();
            string text = caption.text.Replace("​", "");
            if (!Mathf.Approximately(caption.fontSize, UIStyle.FontBody))
                offenders.Add($"«{text}»: кегль {caption.fontSize:F1}, стандарт {UIStyle.FontBody}");

            float height = ((RectTransform)item.transform).rect.height;
            if (!Mathf.Approximately(height, UIStyle.NavItemH))
                offenders.Add($"«{text}»: высота пункта {height:F1}, по D6 — {UIStyle.NavItemH}");

            float needed = caption.GetPreferredValues(caption.text).x;
            if (needed > SettingsNav.ItemInnerWidth + 0.01f)
                offenders.Add($"«{text}»: нужно {needed:F0} px, в пункте {SettingsNav.ItemInnerWidth:F0} px");
        }
        return offenders;
    }

    /// <summary>Пункты и подписи групп — один тест на язык: сборка окна на язык здесь самое дорогое,
    /// и две проверки одной и той же навигации платили её дважды.</summary>
    [TestCaseSource(nameof(Languages))]
    public void EveryNavCaption_AndGroupCaption_IsItsStandardSize_AndFitsTheNav(string language)
    {
        BuildPanel(language);
        var items = Items();
        Assume.That(items.Count, Is.EqualTo(SettingsPanelUI.PageOrder.Length), "в навигации девять разделов");

        var offenders = Offenders(items);

        Assert.IsEmpty(offenders,
            $"язык {language}: подпись пункта не мельчится и не вылезает за пункт — расширяйте навигацию, а не "
            + "уменьшайте шрифт:\n" + string.Join("\n", offenders));

        var groups = _canvasGo!.transform.Find(SettingsWindowPaths.Nav).Cast<Transform>()
            .Where(t => t.name.StartsWith(SettingsNav.GroupPrefix, System.StringComparison.Ordinal))
            .Select(t => t.GetComponent<TMP_Text>()).ToList();
        Assert.AreEqual(3, groups.Count, "три группы: Картинка, Дом, Прочее");

        foreach (var group in groups)
        {
            float needed = group.GetPreferredValues(group.text).x;
            Assert.LessOrEqual(needed, SettingsNav.ItemInnerWidth + 0.01f,
                $"{language}: подпись группы «{group.text}» шире навигации");
            Assert.AreEqual(UIStyle.FontCaption, group.fontSize, $"{language}: кегль подписи группы");
        }
    }

    [Test]
    public void EveryPage_IsReachableFromTheNav_AndTheNavOpensExactlyThatPage()
    {
        var ui = BuildPanel();
        var items = Items();
        var ids = SettingsPanelUI.PageOrder;

        for (int i = 0; i < ids.Length; i++)
        {
            Assert.AreEqual("NavItem_" + ids[i], items[i].name);
            items[i].onClick.Invoke();

            Assert.AreEqual(i, ui.CurrentTab, $"клик по пункту «{ids[i]}» открывает страницу с тем же номером");
            var visible = _canvasGo!.transform.Find(SettingsWindowPaths.Body.TrimEnd('/')).Cast<Transform>()
                .Where(c => c.name.StartsWith("Page_") && c.gameObject.activeSelf).Select(c => c.name).ToList();
            CollectionAssert.AreEqual(new[] { "Page_" + ids[i] }, visible,
                "из навигации открывается ровно одна страница — та, что названа в пункте");
        }
    }

    [Test]
    public void TheNav_StaysBelowTheHeaderAndAboveTheFooter_AndHoldsEveryItem()
    {
        BuildPanel();
        var panel = (RectTransform)_canvasGo!.transform.Find(SettingsWindowPaths.Panel);
        var nav = (RectTransform)panel.Find("SettingsNav");
        var corners = new Vector3[4];

        nav.GetWorldCorners(corners);
        float top = panel.InverseTransformPoint(corners[1]).y;
        float bottom = panel.InverseTransformPoint(corners[0]).y;
        Assert.AreEqual(panel.rect.yMax - UIStyle.TitleBarH, top, 0.01f, "навигация начинается под шапкой");
        Assert.AreEqual(panel.rect.yMin + UIStyle.FooterH, bottom, 0.01f, "и кончается над футером");

        foreach (var item in Items())
        {
            ((RectTransform)item.transform).GetWorldCorners(corners);
            float itemBottom = panel.InverseTransformPoint(corners[0]).y;
            Assert.GreaterOrEqual(itemBottom, bottom - 0.01f, $"{item.name} ушёл ниже навигации");
        }
    }

    [Test]
    public void TheDetector_CatchesAnOverlongCaption_SoAGreenGuardMeansSomething()
    {
        BuildPanel();
        var item = Items()[0];
        item.GetComponentInChildren<TMP_Text>().text = new string('Ш', 40);

        Assert.IsNotEmpty(Offenders(new[] { item }),
            "сторож, который не краснеет на заведомо длинной подписи, зеленеет впустую");
    }

    [Test]
    public void TheWindow_IsTheD6Size()
    {
        BuildPanel();
        var panel = (RectTransform)_canvasGo!.transform.Find(SettingsWindowPaths.Panel);

        Assert.AreEqual(new Vector2(920f, 640f), panel.sizeDelta,
            "D6: окно настроек 920×640 — на 1366×768 с полом масштаба 0,8125 оно помещается между тулбаром и строкой состояния");
        Assert.AreEqual(200f, UIStyle.NavW, "навигация — 200 (D6)");
    }
}
