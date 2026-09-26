using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Вердикт по ПРОШЛОМУ ПОЛНОМУ (без -testFilter) прогону. SceneLeakGuard пишет
/// test-results/scene-leak-report.txt на каждом ТАКОМ прогоне (см. SceneLeakGuard.cs — прицельный
/// прогон файл не трогает), а падение внутри его собственного [OneTimeTearDown] не входит в
/// Total/Failed этой сборки NUnit — проверено живьём: Assert.Fail там честно бросается и не
/// двигает счёт ни на единицу. Поэтому решение — обычный тест-метод: его собственный провал
/// считается всегда, без исключений.
///
/// Задержка в один (полный) прогон — не изъян, а сама форма задачи: «сначала прогнать в
/// отчётном режиме и посчитать утечки, потом включить проверку» — это ДВА прогона
/// по определению. Файл от прошлого полного прогона и есть тот самый отчёт; прицельный
/// прогон между ними больше не стирает его чужими частичными данными.</summary>
public class SceneLeakGuardReportTests
{
    [Test]
    public void PreviousRun_LeavesNoUnknownSceneLeaks_InTheReportFile()
    {
        var reportLines = ReadReportLinesOrInconclusive();
        if (reportLines == null) return;

        var unknown = new List<string>();
        foreach (var line in reportLines)
        {
            int colon = line.IndexOf(':');
            if (colon < 0) { unknown.Add(line); continue; }
            string testName = line.Substring(0, colon);
            foreach (var raw in line.Substring(colon + 1).Split(','))
            {
                var objectName = raw.Trim();
                if (objectName.Length == 0) continue;
                if (!SceneLeakGuard.IsKnown(testName, objectName))
                    unknown.Add($"{testName}: {objectName}");
            }
        }

        Assert.IsEmpty(unknown,
            $"{unknown.Count} утёкший(их) объект(ов) из ПРОШЛОГО полного прогона EditMode не "
            + "опознаны ни SceneLeakGuard.KnownLeakers, ни KnownWholeSceneLeakers "
            + "(test-results/scene-leak-report.txt):\n"
            + string.Join("\n", unknown)
            + "\nДобавь утёкшее в _spawned/TearDown своего теста, или впиши (тест, имя объекта) "
            + "в SceneLeakGuard.KnownLeakers с причиной, если убрать утечку — не твоя правка");
    }

    /// <summary>Список долга обязан УБЫВАТЬ — тот же принцип, что у
    /// <c>HintCoverageGuardTests.EveryKnownGap_IsStillAGap</c>: запись, чья утечка уже
    /// починена, здесь больше не встречается, и не убрать её из списка — значит спрятать
    /// следующую поломку того же места за чужим оправданием.</summary>
    [Test]
    public void EveryKnownLeaker_IsStillPresent_InTheReportFile()
    {
        var reportLines = ReadReportLinesOrInconclusive();
        if (reportLines == null) return;

        var stale = new List<string>();
        foreach (var (testName, objectPattern, _) in SceneLeakGuard.KnownLeakers)
            if (!SceneLeakGuard.AnyLeakedObjectMatches(reportLines, testName, objectPattern))
                stale.Add($"{testName}: {objectPattern}");

        foreach (var (testName, _) in SceneLeakGuard.KnownWholeSceneLeakers)
            if (!SceneLeakGuard.AnyLeakedObjectMatches(reportLines, testName, "*"))
                stale.Add($"{testName}: (любой объект — KnownWholeSceneLeakers)");

        Assert.IsEmpty(stale,
            "запись KnownLeakers/KnownWholeSceneLeakers больше не встречается в отчёте "
            + "ПРОШЛОГО полного прогона — либо утечку почистили (вычеркните запись), либо "
            + "тест/объект переименовали (тогда запись мертва). Устарели:\n"
            + string.Join("\n", stale));
    }

    private static List<string>? ReadReportLinesOrInconclusive()
    {
        string path = Path.Combine(Application.dataPath, "..", SceneLeakGuard.ReportPath);
        if (!File.Exists(path))
        {
            Assert.Inconclusive("test-results/scene-leak-report.txt ещё нет — ни один " +
                "полный прогон EditMode здесь не завершался целиком");
            return null;
        }

        return File.ReadAllLines(path).Skip(1).Where(l => l.Length > 0).ToList();
    }
}
