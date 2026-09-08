using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>PIP-01 обобщено с «открытого конца трубы» на «несоединённый порт любого узла
/// трассы» — незакрытый порт подачи, обратки, заглушки, отвода, муфты или тройника это та
/// же самая вода на полу, что и открытый конец трубы. Число портов на узел берётся из
/// PipeNodePorts.CountOf, а не заводится здесь заново (STRUCTURE.md → «Element type checks
/// live in ONE place per layer»).
///
/// Каждый вид узла проверен на оба противоположных входа: все порты подведены — тихо, один
/// свободен — ровно одна находка, называющая именно этот порт. У тройника отдельно проверено
/// «два из трёх подведены», потому что у него, в отличие от прочих, есть промежуточный
/// случай между «всё открыто» и «всё закрыто».</summary>
public class PipeRulesOpenPortTests
{
    private static readonly PointMm[] PortPositions =
    {
        new PointMm(0f, 0f, 0f),
        new PointMm(1000f, 0f, 0f),
        new PointMm(0f, 1000f, 0f),
    };

    private static readonly PipeAxis[] PortAxes =
    {
        PipeAxis.Right,
        PipeAxis.Left,
        PipeAxis.Up,
    };

    private static List<PipeFinding> WithCode(IReadOnlyList<PipeFinding> findings, string code)
    {
        var picked = new List<PipeFinding>();
        foreach (var finding in findings)
            if (finding.Code == code) picked.Add(finding);
        return picked;
    }

    private static PipeTestScene BuildFitting(PipeNodeKind kind, int portCount)
    {
        var ports = new (PointMm at, PipeAxis outward)[portCount];
        for (int i = 0; i < portCount; i++)
            ports[i] = (PortPositions[i], PortAxes[i]);
        return new PipeTestScene().Fitting("f", kind, ports);
    }

    private static void ConnectPort(PipeTestScene scene, int index) =>
        scene.Fitting("stub" + index, PipeNodeKind.Cap,
            (PortPositions[index], PortAxes[index].Opposite));

    [TestCase(PipeNodeKind.Elbow)]
    [TestCase(PipeNodeKind.Coupling)]
    [TestCase(PipeNodeKind.Tee)]
    [TestCase(PipeNodeKind.Cap)]
    [TestCase(PipeNodeKind.Supply)]
    [TestCase(PipeNodeKind.Return)]
    public void PipeRules_OpenPort_IsSilent_WhenEveryPortOfTheNodeIsConnected(PipeNodeKind kind)
    {
        int count = PipeNodePorts.CountOf(kind);
        var scene = BuildFitting(kind, count);
        for (int i = 0; i < count; i++) ConnectPort(scene, i);

        CollectionAssert.IsEmpty(WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeOpenEnd),
            kind + ": все " + count + " порт(ов) подведены — открытых концов не осталось");
    }

    [TestCase(PipeNodeKind.Elbow)]
    [TestCase(PipeNodeKind.Coupling)]
    [TestCase(PipeNodeKind.Tee)]
    [TestCase(PipeNodeKind.Cap)]
    [TestCase(PipeNodeKind.Supply)]
    [TestCase(PipeNodeKind.Return)]
    public void PipeRules_OpenPort_ReportsExactlyOneFinding_NamingTheFreePort(PipeNodeKind kind)
    {
        int count = PipeNodePorts.CountOf(kind);
        int freeIndex = count - 1;
        var scene = BuildFitting(kind, count);
        for (int i = 0; i < freeIndex; i++) ConnectPort(scene, i);

        var open = WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeOpenEnd);

        Assert.AreEqual(1, open.Count,
            kind + ": один порт из " + count + " остался свободным — находка должна быть ровно одна");
        Assert.AreEqual("f", open[0].ElementId);
        StringAssert.Contains("порт " + freeIndex, open[0].Message,
            "сообщение обязано называть именно тот порт, который не соединён — иначе на "
            + "тройнике не понять, какой из трёх");
        StringAssert.Contains(PipeFittingNames.Title(kind), open[0].Message);
    }

    [Test]
    public void PipeRules_OpenPort_Tee_TwoOfThreeConnected_ReportsOnlyTheMiddlePort()
    {
        var scene = BuildFitting(PipeNodeKind.Tee, 3);
        ConnectPort(scene, 0);
        ConnectPort(scene, 2);

        var open = WithCode(PipeRules.Collect(scene), PipeIssueCatalog.CodeOpenEnd);

        Assert.AreEqual(1, open.Count,
            "у тройника подведены два патрубка из трёх — свободен ровно один");
        Assert.AreEqual("f", open[0].ElementId);
        StringAssert.Contains("порт 1", open[0].Message,
            "свободен средний по счёту порт — сообщение обязано назвать номер 1, а не любой из трёх");
    }

    [Test]
    public void PipeRules_OpenPort_VanishesTheMomentTheLastFreePortIsConnected()
    {
        int count = PipeNodePorts.CountOf(PipeNodeKind.Elbow);

        var justPlaced = BuildFitting(PipeNodeKind.Elbow, count);
        for (int i = 0; i < count - 1; i++) ConnectPort(justPlaced, i);
        Assert.AreEqual(1,
            WithCode(PipeRules.Collect(justPlaced), PipeIssueCatalog.CodeOpenEnd).Count,
            "только что поставленный отвод ещё не подключен последним патрубком — находка "
            + "обязана появиться, это и есть смысл правила");

        var fullyConnected = BuildFitting(PipeNodeKind.Elbow, count);
        for (int i = 0; i < count; i++) ConnectPort(fullyConnected, i);
        CollectionAssert.IsEmpty(
            WithCode(PipeRules.Collect(fullyConnected), PipeIssueCatalog.CodeOpenEnd),
            "как только подвели последний патрубок, находка обязана погаснуть — правило, "
            + "которое не гаснет, хуже отсутствующего");
    }
}
