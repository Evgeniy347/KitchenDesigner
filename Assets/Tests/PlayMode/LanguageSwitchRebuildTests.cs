using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Смена языка — без перезапуска. Тексты UI раскладываются при постройке панелей, поэтому
/// перевод «на лету» означает пересборку всего холста: старые панели уничтожаются, новые
/// строятся заново уже на новом языке, а открытые окна открываются снова там же, где были.
/// Тесты держат три вещи: новый язык действительно на экране, холст ровно один (старый не
/// повис невидимым поверх нового), и окно, в котором человек выбрал язык, не захлопнулось
/// у него перед носом.
/// </summary>
public class LanguageSwitchRebuildTests
{
    private GameObject? _bootstrap;
    private GameObject? _camera;

    [UnitySetUp]
    public IEnumerator SetUp()
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

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Loc.SetLanguage(Localizer.SourceLanguage);
        yield return null;
        yield return null;

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.Destroy(e.gameObject);
        foreach (var c in Object.FindObjectsByType<Canvas>())
            if (c != null) Object.Destroy(c.gameObject);
        foreach (var es in Object.FindObjectsByType<EventSystem>())
            if (es != null) Object.Destroy(es.gameObject);
        if (_bootstrap != null) Object.Destroy(_bootstrap);
        if (_camera != null) Object.Destroy(_camera);
        yield return null;
    }

    private static TextMeshProUGUI LanguageRowLabel() =>
        UIManager.Instance!.Canvas!.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Single(t => t.name == "Lbl_" + SettingsProjectTab.LanguageRowId);

    private static readonly string[] Roots = { "SettingsPanel", "Sidebar", "ContextMenu", "DayNightPanel", "ErrorPanel" };

    private static int InterfaceCanvases() =>
        Object.FindObjectsByType<Canvas>().Count(c => c.name == "UICanvas");

    private static IEnumerator UntilRebuilt()
    {
        for (int i = 0; i < 5; i++) yield return null;
    }

    [UnityTest]
    public IEnumerator SetLanguage_RebuildsTheInterface_AndReopensWhatWasOpen()
    {
        var board = ElementFactory.CreatePart(new Vector3Int(600, 400, 16), "Shelf", Vector3.zero)
            .GetComponent<KitchenElement>();
        var ui = UIManager.Instance!;
        ui.ToggleSettings();
        ui.OpenContextMenu(board);
        yield return null;
        Assert.AreEqual("Язык", LanguageRowLabel().text, "прогон закреплён за русским исходником");
        Assert.AreEqual(0, ui.SettingsPanel!.CurrentTab);
        int canvasesBefore = InterfaceCanvases();

        Loc.SetLanguage("en");
        yield return UntilRebuilt();

        Assert.AreEqual("Language", LanguageRowLabel().text,
            "подпись строки обязана прийти из en.json после пересборки, без перезапуска");
        Assert.AreEqual(canvasesBefore, InterfaceCanvases(),
            "старый холст обязан уйти: второй, невидимый, ловил бы клики поверх нового");
        Assert.IsTrue(ui.IsPanelVisible(ToolbarPanel.Settings),
            "язык выбирают в настройках — окно не должно захлопнуться у человека перед носом");
        Assert.AreEqual(0, ui.SettingsPanel!.CurrentTab);
        Assert.AreSame(board, ui.ContextMenu!.OpenTarget,
            "панель свойств выделенной детали после смены языка открыта на той же детали");
    }

    [UnityTest]
    public IEnumerator English_LeavesNoRussianAndNoRawKey_InTheMainWindows()
    {
        Loc.SetLanguage("en");
        yield return UntilRebuilt();

        var canvas = UIManager.Instance!.Canvas!;
        var nativeNames = Loc.Languages.Select(l => l.NativeName).ToHashSet();
        var ruKeys = Loc.Current.Table(Localizer.SourceLanguage)!.Keys.ToHashSet();
        var texts = canvas.GetComponentsInChildren<Transform>(true)
            .Where(t => Roots.Contains(t.name))
            .SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
            .Where(t => !nativeNames.Contains(t.text))
            .ToList();
        Assert.That(texts.Count, Is.GreaterThan(100), "окно настроек и каталог обязаны быть построены — иначе проверять нечего");

        var russian = texts.Where(t => Regex.IsMatch(t.text, "[А-Яа-яЁё]")).Select(t => t.name + ": " + t.text).ToList();
        var rawKeys = texts.Where(t => ruKeys.Contains(t.text)).Select(t => t.name + ": " + t.text).ToList();

        Assert.IsEmpty(russian, "на английском экране осталась русская подпись — строка не вынесена в таблицу: "
            + string.Join(" | ", russian.Take(30)));
        Assert.IsEmpty(rawKeys, "на экране ключ вместо текста — его нет ни в en.json, ни в ru.json: "
            + string.Join(" | ", rawKeys.Take(30)));
    }
}
