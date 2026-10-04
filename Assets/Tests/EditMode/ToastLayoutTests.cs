using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Update;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.UI;

// docs/ui-redesign/dialogs.md, «Что меняется» п. 2: тост — подложка NavBg, полоса уровня 3 px,
// значок, текст, действие ссылкой и ×, над строкой состояния по центру. Сторож держит раскладку:
// ничего не заходит друг на друга ни на одном языке и ни при одном уровне, потому что ширина
// тоста считается от текста, а текст — свойство перевода.
public class ToastLayoutTests
{
    private GameObject? _canvasGo;
    private ToastNotification? _toast;

    [SetUp]
    public void Setup()
    {
        _canvasGo = UiTestCanvas.Create("ToastCanvas");
        var host = new GameObject("Toast");
        host.transform.SetParent(_canvasGo.transform);
        _toast = host.AddComponent<ToastNotification>();
        _toast.Build(_canvasGo.transform);
    }

    [TearDown]
    public void Teardown()
    {
        LanguageStartup.PinSourceLanguageForTestRun();
        UiTestCanvas.Release(_canvasGo);
    }

    private ToastView View => _toast!.View!;

    private static Rect RectIn(RectTransform panel, RectTransform rt)
    {
        Vector2 min = panel.InverseTransformPoint(rt.TransformPoint(rt.rect.min));
        Vector2 max = panel.InverseTransformPoint(rt.TransformPoint(rt.rect.max));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void Show(string message, StatusLevel level, string? actionLabel = null)
    {
        System.Action? action = actionLabel != null ? () => { } : null;
        _toast!.Show(message, 2f, actionLabel, action, level);
        Canvas.ForceUpdateCanvases();
    }

    private Dictionary<string, Rect> Parts() => new()
    {
        ["полоса"] = RectIn(View.Panel, View.Stripe.rectTransform),
        ["значок"] = RectIn(View.Panel, View.Icon.rectTransform),
        ["текст"] = RectIn(View.Panel, View.Label.rectTransform),
        ["действие"] = RectIn(View.Panel, (RectTransform)View.ActionButton.transform),
        ["×"] = RectIn(View.Panel, (RectTransform)View.DismissButton.transform),
    };

    [Test]
    public void Toast_StandsAboveTheStatusBar_CentredAtTheBottom()
    {
        var panel = View.Panel;
        Assert.AreEqual(new Vector2(0.5f, 0f), panel.anchorMin, "тост — по центру внизу");
        Assert.AreEqual(UIStyle.StatusBarH + UIStyle.Space5, panel.anchoredPosition.y,
            "над строкой состояния (bottom 52), а не посреди экрана на 790 px (dialogs.md, п. 5)");
        Assert.AreEqual(0f, panel.anchoredPosition.x);
        Assert.AreEqual(UIStyle.NavBg, panel.Find(WindowSurface.FillNode).GetComponent<Image>().color,
            "подложка NavBg, а не Surface на Surface");
    }

    [Test]
    public void Toast_ActionButton_IsVisibleOnlyWhenTheToastHasAnAction()
    {
        Show("Удалено: Полка", StatusLevel.Success, "Отменить");
        Assert.IsTrue(View.ActionButton.gameObject.activeSelf);
        Assert.AreEqual("Отменить", View.ActionButton.GetComponentInChildren<TMPro.TMP_Text>().text);

        Show("Проект сохранён", StatusLevel.Info);
        Assert.IsFalse(View.ActionButton.gameObject.activeSelf,
            "без действия кнопки нет, и места под неё в тосте нет");
    }

    [Test]
    public void Toast_Parts_NeverOverlap_AndStayInsideThePanel_OnEveryLanguage_AndEveryLevel()
    {
        var offenders = new List<string>();
        foreach (var language in Loc.Languages.Select(l => l.Code).ToList())
        {
            Loc.SetLanguage(language);
            foreach (StatusLevel level in System.Enum.GetValues(typeof(StatusLevel)))
            {
                Show(Loc.F("toast.deleted", "Полка"), level, Loc.T("common.undo"));
                Check(offenders, $"{language}/{level}", withAction: true);
                Show(Loc.T("toast.photoLookReset"), level);
                Check(offenders, $"{language}/{level}/длинный", withAction: false);
            }
        }

        Assert.GreaterOrEqual(Loc.Languages.Count, 10, "обход обязан видеть все языки");
        Assert.IsEmpty(offenders, "части тоста налезают друг на друга или торчат за панель:\n"
            + string.Join("\n", offenders));
    }

    private void Check(List<string> offenders, string where, bool withAction)
    {
        var parts = Parts();
        if (!withAction) parts.Remove("действие");
        var panelRect = new Rect(View.Panel.rect);
        var names = parts.Keys.ToList();
        foreach (var name in names)
            if (parts[name].xMin < panelRect.xMin - 0.5f || parts[name].xMax > panelRect.xMax + 0.5f
                || parts[name].yMin < panelRect.yMin - 0.5f || parts[name].yMax > panelRect.yMax + 0.5f)
                offenders.Add($"{where}: «{name}» за границей панели");
        for (int i = 0; i < names.Count; i++)
        for (int j = i + 1; j < names.Count; j++)
            if (parts[names[i]].Overlaps(parts[names[j]]))
                offenders.Add($"{where}: «{names[i]}» заходит на «{names[j]}»");
        if (View.Label.isTextOverflowing) offenders.Add($"{where}: текст не помещается в свою колонку");
    }

    [Test]
    public void Toast_Width_FollowsTheText_AndStopsAtTheMaximum_WhereTheTextWraps()
    {
        Show("Ок", StatusLevel.Info);
        float shortWidth = View.Panel.sizeDelta.x;
        float shortHeight = View.Panel.sizeDelta.y;
        Show(Loc.T("toast.photoLookReset"), StatusLevel.Info);

        Assert.Less(shortWidth, ToastView.Metrics.MaxWidth - 100f, "короткий тост короткий");
        Assert.AreEqual(ToastView.Metrics.MaxWidth, View.Panel.sizeDelta.x, 0.5f);
        Assert.Greater(View.Panel.sizeDelta.y, shortHeight, "длинный текст переносится, и тост растёт вверх");
        Assert.AreEqual(ToastView.Metrics.MinHeight, shortHeight, 0.5f);
    }

    [Test]
    public void Toast_LevelIsReadWithoutColour_ShapeOfTheIconDiffers_AndTheColourHasContrast()
    {
        var seen = new HashSet<Color>();
        foreach (StatusLevel level in System.Enum.GetValues(typeof(StatusLevel)))
        {
            Show("Сообщение", level);
            Assert.AreEqual(ToastLevelStyle.ColorFor(level), View.Stripe.color, level + ": полоса");
            Assert.AreEqual(ToastLevelStyle.ColorFor(level), View.Icon.color, level + ": значок");
            Assert.IsNotNull(View.Icon.sprite, level + ": значок без картинки — уровень читался бы только цветом");
            Assert.GreaterOrEqual(WcagContrast.Ratio(ToastLevelStyle.ColorFor(level), UIStyle.NavBg),
                WcagContrast.NonTextAA, level + ": значок и полоса различимы на подложке (3:1)");
            Assert.IsTrue(seen.Add(ToastLevelStyle.ColorFor(level)), level + ": цвет уровня повторяет чужой");
        }

        Assert.AreNotSame(ToastLevelStyle.IconFor(StatusLevel.Success), ToastLevelStyle.IconFor(StatusLevel.Error));
        Assert.AreNotSame(ToastLevelStyle.IconFor(StatusLevel.Info), ToastLevelStyle.IconFor(StatusLevel.Warning));
    }

    [Test]
    public void Toast_DismissAndAction_HideTheToast_OnlyTheActionRunsTheCallback()
    {
        int ran = 0;
        _toast!.Show("Удалено: Полка", 2f, "Отменить", () => ran++, StatusLevel.Success);
        View.DismissButton.onClick.Invoke();
        Assert.IsFalse(View.Panel.gameObject.activeSelf, "× убирает тост");
        Assert.AreEqual(0, ran, "× не отменяет удаление: действие — только по ссылке");

        _toast.Show("Удалено: Полка", 2f, "Отменить", () => ran++, StatusLevel.Success);
        View.ActionButton.onClick.Invoke();
        Assert.IsFalse(View.Panel.gameObject.activeSelf);
        Assert.AreEqual(1, ran);
    }

    [Test]
    public void Toast_Dismiss_IsNotNamedLikeAWindowCross()
    {
        Assert.AreNotEqual(WindowChrome.CloseButtonName, View.DismissButton.name,
            "сторожа окон ищут × по имени CloseBtn: тост — не окно, у него нет шапки и заголовка");
    }
}
