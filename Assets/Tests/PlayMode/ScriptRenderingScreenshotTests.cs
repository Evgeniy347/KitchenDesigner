using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using KitchenDesigner.Core;
using KitchenDesigner.Core.UI;

/// <summary>
/// Японский, китайский и тунисский арабский — три письменности, которых нет в LiberationSans.
/// Тест переключает язык так же, как человек (живая пересборка интерфейса), снимает окно
/// настроек и панель свойств детали в test-results/script_*.png и проверяет то, что видно
/// глазами, сенсорами:
///   * «тофу» — каждый видимый символ каждой подписи обязан найтись в шрифте или в его
///     запасных шрифтах (системные шрифты Windows, подключённые OsFontFallback);
///   * арабский — в отрисованном тексте не осталось ни одной базовой буквы 0621–064A: все
///     заменены формами представления (иначе буквы стоят оторванными), подпись выровнена
///     вправо и раскладывается справа налево.
/// Переполнение подписей тест печатает списком в журнал, а судит о нём человек по кадру:
/// длина перевода — забота переводчика, а не шрифта.
/// Если файла перевода ещё нет (переводчики пишут их параллельно), язык собирается из
/// английского с несколькими родными строками — проверяется отрисовка, а не перевод.
/// </summary>
public class ScriptRenderingScreenshotTests
{
    private GameObject? _bootstrap;
    private GameObject? _camera;

    private static readonly Regex Tags = new Regex("<[^<>]+>");

    private static readonly Dictionary<string, Dictionary<string, string>> NativeSamples = new()
    {
        ["ja"] = new() { ["@name"] = "日本語", ["settings.project.language"] = "言語", ["settings.tab.project"] = "プロジェクト", ["element.common.width"] = "幅" },
        ["zh-Hans"] = new() { ["@name"] = "简体中文", ["settings.project.language"] = "语言", ["settings.tab.project"] = "项目", ["element.common.width"] = "宽度" },
        ["ar-TN"] = new() { ["@name"] = "العربية (تونس)", ["@rtl"] = "true", ["settings.project.language"] = "اللغة", ["settings.tab.project"] = "المشروع", ["element.common.width"] = "العرض" },
    };

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
        Loc.Reload();
        Loc.SetLanguage(Localizer.SourceLanguage);
        for (int i = 0; i < 5; i++) yield return null;

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

    [UnityTest]
    public IEnumerator Japanese_SettingsAndPropertiesPanel_HaveNoMissingGlyphs() => Run("ja");

    [UnityTest]
    public IEnumerator Chinese_SettingsAndPropertiesPanel_HaveNoMissingGlyphs() => Run("zh-Hans");

    [UnityTest]
    public IEnumerator Arabic_SettingsAndPropertiesPanel_AreJoinedRightAlignedAndCovered() => Run("ar-TN");

    private IEnumerator Run(string language)
    {
        UseLanguage(language);
        for (int i = 0; i < 5; i++) yield return null;
        Assert.AreEqual(language, Loc.Language);
        Assert.IsNotEmpty(OsFontFallback.AttachedFonts,
            "на машине прогона (Windows) системный шрифт для " + language + " обязан найтись и подключиться запасным");

        var board = ElementFactory.CreatePart(new Vector3Int(600, 400, 16), "Shelf", Vector3.zero).GetComponent<KitchenElement>();
        var ui = UIManager.Instance!;
        ui.ToggleSettings();
        ui.OpenContextMenu(board);
        yield return null;
        yield return null;

        foreach (var panel in new[] { "SettingsPanel", "ContextMenu" })
        {
            var root = UiTestTree.FindDeep(ui.Canvas!.transform, panel);
            Assert.IsNotNull(root, panel + " не построен");
            var labels = root!.GetComponentsInChildren<TMP_Text>(false).Where(t => !string.IsNullOrWhiteSpace(t.text)).ToList();
            Assert.That(labels.Count, Is.GreaterThan(10), panel + ": подписей нет — проверять нечего");

            AssertNoMissingGlyphs(language, panel, labels);
            if (Loc.IsRightToLeft) AssertRightToLeft(panel, labels);
            AssertHintBadgesClearOfText(panel, root);
            ReportOverflow(language, panel, labels);

            yield return CapturePanel(ui.Canvas!, root, "script_" + language + "_" + panel.ToLowerInvariant() + ".png");
        }
    }

    private static void UseLanguage(string language)
    {
        var current = Loc.Current;
        if (current.Table(language) != null)
        {
            Loc.SetLanguage(language);
            return;
        }
        var tables = current.Languages.Select(l => current.Table(l.Code)!).ToList();
        tables.Add(new StringTable(language, NativeSamples[language]));
        Loc.Use(new Localizer(tables, language));
    }

    private static string Rendered(TMP_Text label) =>
        Tags.Replace(label.textPreprocessor != null ? label.textPreprocessor.PreprocessText(label.text) : label.text, "");

    private static void AssertNoMissingGlyphs(string language, string panel, List<TMP_Text> labels)
    {
        var missing = new List<string>();
        foreach (var label in labels)
        {
            var font = label.font;
            foreach (char c in Rendered(label))
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c) || char.IsSurrogate(c) || c == '​') continue;
                if (!font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: true))
                    missing.Add(label.name + ": U+" + ((int)c).ToString("X4") + " «" + c + "»");
            }
        }
        Assert.IsEmpty(missing, language + ", " + panel + ": символы без глифа (на экране пустые квадраты): "
            + string.Join(" | ", missing.Distinct().Take(30)));
    }

    private static void AssertRightToLeft(string panel, List<TMP_Text> labels)
    {
        var withoutSeam = labels.Where(t => t.GetComponentInParent<TMP_InputField>() == null && t.GetComponent<RightToLeftLabel>() == null)
            .Select(t => t.name).ToList();
        Assert.IsEmpty(withoutSeam, panel + ": подпись собрана мимо UIFactory.CreateLabel и не получила арабской раскладки: "
            + string.Join(", ", withoutSeam.Take(20)));

        var leftAligned = labels.Where(t => t.GetComponent<RightToLeftLabel>() != null && t.horizontalAlignment == HorizontalAlignmentOptions.Left)
            .Select(t => t.name).ToList();
        Assert.IsEmpty(leftAligned, panel + ": в арабском интерфейсе подпись прижата влево: " + string.Join(", ", leftAligned.Take(20)));

        var unjoined = labels.Where(t => t.GetComponent<RightToLeftLabel>() != null)
            .Where(t => Rendered(t).Any(c => c >= 'ء' && c <= 'ي' && c != ArabicShaper.Tatweel))
            .Select(t => t.name + ": " + t.text).ToList();
        Assert.IsEmpty(unjoined, panel + ": в отрисованном тексте осталась базовая арабская буква — TMP не соединяет буквы сам: "
            + string.Join(" | ", unjoined.Take(10)));

        var arabic = labels.Count(t => ArabicShaper.HasArabic(t.text));
        Assert.That(arabic, Is.GreaterThan(0), panel + ": ни одной арабской подписи — язык не переключился, проверять нечего");
    }

    private static void AssertHintBadgesClearOfText(string panel, Transform root)
    {
        var overlapping = new List<string>();
        foreach (var badge in root.GetComponentsInChildren<HintBadge>(false))
        {
            var label = badge.transform.parent.GetComponent<TMP_Text>();
            if (label == null) continue;
            label.ForceMeshUpdate();
            var text = label.textBounds;
            var rect = (RectTransform)badge.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float left = label.rectTransform.InverseTransformPoint(corners[0]).x;
            float right = label.rectTransform.InverseTransformPoint(corners[2]).x;
            if (right > text.min.x + 1f && left < text.max.x - 1f)
                overlapping.Add(label.name + ": значок [" + left.ToString("F0") + "; " + right.ToString("F0") + "], текст [" + text.min.x.ToString("F0") + "; " + text.max.x.ToString("F0") + "]");
        }
        Assert.IsEmpty(overlapping, panel + ": значок «i» лежит поверх текста своей подписи (в арабском он обязан встать слева от текста, а не справа): "
            + string.Join(" | ", overlapping));
    }

    private static void ReportOverflow(string language, string panel, List<TMP_Text> labels)
    {
        var overflowing = new List<string>();
        foreach (var label in labels)
        {
            if (label.GetComponentInParent<TMP_InputField>() != null) continue;
            label.ForceMeshUpdate();
            float width = label.rectTransform.rect.width;
            if (width > 0 && label.textWrappingMode == TextWrappingModes.NoWrap && label.preferredWidth > width + 1f)
                overflowing.Add(label.name + " (" + Mathf.RoundToInt(label.preferredWidth) + " > " + Mathf.RoundToInt(width) + "): " + label.text);
        }
        Debug.Log("[SCRIPT-OVERFLOW] " + language + " " + panel + ": " + overflowing.Count
            + (overflowing.Count == 0 ? "" : " — " + string.Join(" | ", overflowing.Take(40))));
    }

    private static IEnumerator CapturePanel(Canvas canvas, Transform panel, string fileName)
    {
        var hidden = UiTestTree.HideAllExcept(canvas.transform, panel);
        var panelRt = (RectTransform)panel;
        var origAnchorMin = panelRt.anchorMin;
        var origAnchorMax = panelRt.anchorMax;
        var origPivot = panelRt.pivot;
        var origPos = panelRt.anchoredPosition;

        var scaler = canvas.GetComponent<CanvasScaler>();
        var origScaleMode = scaler.uiScaleMode;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        var origRenderMode = canvas.renderMode;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.planeDistance = 1f;
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        yield return null;

        int w = Mathf.Max(1, Mathf.CeilToInt(panelRt.rect.width));
        int h = Mathf.Max(1, Mathf.CeilToInt(panelRt.rect.height));
        var camGo = new GameObject("ScriptCaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = h * 0.5f;
        cam.aspect = (float)w / h;
        cam.cullingMask = 1 << canvas.gameObject.layer;
        canvas.worldCamera = cam;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        var dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("[SCREENSHOT] Saved: " + path + " (" + w + "x" + h + ")");

        RenderTexture.active = null;
        cam.targetTexture = null;
        canvas.worldCamera = null;
        canvas.renderMode = origRenderMode;
        scaler.uiScaleMode = origScaleMode;
        panelRt.anchorMin = origAnchorMin;
        panelRt.anchorMax = origAnchorMax;
        panelRt.pivot = origPivot;
        panelRt.anchoredPosition = origPos;
        UiTestTree.Restore(hidden);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
        Assert.IsTrue(File.Exists(path), "PNG не записан: " + path);
    }
}
