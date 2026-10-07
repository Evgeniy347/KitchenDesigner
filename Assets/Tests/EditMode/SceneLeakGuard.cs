using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Сенсор задачи «утечка в общую EditMode-сцену»: SceneRestorerJointRepairTests
/// оставляла после себя ScrewLegElement с настоящим MeshCollider на мировом (0,·,0) —
/// ровно там, где стоит луч камеры в SelectionManagerTests, — и три чужих теста
/// покраснели без единой строки о причине (agents/TEST-DESIGN.md → «A defect the
/// platform cannot report stays invisible until you build the sensor»).
///
/// Прибор сравнивает КОРНЕВЫЕ объекты активной сцены до и после КАЖДОГО EditMode-теста.
/// Проводка — не NUnit'овский <c>[assembly: ITestAction]</c>: в этой связке Unity
/// Test Framework он для EditMode молча не срабатывает вовсе (проверено — тот же
/// класс, применённый ПРЯМО к фикстуре, честно логировал каждый вызов; тот же класс
/// сборочным атрибутом — ни одного). Проводка идёт через
/// <c>UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.RegisterCallbacks</c> —
/// платформенный, а не NUnit'овский крючок, и он на весь прогон ОДИН, без правки
/// каждого файла с тестами.
///
/// Прибор ТОЛЬКО считает и пишет полный список в test-results/scene-leak-report.txt —
/// сжатое сообщение здесь было бы той самой ловушкой из «A brute-force sweep writes
/// the WHOLE list to a file». Судит отчёт отдельный обычный тест
/// (<c>SceneLeakGuardReportTests</c>): падение внутри <c>[OneTimeTearDown]</c> этого
/// прибора НЕ входит в Total/Failed прогона — проверено живьём (Assert.Fail внутри
/// SetUpFixture.OneTimeTearDown честно бросается и ничего не меняет в счёте NUnit
/// для ЭТОЙ сборки), поэтому вердикт — обычный тест-метод, а не сборочный сенсор:
/// обычный тест-кейс считается всегда.</summary>
public static class SceneLeakGuard
{
    /// <summary>Тесты, для которых утечка уже разобрана и не является простым
    /// «забыли добавить в _spawned». Каждая строка — чужой код или намеренно
    /// разделяемый объект, а не моя невнимательность. Список тает: разбор
    /// одной причины закрывает сразу несколько строк — как уже закрыл
    /// ElementActivatorTests/RadialShelfTests/WallDeviceDecorTests/
    /// ElementFactory(Sandbox)Tests в этом же коммите.
    ///
    /// Ключ — ИМЯ ОБЪЕКТА, не только тест (review-perf-tests-tooling.md #2): раньше запись
    /// прощала тесту ЛЮБУЮ утечку — запись EdgeCoverageBroadPhaseEquivalenceTests (заведена
    /// для __TextureOverlays, с тех пор её утечка починена и запись убрана как устаревшая)
    /// заодно прятала бы утёкший ScrewLeg с MeshCollider — ровно тот инцидент, ради которого
    /// сторож построен. objectName оканчивается на `*` — совпадение по префиксу (для семейств
    /// вроде "P_" + тип детали или цепочки "Proba_pipe_elbow..."), иначе — точное имя.</summary>
    public static readonly (string testName, string objectName, string why)[] KnownLeakers =
    {
        // Auto-created overlay/highlight child, производится ElementHighlighter/
        // ElementOutline/TextureOverlayRenderer в ответ на hover/выделение — тест
        // не создаёт и не именует его сам, значит не может и добавить в _spawned.
        ("ContextMenuEdgeSectionTests.Close_DropsHoverHighlight", "__EdgeSideHighlight",
            "авто-объект подсветки ребра, ставит его ElementHighlighter"),
        ("ContextMenuLayoutTests.Wall_AddSameTextureTwice_IsAllowed", "__TextureOverlays",
            "авто-объект наложений текстуры от TextureOverlayRenderer: рождается добавлением из панели"),
        ("ElementHighlighterTests.Transparency_ReturnsTheMaterialTheRendererWore_NotWhatTheElementDecorWouldRepaint", "__Outline",
            "авто-объект контура от ElementHighlighter/ElementOutline"),
        ("ElementHighlighterTests.TransparencyDropped_WhileStillViolating_BecomesOpaqueAndStaysRed", "__Outline",
            "тот же авто-объект контура"),
        ("ElementHighlighterTests.TransparentAndViolating_IsBothAtOnce_SeeThroughAndRed", "__Outline",
            "тот же авто-объект контура"),
        ("ElementHighlighterTests.ViolationDropped_WhileStillTransparent_StaysSeeThroughInItsOwnColour", "__Outline",
            "тот же авто-объект контура"),
        ("ElementHighlighterTests.WallTransparency_CanBeTurnedBackOff", "__Outline",
            "тот же авто-объект контура"),

        // Undo гасит деталь через SetActive(false), а не уничтожает (чтобы Redo
        // мог её вернуть) — agents/TEST-DESIGN.md: «удаление не уничтожает
        // объект». После Undo деталь уходит из PartRegistry, и общий цикл
        // TearDown'а (foreach PartRegistry.GetAll()) её больше не видит.
        ("PipePanelChoiceUndoGuardTests.EveryChoiceRowOfThePropertiesPanel_IsUndoableInOneStep", "P_*",
            "Spawn(type) именует деталь \"P_\" + имя типа (~34 типа) — та же форма деактивации после Undo"),

        // Побочный объект инфраструктуры, который тест не создаёт и не именует
        // сам — PhotoQualityController заводит Volume при применении настройки.
        ("McpSettingsParityTests.EveryExemption_StillNamesALiveSetting_AndAStillMissingOne", "PhotoModeVolume",
            "Volume от PhotoQualityController, побочный эффект настройки"),
        ("McpSettingsParityTests.EverySettingThePanelCanChange_IsAlsoSettableThroughMcp", "PhotoModeVolume",
            "тот же побочный Volume"),

        // Общий на весь прогон EventSystem — agents/TEST-DESIGN.md: «EventSystem
        // в EditMode один на весь прогон». Уничтожать его в TearDown одного
        // теста сломало бы фокус для всех, кто идёт следом.
        ("SettingsSliderUndoTests.AClickOnTheTrack_IsItsOwnStep", "EventSystem",
            "общий на весь прогон, документированное поведение"),
    };

    /// <summary>Отдельно от <see cref="KnownLeakers"/> и НАМЕРЕННО грубее: эти шесть фикстур
    /// поднимают целиком замороженную сцену в ~400 деталей (Vintovaya_opora_*,
    /// Leg_*, все стены) и убирают её только в [OneTimeTearDown] — перечислить каждое имя было
    /// бы отдельной задачей на файл, не однострочной правкой. Запись прощает тесту ЛЮБУЮ
    /// утечку — тем и отличается от KnownLeakers, где имя объекта проверяется; заводить сюда
    /// тест с точечной утечкой (как раньше EdgeCoverageBroadPhaseEquivalenceTests) означало бы
    /// вернуть дыру, ради которой review-perf-tests-tooling.md #2 переписан.</summary>
    public static readonly (string testName, string why)[] KnownWholeSceneLeakers =
    {
        ("PipeGapSensorTests.MouthToMouthGap_OnASelfSeatedScene_IsWithinTheProjectJoinTolerance",
            "живая сцена теста (SensorPipe/SensorElbow) поднята без уборки — сенсор, не приёмка"),
        ("PipeGapSensorTests.MouthToMouthGap_TrubaToOtvod91_OnTheFrozenUserScene",
            "полная замороженная сцена пользователя — сенсор зазоров, а не acceptance-тест"),
        ("SaveValidationSensorTests.Analyze_LiveExampleSave_PrintsFindings",
            "полная замороженная сцена — печатающий сенсор (TestContext.WriteLine), не приёмка"),
        ("SaveValidationTests.Analyze_FrozenPipeGapScene_MatchesKnownIssueBaseline",
            "полная замороженная сцена pipe-gap"),
        ("SaveValidationTests.RoundTrip_ExampleSave_ReportsPoseDrift",
            "полная замороженная сцена example.save.json"),
        ("ValidationInvariantTests.Validation_ExampleSave_IsDeterministic",
            "полная замороженная сцена example.save.json"),
    };

    public const string ReportPath = "test-results/scene-leak-report.txt";

    [ThreadStatic] private static HashSet<int>? _before;

    private static readonly List<string> _reportLines = new List<string>();

    private static int _leakingTestCount;

    public static void SnapshotBefore()
    {
        var before = _before ??= new HashSet<int>();
        before.Clear();
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var go in roots)
            if (go != null) before.Add(go.GetInstanceID());
    }

    public static void CheckAfter(string testFullName)
    {
        var before = _before;
        if (before == null) return;

        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        List<string>? leaked = null;
        foreach (var go in roots)
        {
            if (go == null || before.Contains(go.GetInstanceID())) continue;
            (leaked ??= new List<string>()).Add(go.name);
        }
        before.Clear();
        if (leaked == null || leaked.Count == 0) return;

        _leakingTestCount++;
        _reportLines.Add($"{testFullName}: {string.Join(", ", leaked)}");
    }

    private static string BareTestName(string testFullName)
    {
        int paren = testFullName.IndexOf('(');
        return paren < 0 ? testFullName : testFullName.Substring(0, paren);
    }

    private static bool MatchesObjectName(string pattern, string objectName) =>
        pattern.EndsWith("*", StringComparison.Ordinal)
            ? objectName.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.Ordinal)
            : objectName == pattern;

    /// <summary>Утечка прощена, только если известны И тест, И конкретное имя объекта
    /// (или его префикс — см. <see cref="KnownLeakers"/>), либо тест целиком в
    /// <see cref="KnownWholeSceneLeakers"/>. Раньше проверялся только тест, и запись одной
    /// известной утечки прощала любую другую из того же теста.</summary>
    public static bool IsKnown(string testFullName, string objectName)
    {
        string bareName = BareTestName(testFullName);

        foreach (var (known, _) in KnownWholeSceneLeakers)
            if (known == testFullName || known == bareName) return true;

        foreach (var (known, pattern, _) in KnownLeakers)
        {
            if (known != testFullName && known != bareName) continue;
            if (MatchesObjectName(pattern, objectName)) return true;
        }
        return false;
    }

    /// <summary>Обнаружена ли эта строка отчёта хоть одним известным объектом — используется
    /// сторожем устаревания (<c>SceneLeakGuardReportTests</c>), чтобы отличить запись, которую
    /// прошлый прогон подтвердил, от той, что там больше не встречается.</summary>
    public static bool AnyLeakedObjectMatches(IEnumerable<string> reportLines, string testName, string objectPattern)
    {
        foreach (var line in reportLines)
        {
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            string lineTestName = line.Substring(0, colon);
            if (lineTestName != testName && BareTestName(lineTestName) != testName) continue;

            foreach (var raw in line.Substring(colon + 1).Split(','))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;
                if (objectPattern == "*" || MatchesObjectName(objectPattern, name)) return true;
            }
        }
        return false;
    }

    private static bool IsFilteredRun() =>
        Array.IndexOf(Environment.GetCommandLineArgs(), "-testFilter") >= 0;

    /// <summary>Вызывается из <see cref="SceneLeakGuardFixture"/> ОДИН раз, в самом
    /// конце прогона: пишет полный список утечек ЭТОГО прогона в файл, и
    /// сбрасывает счётчики. Ничего не роняет — судит отдельный тест, который читает
    /// файл (см. класс).
    ///
    /// Прицельный (-testFilter) прогон отчёт НЕ трогает (review-perf-tests-tooling.md #2):
    /// иначе обычный рабочий цикл агента (`unity.ps1 tests -Filter Foo`) стирал отчёт
    /// последнего ПОЛНОГО прогона данными одного класса, и вердикт судил уже не тот прогон,
    /// который был утечкой заражён.</summary>
    public static void FlushReport()
    {
        if (IsFilteredRun())
        {
            _reportLines.Clear();
            _leakingTestCount = 0;
            return;
        }

        string path = Path.Combine(Application.dataPath, "..", ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var text = new StringBuilder();
        text.AppendLine($"SceneLeakGuard: {_leakingTestCount} leaking test(s), "
            + $"KnownLeakers={KnownLeakers.Length}, KnownWholeSceneLeakers={KnownWholeSceneLeakers.Length}");
        foreach (var line in _reportLines) text.AppendLine(line);
        File.WriteAllText(path, text.ToString());

        _reportLines.Clear();
        _leakingTestCount = 0;
    }
}

[SetUpFixture]
public sealed class SceneLeakGuardFixture
{
    [OneTimeTearDown]
    public void FlushSceneLeakReport() => SceneLeakGuard.FlushReport();
}

[InitializeOnLoad]
internal static class SceneLeakGuardWiring
{
    static SceneLeakGuardWiring()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
        }

        public void TestStarted(ITestAdaptor test)
        {
            if (!test.IsSuite) SceneLeakGuard.SnapshotBefore();
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.Test.IsSuite) SceneLeakGuard.CheckAfter(result.Test.FullName);
        }
    }
}
