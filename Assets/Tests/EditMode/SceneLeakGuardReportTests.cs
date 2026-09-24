using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>Вердикт по ПРОШЛОМУ прогону. SceneLeakGuard пишет
/// test-results/scene-leak-report.txt на каждом прогоне (см. SceneLeakGuard.cs), а
/// падение внутри его собственного [OneTimeTearDown] не входит в Total/Failed этой
/// сборки NUnit — проверено живьём: Assert.Fail там честно бросается и не двигает
/// счёт ни на единицу. Поэтому решение — обычный тест-метод: его собственный провал
/// считается всегда, без исключений.
///
/// Задержка в один прогон — не изъян, а сама форма задачи: «сначала прогнать в
/// отчётном режиме и посчитать утечки, потом включить проверку» — это ДВА прогона
/// по определению. Файл от прошлого прогона и есть тот самый отчёт.</summary>
public class SceneLeakGuardReportTests
{
    [Test]
    public void PreviousRun_LeavesNoUnknownSceneLeaks_InTheReportFile()
    {
        string path = Path.Combine(Application.dataPath, "..", SceneLeakGuard.ReportPath);
        if (!File.Exists(path))
        {
            Assert.Inconclusive("test-results/scene-leak-report.txt ещё нет — ни один " +
                "прогон EditMode здесь не завершался целиком");
            return;
        }

        var reportLines = File.ReadAllLines(path).Skip(1)
            .Where(l => l.Length > 0)
            .ToList();
        var unknown = reportLines.Where(l => !SceneLeakGuard.IsKnown(NameOf(l))).ToList();

        Assert.IsEmpty(unknown,
            $"{unknown.Count} тест(ов) из ПРОШЛОГО прогона EditMode оставили объекты в "
            + "общей сцене (test-results/scene-leak-report.txt):\n"
            + string.Join("\n", unknown)
            + "\nДобавь утёкшее в _spawned/TearDown своего теста, или впиши тест в "
            + "SceneLeakGuard.KnownLeakers с причиной, если убрать утечку — не твоя правка");
    }

    private static string NameOf(string reportLine)
    {
        int colon = reportLine.IndexOf(':');
        return colon < 0 ? reportLine : reportLine.Substring(0, colon);
    }
}
