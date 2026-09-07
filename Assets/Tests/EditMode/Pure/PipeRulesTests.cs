using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Правила замыкания и пересечений трассы.
///
/// PIP-01 — открытый конец трубы. Это ошибка, а не предупреждение: незакрытый
/// торец — вода на полу, деталь ОТКАЖЕТ. PIP-02 — стыковка разных диаметров без
/// переходника: такой узел не собрать. PIP-03 — трасса сквозь деталь; сквозь
/// стену и пол трубу как раз и ведут, поэтому препятствием считается только то,
/// что мешает физически.
///
/// У каждого правила здесь есть парный отрицательный случай: правило, которое
/// умеет только срабатывать, зелёного состояния не описывает.</summary>
public class PipeRulesTests
{
    private static readonly PointMm Start = new PointMm(0f, 0f, 0f);
    private static readonly PointMm Joint = new PointMm(1000f, 0f, 0f);
    private static readonly PointMm JointOut = new PointMm(1050f, 0f, 0f);
    private static readonly PointMm End = new PointMm(2000f, 0f, 0f);
    private static readonly PointMm Top = new PointMm(1000f, 800f, 0f);

    private static List<PipeFinding> WithCode(IReadOnlyList<PipeFinding> findings, string code)
    {
        var picked = new List<PipeFinding>();
        foreach (var finding in findings)
            if (finding.Code == code) picked.Add(finding);
        return picked;
    }

    [Test]
    public void PipeRules_OpenEnd_IsReportedForEveryFreePipeEnd()
    {
        var scene = new PipeTestScene().Pipe("p", Start, Joint, PipeSpec.Dn20);
        var open = WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeOpenEnd);

        Assert.AreEqual(2, open.Count, "у одиноко стоящей трубы открыты оба торца");
        Assert.AreEqual(PipeFindingLevel.Error, open[0].Level,
            "открытый конец — вода на полу, это отказ, а не замечание");
        Assert.AreEqual("p", open[0].ElementId);
    }

    [Test]
    public void PipeRules_OpenEnd_IsSilent_WhenBothEndsAreCapped()
    {
        var scene = new PipeTestScene()
            .Pipe("p", Start, Joint, PipeSpec.Dn20)
            .Fitting("c1", PipeNodeKind.Cap, (Start, PipeAxis.Right))
            .Fitting("c2", PipeNodeKind.Cap, (Joint, PipeAxis.Left));

        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeOpenEnd),
            "заглушка закрывает торец — это законный конец трассы");
    }

    [Test]
    public void PipeRules_OpenEnd_IsSilent_WhenThePipeReachesSupplyAndReturn()
    {
        var scene = new PipeTestScene()
            .Pipe("p", Start, Joint, PipeSpec.Dn20)
            .Fitting("supply", PipeNodeKind.Supply, (Start, PipeAxis.Right))
            .Fitting("return", PipeNodeKind.Return, (Joint, PipeAxis.Left));

        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeOpenEnd));
    }

    [Test]
    public void PipeRules_SizeMismatch_IsReported_WhenTwoPipesMeetDirectly()
    {
        var scene = new PipeTestScene()
            .Pipe("thin", Start, Joint, PipeSpec.Dn20)
            .Pipe("thick", Joint, End, PipeSpec.Dn25);

        var mismatch = WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeSizeMismatch);
        Assert.AreEqual(1, mismatch.Count, "3/4 дюйма в 1 дюйм напрямую не садится — нужен переходник");
        Assert.AreEqual(PipeFindingLevel.Error, mismatch[0].Level);
        Assert.AreEqual("thick", mismatch[0].OtherElementId,
            "замечание называет обе трубы, иначе непонятно, где искать");
    }

    [Test]
    public void PipeRules_SizeMismatch_IsSilent_WhenTwoPipesOfOneSizeMeet()
    {
        var scene = new PipeTestScene()
            .Pipe("a", Start, Joint, PipeSpec.Dn20)
            .Pipe("b", Joint, End, PipeSpec.Dn20);

        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeSizeMismatch));
    }

    [Test]
    public void PipeRules_SizeMismatch_IsReported_WhenAnElbowJoinsTwoDiameters()
    {
        var scene = new PipeTestScene()
            .Pipe("thin", Start, Joint, PipeSpec.Dn20)
            .Pipe("thick", Joint, Top, PipeSpec.Dn25)
            .Fitting("elbow", PipeNodeKind.Elbow, (Joint, PipeAxis.Left), (Joint, PipeAxis.Up));

        var mismatch = WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeSizeMismatch);
        Assert.AreEqual(1, mismatch.Count);
        Assert.AreEqual("elbow", mismatch[0].ElementId,
            "отвод бывает только одного диаметра — свести на нём два размера нельзя");
    }

    /// <summary>Обе стороны одного правила в одном тесте: те же две трубы, тот
    /// же стык. Без переходной муфты PIP-02 обязан сработать, с ней — молчать.
    /// Порознь эти утверждения ничего не стоят: правило, которое всегда молчит,
    /// пройдёт вторую половину, а правило, которое всегда ругается, — первую.</summary>
    [Test]
    public void PipeRules_SizeMismatch_FiresWithoutATransitionCoupling_AndIsSilentWithOne()
    {
        var direct = new PipeTestScene()
            .Pipe("thin", Start, Joint, PipeSpec.Dn20)
            .Pipe("thick", Joint, End, PipeSpec.Dn25);

        Assert.AreEqual(1, WithCode(PipeRules.Collect(direct),
                PipeIssueCatalog.CodeSizeMismatch).Count,
            "3/4\" и 1\" сведены напрямую — переходника между ними нет, и это отказ");

        var through = new PipeTestScene()
            .Pipe("thin", Start, Joint, PipeSpec.Dn20)
            .Pipe("thick", JointOut, End, PipeSpec.Dn25)
            .Fitting("sleeve", PipeNodeKind.Coupling,
                (Joint, PipeAxis.Left), (JointOut, PipeAxis.Right));

        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(through), PipeIssueCatalog.CodeSizeMismatch),
            "переходная муфта для того и стоит: два диаметра на ней законны, "
            + "и в её свойствах пишут оба");
    }

    [Test]
    public void PipeRules_ObstacleCrossed_IsReported_ForAPartOnTheRoute()
    {
        var scene = new PipeTestScene()
            .Pipe("p", Start, End, PipeSpec.Dn20)
            .Obstacle("carcass", PipeObstacleKind.Part,
                PipeTestScene.At(900f, -100f, -100f), PipeTestScene.At(1100f, 100f, 100f));

        var crossed = WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeObstacleCrossed);
        Assert.AreEqual(1, crossed.Count);
        Assert.AreEqual(PipeFindingLevel.Error, crossed[0].Level);
        Assert.AreEqual("carcass", crossed[0].OtherElementId);
    }

    [Test]
    public void PipeRules_ObstacleCrossed_IsSilent_InsideAWallOrAFloor()
    {
        foreach (var kind in new[] { PipeObstacleKind.Wall, PipeObstacleKind.Floor })
        {
            var scene = new PipeTestScene()
                .Pipe("p", Start, End, PipeSpec.Dn20)
                .Obstacle("host", kind,
                    PipeTestScene.At(900f, -100f, -100f), PipeTestScene.At(1100f, 100f, 100f));

            CollectionAssert.IsEmpty(
                WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeObstacleCrossed),
                kind + ": в стене и в полу трубу как раз и прокладывают");
        }
    }

    [Test]
    public void PipeRules_ObstacleCrossed_IsSilent_WhenThePartStandsAside()
    {
        var scene = new PipeTestScene()
            .Pipe("p", Start, End, PipeSpec.Dn20)
            .Obstacle("carcass", PipeObstacleKind.Part,
                PipeTestScene.At(900f, 500f, -100f), PipeTestScene.At(1100f, 700f, 100f));

        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeObstacleCrossed));
    }

    [Test]
    public void PipeRules_ObstacleCrossed_IsSilent_WhenThePartOnlyTouchesTheSurface()
    {
        float radiusMm = PipeSpec.Get(PipeSpec.Dn20).OuterDiameterMm * 0.5f;
        var scene = new PipeTestScene()
            .Pipe("p", Start, End, PipeSpec.Dn20)
            .Obstacle("carcass", PipeObstacleKind.Part,
                PipeTestScene.At(900f, radiusMm, -100f), PipeTestScene.At(1100f, 500f, 100f));

        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeObstacleCrossed),
            "деталь, лежащая на трубе, её не пересекает");
    }

    [Test]
    public void PipeRules_BlocksRouting_SeparatesPartsFromStructure()
    {
        Assert.IsTrue(PipeRules.BlocksRouting(PipeObstacleKind.Part));
        Assert.IsTrue(PipeRules.BlocksRouting(PipeObstacleKind.Furniture));
        Assert.IsFalse(PipeRules.BlocksRouting(PipeObstacleKind.Wall));
        Assert.IsFalse(PipeRules.BlocksRouting(PipeObstacleKind.Floor));
    }

    [Test]
    public void PipeRules_Collect_ReportsRulesInCodeOrder()
    {
        var scene = new PipeTestScene()
            .Pipe("thin", Start, Joint, PipeSpec.Dn20)
            .Pipe("thick", Joint, End, PipeSpec.Dn25)
            .Obstacle("carcass", PipeObstacleKind.Part,
                PipeTestScene.At(400f, -100f, -100f), PipeTestScene.At(600f, 100f, 100f));

        var codes = new List<string>();
        foreach (var finding in PipeRules.Collect(scene))
            if (codes.Count == 0 || codes[codes.Count - 1] != finding.Code) codes.Add(finding.Code);

        CollectionAssert.AreEqual(
            new[]
            {
                PipeIssueCatalog.CodeOpenEnd,
                PipeIssueCatalog.CodeSizeMismatch,
                PipeIssueCatalog.CodeObstacleCrossed,
            },
            codes,
            "порядок замечаний — часть контракта: клиент читает их подряд");
    }
}
