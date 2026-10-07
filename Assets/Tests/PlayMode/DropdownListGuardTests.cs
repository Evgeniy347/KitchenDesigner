using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Сторож раскрытых списков. Пользователь открыл «Язык»: десять пунктов, видно семь, а полосы прокрутки
/// нет — нижние пункты не отличить от «списка больше нет». Правило одно на ВСЕ списки приложения
/// (docs/UI-GUIDELINES.md §5, «Раскрытый список»): окно списка ограничено UIStyle.DropdownVisibleItems
/// пунктами, пунктов больше — есть полоса прокрутки, выбранный пункт при открытии виден, список целиком
/// внутри экрана (не помещается вниз — открывается вверх).
/// Тест обходит не один список со скриншота, а три вида: свежесобранные фабрикой у верхнего/нижнего краёв
/// экрана, ВСЕ списки живого приложения (окно настроек, меню свойств детали) и фильтр-список
/// MultiSelectDropdown. Новый список, собранный в обход фабрики, краснеет на проверке типа.
/// Второй сторож — язык: названия языков в списке рисуются родным шрифтом при любом языке интерфейса.
/// </summary>
public class DropdownListGuardTests
{
    private const float Tolerance = 0.75f;

    private static GameObject? _bootstrap;
    private static GameObject? _camera;
    private static TMP_Dropdown? _atTop;
    private static TMP_Dropdown? _atBottom;
    private static TMP_Dropdown? _inCorner;
    private static TMP_Dropdown? _short;

    [OneTimeTearDown]
    public void OneTimeTearDownOnce()
    {
        if (Loc.Language != Localizer.SourceLanguage) Loc.SetLanguage(Localizer.SourceLanguage);
        foreach (var c in Object.FindObjectsByType<Canvas>())
            if (c != null) Object.Destroy(c.gameObject);
        foreach (var es in Object.FindObjectsByType<EventSystem>())
            if (es != null) Object.Destroy(es.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_camera != null) Object.Destroy(_camera);
        _bootstrap = null;
        _camera = null;
        _atTop = _atBottom = _inCorner = _short = null;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (_bootstrap == null)
        {
            PlayModeTestConfig.ConfigureForTests();
            LanguageStartup.PinSourceLanguageForTestRun();

            _camera = new GameObject("Main Camera");
            _camera.tag = "MainCamera";
            _camera.AddComponent<Camera>();

            SaveLoadManager.LastPath = "";
            var autoPath = SaveLoadManager.PathForName(AutoSaveManager.AutoSaveName);
            if (File.Exists(autoPath)) File.Delete(autoPath);

            _bootstrap = new GameObject("Bootstrap");
            _bootstrap.AddComponent<Bootstrap>();
            yield return null;
            yield return null;
        }

        if (Loc.Language != Localizer.SourceLanguage)
        {
            Loc.SetLanguage(Localizer.SourceLanguage);
            yield return RebuildFrames();
        }

        if (_atTop == null)
        {
            var screen = CanvasRect.rect;
            _atTop = MakeDropdown("GuardTop", 40, screen.height * 0.5f - 24f, 0f, 35);
            _atBottom = MakeDropdown("GuardBottom", 40, -screen.height * 0.5f + 24f, 0f, 3);
            _inCorner = MakeDropdown("GuardCorner", 40, -screen.height * 0.5f + 24f, screen.width * 0.5f - 100f, 39);
            _short = MakeDropdown("GuardShort", 3, 0f, 0f, 1);
            yield return null;
        }
    }

    private static IEnumerator RebuildFrames()
    {
        for (int i = 0; i < 4; i++) yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var dd in new[] { _atTop, _atBottom, _inCorner, _short })
            if (dd != null) dd.Hide();
        foreach (var dd in Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (dd != null && dd.transform.Find("Dropdown List") != null) dd.Hide();
        var overlay = UIManager.Instance?.Canvas != null ? CanvasRect.Find("MultiSelectOverlay") : null;
        if (overlay != null) Object.Destroy(overlay.gameObject);
        if (UIManager.Instance?.SettingsPanel != null) UIManager.Instance.SettingsPanel.SetVisible(false);
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.Destroy(e.gameObject);
        yield break;
    }

    private static RectTransform CanvasRect => (RectTransform)UIManager.Instance!.Canvas!.transform;

    private static List<string> Options(int count) =>
        Enumerable.Range(1, count).Select(i => "Вариант " + i).ToList();

    private static TMP_Dropdown MakeDropdown(string name, int count, float yFromCenter, float xFromCenter, int value)
    {
        var dd = UIFactory.CreateDropdown(name, CanvasRect, Options(count), Vector2.zero,
            new Vector2(180f, 32f), null!);
        var rt = (RectTransform)dd.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(xFromCenter, yFromCenter);
        dd.SetValueWithoutNotify(value);
        dd.RefreshShownValue();
        return dd;
    }

    private static IEnumerator OpenAndSettle(TMP_Dropdown dd)
    {
        dd.Show();
        yield return null;
    }

    private static RectTransform OpenList(TMP_Dropdown dd)
    {
        var list = dd.transform.Find("Dropdown List");
        Assert.IsNotNull(list, dd.name + ": список не раскрылся");
        return (RectTransform)list!;
    }

    private static Rect InCanvasSpace(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        var canvas = CanvasRect;
        var a = canvas.InverseTransformPoint(corners[0]);
        var b = canvas.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    private static void AssertInsideScreen(string who, RectTransform list)
    {
        var r = InCanvasSpace(list);
        var screen = CanvasRect.rect;
        Assert.That(r.xMin, Is.GreaterThanOrEqualTo(screen.xMin - Tolerance), who + ": список вылез за левый край экрана");
        Assert.That(r.xMax, Is.LessThanOrEqualTo(screen.xMax + Tolerance), who + ": список вылез за правый край экрана");
        Assert.That(r.yMin, Is.GreaterThanOrEqualTo(screen.yMin - Tolerance), who + ": список вылез за нижний край экрана");
        Assert.That(r.yMax, Is.LessThanOrEqualTo(screen.yMax + Tolerance), who + ": список вылез за верхний край экрана");
    }

    private static void AssertScrollsWhenItDoesNotFit(string who, TMP_Dropdown dd, RectTransform list)
    {
        var scroll = list.GetComponent<ScrollRect>();
        Assert.IsNotNull(scroll, who + ": у списка нет ScrollRect");
        float viewportH = scroll!.viewport.rect.height;
        float contentH = scroll.content.rect.height;
        Assert.That(viewportH, Is.LessThanOrEqualTo(UIFactory.DropdownListMaxHeight + Tolerance),
            who + ": окно списка выше предела UIStyle");
        if (contentH <= viewportH + Tolerance) return;

        var bar = scroll.verticalScrollbar;
        Assert.IsNotNull(bar, who + ": пунктов " + dd.options.Count + " не помещаются в окно, а полосы прокрутки нет");
        Assert.IsTrue(bar!.gameObject.activeInHierarchy,
            who + ": полоса прокрутки есть, но скрыта при том, что пункты не помещаются");
        Assert.That(bar.size, Is.LessThan(0.99f), who + ": ползунок во всю дорожку — полоса не знает о длине списка");
        Assert.IsTrue(scroll.vertical, who + ": вертикальная прокрутка выключена");
        var track = InCanvasSpace((RectTransform)bar.transform);
        var handle = InCanvasSpace(bar.handleRect);
        Assert.That(handle.yMin, Is.GreaterThanOrEqualTo(track.yMin - Tolerance), who + ": ползунок вылез под дорожку");
        Assert.That(handle.yMax, Is.LessThanOrEqualTo(track.yMax + Tolerance), who + ": ползунок вылез над дорожкой");
        Assert.That(handle.xMin, Is.GreaterThanOrEqualTo(track.xMin - Tolerance), who + ": ползунок шире дорожки слева");
        Assert.That(handle.xMax, Is.LessThanOrEqualTo(track.xMax + Tolerance), who + ": ползунок шире дорожки справа");
    }

    private static void AssertSelectedItemVisible(string who, TMP_Dropdown dd, RectTransform list)
    {
        var scroll = list.GetComponent<ScrollRect>()!;
        var selected = list.GetComponentsInChildren<Toggle>(false).FirstOrDefault(t => t.isOn);
        Assert.IsNotNull(selected, who + ": в раскрытом списке ни один пункт не отмечен");
        var view = InCanvasSpace(scroll.viewport);
        var item = InCanvasSpace((RectTransform)selected!.transform);
        Assert.That(item.center.y, Is.InRange(view.yMin - Tolerance, view.yMax + Tolerance),
            who + ": выбранный пункт №" + dd.value + " при открытии остался за краем окна списка");
    }

    private static void AssertFullListContract(string who, TMP_Dropdown dd)
    {
        var list = OpenList(dd);
        AssertInsideScreen(who, list);
        AssertScrollsWhenItDoesNotFit(who, dd, list);
        AssertSelectedItemVisible(who, dd, list);
    }

    [UnityTest]
    public IEnumerator LongList_AtTheTop_HasScrollbar_ShowsSelected_StaysOnScreen()
    {
        var dd = _atTop!;
        yield return OpenAndSettle(dd);
        AssertFullListContract("список у верхнего края", dd);
    }

    [UnityTest]
    public IEnumerator LongList_AtTheBottom_OpensUpward_HasScrollbar_StaysOnScreen()
    {
        var dd = _atBottom!;
        yield return OpenAndSettle(dd);
        AssertFullListContract("список у нижнего края", dd);
        var r = InCanvasSpace(OpenList(dd));
        var control = InCanvasSpace((RectTransform)dd.transform);
        Assert.That(r.yMin, Is.GreaterThanOrEqualTo(control.yMax - 4f),
            "у нижнего края места под полем нет — список обязан открыться ВВЕРХ от него");
    }

    [UnityTest]
    public IEnumerator LongList_InTheBottomRightCorner_StaysOnScreen()
    {
        var dd = _inCorner!;
        yield return OpenAndSettle(dd);
        AssertFullListContract("список в правом нижнем углу", dd);
    }

    [UnityTest]
    public IEnumerator ShortList_NeedsNoScroll_AndStaysOnScreen()
    {
        var dd = _short!;
        yield return OpenAndSettle(dd);
        var list = OpenList(dd);
        AssertInsideScreen("короткий список", list);
        var scroll = list.GetComponent<ScrollRect>()!;
        Assert.That(scroll.content.rect.height, Is.LessThanOrEqualTo(scroll.viewport.rect.height + Tolerance),
            "три пункта обязаны помещаться без прокрутки");
    }

    private const int GeneralTab = 0;

    private static void EnsureSettingsOpen()
    {
        var panel = UIManager.Instance!.SettingsPanel!;
        panel.OpenTab(GeneralTab);
        var scroll = panel.WindowRect!.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null) scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, 0f);
    }

    private static TMP_Dropdown[] DropdownsOfTheApp() =>
        UIManager.Instance!.Canvas!.GetComponentsInChildren<TMP_Dropdown>(true);

    private static TMP_Dropdown LanguageDropdown() =>
        DropdownsOfTheApp().First(d => d.name == "Dd_" + SettingsGeneralTab.LanguageRowId);
    [UnityTest]
    public IEnumerator EveryDropdownOfTheApp_IsBuiltByTheFactory_AndEveryLongOneScrolls()
    {
        var ui = UIManager.Instance!;
        EnsureSettingsOpen();
        var board = ElementFactory.CreatePart(new Vector3Int(600, 400, 16), "Shelf", Vector3.zero)
            .GetComponent<KitchenElement>();
        ui.OpenContextMenu(board);
        yield return null;

        var all = DropdownsOfTheApp()
            .Where(d => d.template != null).ToList();
        Assert.That(all.Count, Is.GreaterThan(3), "в приложении нашлось слишком мало списков — обход сломан");

        var notFromFactory = all.Where(d => d.GetType() != typeof(ScrollableDropdown)).Select(d => d.name).ToList();
        Assert.IsEmpty(notFromFactory,
            "списки собраны в обход UIFactory.CreateDropdown, у них нет полосы прокрутки и ограничения по экрану: "
            + string.Join(", ", notFromFactory));

        var withoutBar = all.Where(d => d.template.GetComponent<ScrollRect>()?.verticalScrollbar == null)
            .Select(d => d.name).ToList();
        Assert.IsEmpty(withoutBar, "у шаблона списка нет полосы прокрутки: " + string.Join(", ", withoutBar));

        var longOnes = all.Where(d => !d.name.StartsWith("Guard") && d.gameObject.activeInHierarchy
            && d.IsInteractable() && d.options.Count > UIStyle.DropdownVisibleItems).ToList();
        Assert.IsNotEmpty(longOnes, "ни одного длинного списка среди видимых — проверять нечего");
        foreach (var dd in longOnes)
        {
            yield return OpenAndSettle(dd);
            AssertFullListContract(dd.name, dd);
            dd.Hide();
        }
    }

    [UnityTest]
    public IEnumerator MultiSelectFilter_WithManyOptions_HasScrollbar_AndStaysOnScreen()
    {
        var screen = CanvasRect.rect;
        var filter = MultiSelectDropdown.Create("GuardFilter", CanvasRect, "Все",
            new Vector2(0f, 0f), new Vector2(200f, 28f), () => { });
        var rt = (RectTransform)filter.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -screen.height * 0.5f + 24f);
        filter.SetOptions(Enumerable.Range(1, 40).Select(i => "COL-" + i.ToString("D2")).ToArray());

        filter.GetComponent<Button>().onClick.Invoke();
        yield return null;

        var overlay = UIManager.Instance?.Canvas != null ? CanvasRect.Find("MultiSelectOverlay") : null;
        Assert.IsNotNull(overlay, "список фильтра не раскрылся");
        var popup = (RectTransform)overlay!.Find("Popup");
        AssertInsideScreen("фильтр", popup);
        var scroll = popup.GetComponentInChildren<ScrollRect>();
        Assert.IsNotNull(scroll);
        Assert.IsNotNull(scroll!.verticalScrollbar, "40 пунктов не помещаются, а полосы прокрутки у фильтра нет");
        Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeInHierarchy, "полоса прокрутки фильтра скрыта");
        Object.Destroy(filter.gameObject);
    }

    [UnityTest, Order(100)]
    public IEnumerator LanguageList_RendersEveryNativeName_InRuAndEnInterface_AndArabicIsShapedRightToLeft()
    {
        foreach (var uiLanguage in new[] { "ru", "en" })
        {
            if (Loc.Language != uiLanguage)
            {
                Loc.SetLanguage(uiLanguage);
                yield return RebuildFrames();
            }
            EnsureSettingsOpen();
            yield return null;
            Assert.AreEqual(uiLanguage, Loc.Language);
            Assert.IsEmpty(OsFontFallback.AttachedFonts,
                "при языке интерфейса " + uiLanguage + " запасные шрифты не подключены — вот это и проверяет тест");

            var dd = LanguageDropdown();
            Assert.IsTrue(dd.gameObject.activeInHierarchy, "строка «Язык» не видна");
            yield return OpenAndSettle(dd);
            var toggles = OpenList(dd).GetComponentsInChildren<Toggle>(false);
            Assert.AreEqual(Loc.Languages.Count, toggles.Length, "пунктов списка не столько, сколько языков");

            var missing = new List<string>();
            for (int i = 0; i < toggles.Length; i++)
            {
                var label = toggles[i].GetComponentInChildren<TMP_Text>();
                string name = Loc.Languages[i].NativeName;
                Assert.AreEqual(name, label.text, "порядок пунктов не совпал с порядком языков");
                foreach (char c in RightToLeftLabel.Rendered(label))
                {
                    if (char.IsWhiteSpace(c) || char.IsControl(c)) continue;
                    if (!label.font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: true))
                        missing.Add(Loc.Languages[i].Code + " «" + name + "»: U+" + ((int)c).ToString("X4"));
                }
            }
            Assert.IsEmpty(missing, "в списке языков (интерфейс " + uiLanguage + ") нет глифов, на экране квадраты: "
                + string.Join(" | ", missing.Distinct().Take(30)));

            AssertArabicShapedAndRussianNot(toggles);
            dd.Hide();
        }
    }

    private static void AssertArabicShapedAndRussianNot(Toggle[] toggles)
    {
        var languages = Loc.Languages.ToList();
        int arabic = languages.FindIndex(l => l.Code.StartsWith("ar"));
        Assert.GreaterOrEqual(arabic, 0, "арабского языка нет в списке — проверять нечего");
        var label = toggles[arabic].GetComponentInChildren<TMP_Text>();
        Assert.IsTrue(label.isRightToLeftText, "арабское название в списке не раскладывается справа налево");
        string shown = RightToLeftLabel.Rendered(label);
        Assert.AreNotEqual(label.text, shown, "арабское название не прошло через ArabicShaper — буквы будут оторваны");
        Assert.IsFalse(shown.Any(c => c >= 'ء' && c <= 'ي'),
            "в арабском названии остались базовые буквы 0621–064A — формы представления не подставлены");
        Assert.IsNull(toggles[languages.FindIndex(l => l.Code == "ru")].GetComponentInChildren<RightToLeftLabel>(),
            "русское название не должно раскладываться справа налево");
    }
}
