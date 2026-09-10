using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Analysis;

/// <summary>
/// Общая оснастка КАДРОВЫХ тестов — тех, чей смысл в том, чтобы показать, как
/// элемент ВЫГЛЯДИТ.
///
/// Зачем она появилась. <c>ElementHighlighter.ViolationTintVisible</c> по умолчанию
/// <c>true</c>, и элемент с нарушением подмешивает к своему цвету красное
/// (<c>ValidityTint</c>). Индикатор валидации — это не то, ради чего кадр
/// снимают, поэтому в кадровых тестах тон выключается ровно так же, как это
/// делает фоторежим (<c>PhotoMode.Enter</c>: выключить, обновить подсветку;
/// <c>PhotoMode.Exit</c>: вернуть).
///
/// Выключение живёт в ОДНОМ месте — <see cref="CaptureFramePng"/>, — и не
/// раньше: <c>Bootstrap</c> в <c>[UnitySetUp]</c> восстанавливает проект и
/// перекрашивает сцену. Значение, выставленное до него, дало бы кадр, снятый
/// до перекраски.
///
/// Возврат в <c>[TearDown]</c> обязателен: <c>ViolationTintVisible</c> — глобальный
/// статик, а <c>conventions/SERIALIZATION.md</c> требует, чтобы набор,
/// прогнанный в одиночку, и он же в полном прогоне давали один результат.
///
/// И главное следствие: выключенный тон означает, что кадр БОЛЬШЕ НЕ КРАСНЕЕТ
/// глазами. Регрессию расстановки теперь видит только утверждение про
/// нарушения — поэтому оно стоит в той же самой функции, что и выключение
/// тона, ДО съёмки. Снять кадр, не задав вопрос валидатору, здесь физически
/// нечем; за этим следит <see cref="ElementFrameCoverageTests"/>.
/// </summary>
public abstract class ElementFrameTests
{
    /// <summary>Префикс кодов, которые КРАСЯТ. Правила GAP-*, DRW-*, LEG-* и
    /// прочие в цвет элемента не попадают вовсе.</summary>
    private const string CollisionCodePrefix = "COL-";

    private bool _tintTaken;
    private bool _violationTintWas;

    [TearDown]
    public void RestoreValidationTint()
    {
        if (!_tintTaken) return;
        ElementHighlighter.ViolationTintVisible = _violationTintWas;
        _tintTaken = false;
    }

    /// <summary>Единственный хелпер про валидность кадра. Раньше их было два —
    /// <c>AssertFrameShowsDecorNotViolationTint</c> спрашивал, попал ли В
    /// СПИСОК нарушителей сам снимаемый элемент, а
    /// <c>AssertFrameSceneHasNoCollisions</c> — есть ли в сцене хоть один код
    /// COL-*. Это один и тот же вопрос двумя именами: элемент попадает в
    /// <c>violations</c> ровно тогда, когда про него выписан COL-*, а сцена
    /// кадрового теста — это сам элемент и его опора, так что «нарушителей
    /// нет вообще» строго сильнее и не требует передавать элемент.
    ///
    /// Коды COL-* — ровно те правила, которые красят
    /// (<c>ElementHighlighter.ApplyMaterial</c> берёт цвет из
    /// <c>ConstraintValidator.Validate(...).violations</c>); остальные (GAP-*,
    /// DRW-*, DWH-*, LEG-*) сцену не красят и идут в сообщение справкой.
    ///
    /// Оговорки «нарушение задумано» здесь нет намеренно. Параметр
    /// <c>overlapIsByDesign</c> заводился ради комнатных кадров, где четыре
    /// стены законно перекрывались на углах; после того как валидация
    /// научилась прощать угол (<c>WallCentreline.MeetAtSharedCorner</c>), у
    /// него не осталось ни одного вызывающего с непустой причиной. Ветка,
    /// которую ничто не зовёт, ничем и не проверяется, а готовая площадка —
    /// это то, что следующий агент применит не подумав: увидев красный кадр,
    /// он объявит нарушение задуманным вместо того, чтобы чинить
    /// расстановку. Понадобится снова — завести заново в тот же день, вместе
    /// с кадром, который её использует.</summary>
    protected static void AssertFrameShowsMaterialNotViolationTint(string frame)
    {
        var lines = new List<string>();
        var tinting = new List<string>();
        foreach (var issue in SceneAnalyzer.Analyze())
        {
            string line = issue.Level + " " + issue.Code + " " + issue.Detail
                + " — " + issue.Message;
            lines.Add(line);
            if (issue.Code.StartsWith(CollisionCodePrefix)) tinting.Add(line);
        }

        Assert.IsEmpty(tinting,
            frame + ": расстановка в кадре нарушает правило валидации.\n"
            + "Правило: кадровый тест снимает МАТЕРИАЛ элемента, поэтому "
            + "валидационный тон в нём выключен — покрасневший элемент больше "
            + "не видно глазами, и регрессию расстановки сторожит только эта "
            + "проверка. Чинить надо расстановку в самом тесте (опору, высоту, "
            + "зазоры), а не проверку: оговорки «так задумано» здесь нет и "
            + "заводить её задним числом ради одного красного кадра нельзя. "
            + "Разрешения на правку расстановки не нужно, это часть той же "
            + "правки.\n"
            + "Нарушения сцены:\n" + string.Join("\n", lines));
    }

    /// <summary>Снять кадр в файл. Единственная точка, где кадровый набор
    /// получает пиксели, — и потому единственное место, где выключается тон и
    /// задаётся вопрос про нарушения.</summary>
    protected IEnumerator CaptureFramePng(Camera cam, string png, int width, int height)
    {
        AssertFrameShowsMaterialNotViolationTint(png);
        DisableValidationTint();

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        yield return null;
        yield return null;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        string dir = Path.Combine(Application.dataPath, "..", "test-results");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, png);
        File.WriteAllBytes(path, tex.EncodeToPNG());

        Assert.IsTrue(File.Exists(path), "PNG не создан: " + path);
        Assert.IsTrue(new FileInfo(path).Length > 0, "PNG пустой: " + path);
        Debug.Log("[ISO] Saved: " + path);

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    /// <summary>Тот же приём, что у <c>PhotoMode.Enter</c>: запомнить, снять
    /// флаг и ПЕРЕКРАСИТЬ уже расставленные элементы. Без перекраски флаг
    /// подействовал бы только на тех, кого кто-то тронет после него, — а
    /// материалы на сцене уже проставлены при создании.</summary>
    private void DisableValidationTint()
    {
        if (!_tintTaken)
        {
            _violationTintWas = ElementHighlighter.ViolationTintVisible;
            _tintTaken = true;
        }
        if (!ElementHighlighter.ViolationTintVisible) return;

        ElementHighlighter.ViolationTintVisible = false;
        if (ElementHighlighter.Instance != null)
            ElementHighlighter.Instance.RefreshHighlights();
    }
}

/// <summary>
/// Сторож полноты кадровых проверок.
///
/// Выключенный валидационный тон стоит ровно столько, сколько стоит
/// утверждение, которое его заменяет. Список «в этих тестах проверка есть»
/// прожил бы до первого нового кадра, поэтому здесь сторожится не список, а
/// КОНСТРУКЦИЯ: тон выключается и вопрос валидатору задаётся в одной и той же
/// функции <c>ElementFrameTests.CaptureFramePng</c>, а наследники не имеют
/// права ни снимать пиксели сами, ни трогать <c>ViolationTintVisible</c>. Тогда «кадр
/// без проверки» невозможно написать: чтобы кадр показывал материал, набор
/// обязан пройти через оснастку, а внутри неё проверка безусловна.
/// </summary>
public class ElementFrameCoverageTests
{
    private const string SuiteDir = "Tests/PlayMode";

    private static List<System.Type> FrameSuites() =>
        typeof(ElementFrameTests).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ElementFrameTests)))
            .OrderBy(t => t.Name)
            .ToList();

    private static string SourceOf(System.Type suite)
    {
        string path = Path.Combine(Application.dataPath, SuiteDir, suite.Name + ".cs");
        Assert.IsTrue(File.Exists(path),
            "исходник набора " + suite.Name + " не найден по " + path + ". Скан ищет файл "
            + "по имени класса; переименованный или переехавший набор молча выпал бы из "
            + "проверки, и сканы ниже стали бы зелёными ни на чём — держите файл и класс "
            + "одноимёнными либо чините этот поиск вместе с переездом");
        return File.ReadAllText(path);
    }

    /// <summary>Доказательство скана до того, как ему верить: пустая выборка
    /// сделала бы оба скана ниже зелёными, ничего не проверив. Четыре набора —
    /// это те, ради которых оснастка написана; пятый и далее только добавятся.</summary>
    [Test]
    public void TheScanFindsTheFrameSuites_SoAnEmptyResultCannotPassForGreen()
    {
        var suites = FrameSuites();
        Assert.GreaterOrEqual(suites.Count, 4,
            "скан не нашёл кадровых наборов: значит он смотрит не туда, и оба сканирующих "
            + "теста ниже проходят на пустоте. Найдено: "
            + string.Join(", ", suites.Select(t => t.Name)));

        string fixture = File.ReadAllText(
            Path.Combine(Application.dataPath, SuiteDir, nameof(ElementFrameTests) + ".cs"));
        int assertAt = fixture.IndexOf("AssertFrameShowsMaterialNotViolationTint(png)",
            System.StringComparison.Ordinal);
        int disableAt = fixture.IndexOf("DisableValidationTint();", System.StringComparison.Ordinal);
        Assert.Greater(assertAt, 0,
            "в CaptureFramePng больше нет вопроса валидатору — кадры снимаются без тона И "
            + "без проверки, то есть регрессию расстановки не видит уже никто");
        Assert.Greater(disableAt, assertAt,
            "тон обязан выключаться ПОСЛЕ утверждения и в той же функции: разнесённые по "
            + "разным местам, они разойдутся, и снова появится кадр без тона и без проверки");
    }

    [Test]
    public void NoFrameSuite_TakesItsOwnPixels_SoEveryFrameGoesThroughTheCheckedCapture()
    {
        var offenders = new List<string>();
        foreach (var suite in FrameSuites())
        {
            string src = SourceOf(suite);
            if (src.Contains("EncodeToPNG") || src.Contains("ReadPixels"))
                offenders.Add(suite.Name);
        }

        Assert.IsEmpty(offenders,
            "Правило: кадр в этих наборах снимает только ElementFrameTests.CaptureFramePng — "
            + "там же, где выключается валидационный тон, стоит утверждение про нарушения, и "
            + "разделить их нельзя. Набор, читающий пиксели сам, обходит проверку: его кадр "
            + "не краснеет ни глазами (тон выключен), ни тестом. Снимайте через "
            + "CaptureFramePng, а если нужен другой размер или другая камера — добавьте "
            + "параметр в оснастку. Обходят: " + string.Join(", ", offenders));
    }

    [Test]
    public void NoFrameSuite_SwitchesTheValidationTintItself_SoTheCheckCannotBeOrphaned()
    {
        var offenders = new List<string>();
        foreach (var suite in FrameSuites())
            if (SourceOf(suite).Contains("ViolationTintVisible"))
                offenders.Add(suite.Name);

        Assert.IsEmpty(offenders,
            "Правило: валидационным тоном в кадровых наборах распоряжается только оснастка. "
            + "Набор, который гасит тон сам, получает кадр без индикатора и без утверждения "
            + "про нарушения — ровно та невидимая регрессия расстановки, ради которой "
            + "утверждение и заведено. Уберите строку: CaptureFramePng уже гасит тон и уже "
            + "спрашивает валидатор. Распоряжаются сами: " + string.Join(", ", offenders));
    }
}
