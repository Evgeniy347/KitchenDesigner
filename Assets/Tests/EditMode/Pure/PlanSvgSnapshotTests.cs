using System;
using System.IO;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Tests.Geometry;

/// <summary>Эталон картинки — SVG: текст, который можно прочитать глазами и сравнить построчно, в отличие от PNG.
/// Эталон принимает человек: при расхождении рядом ложится *.candidate.svg.txt, и принять его — значит переименовать
/// поверх *.verified.svg.txt после просмотра (agents/TEST-DESIGN.md → «Snapshot baselines — accepting candidates»). Расширение .txt нарочно: настоящий .svg Unity импортирует как спрайт.
/// Кухня несимметрична: окно не посередине стены, шкаф-нарушитель не крайний, дверь у одного края.</summary>
public class PlanSvgSnapshotTests
{
    private static string Dir => RepoPaths.Subdir("Assets", "Tests", "EditMode", "Pure", "PlanSnapshots");

    [Test]
    public void KitchenTop_MatchesTheVerifiedSvg() =>
        Match(PlanRender.Svg(PlanFixtures.Kitchen(), PlanView.Top, true, 512), "plan_kitchen_top");

    [Test]
    public void KitchenFront_MatchesTheVerifiedSvg() =>
        Match(PlanRender.Svg(PlanFixtures.Kitchen(), PlanView.Front, true, 512), "plan_kitchen_front");

    [Test]
    public void KitchenTopWithoutLabels_MatchesTheVerifiedSvg() =>
        Match(PlanRender.Svg(PlanFixtures.Kitchen(), PlanView.Top, false, 512), "plan_kitchen_top_bare");

    private static void Match(string actual, string name)
    {
        var verified = Path.Combine(Dir, name + ".verified.svg.txt");
        var candidate = Path.Combine(Dir, name + ".candidate.svg.txt");
        if (!File.Exists(verified))
        {
            File.WriteAllText(candidate, actual);
            Assert.Fail("нет эталона " + name + ".verified.svg; кандидат: " + candidate);
        }

        var expected = File.ReadAllText(verified).Replace("\r\n", "\n");
        if (expected == actual)
        {
            if (File.Exists(candidate)) File.Delete(candidate);
            return;
        }

        File.WriteAllText(candidate, actual);
        Assert.Fail(name + ": " + FirstDifference(expected, actual) + "\nкандидат: " + candidate);
    }

    private static string FirstDifference(string expected, string actual)
    {
        var a = expected.Split('\n');
        var b = actual.Split('\n');
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<конец>";
            var right = i < b.Length ? b[i] : "<конец>";
            if (left != right) return "строка " + (i + 1) + ":\n  было:  " + left + "\n  стало: " + right;
        }
        return "совпадают";
    }
}
