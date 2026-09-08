using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Что происходит со связями, когда один вид узла трассы меняют на другой.
///
/// Правило ОДНО, и оно не таблица 7×7: связь переживает замену тогда и только тогда,
/// когда у нового вида есть свободный порт, который стыкуется с ТЕМ ЖЕ портом соседа
/// по обычному правилу стыка — то же место с точностью до Tolerance.ContactMm и
/// встречная ось. Остальные связи рвутся. Ничего не удаляется и никто не двигается:
/// план состоит из индексов портов и не умеет ни переносить соседа, ни стирать ветку.
///
/// Перечень пар протух бы на восьмом виде, а механизм — нет: он спрашивает у
/// PipeJoint.Connects, то есть у того же и единственного описания стыка, которым
/// пользуется PipeNetwork. Второго описания портов здесь тоже нет: число и оси портов
/// живут в PipeNodePorts/PipeFittingSpec, а стенд только расставляет их в мире.
///
/// Куда сажают новый вид — решает вызывающая команда, а не это правило: при заданной
/// расстановке ответ детерминирован. Когда портов у нового вида меньше и выбирать
/// приходится, правило выбора отдельное и называется AnchorPortIndex — «выживает
/// связь на МЛАДШЕМ занятом порту старого вида».
///
/// Отдельно про ловушку: у каждого вида своя посадка (PipeFittingSpec.HubOffsetMm),
/// поэтому «оставить элемент в прежнем начале координат» не сохраняет НИ ОДНОЙ связи
/// даже при замене муфты на тройник. Это пинает последний тест файла.</summary>
public class PipeKindSwapTests
{
    private const float Leg = 100f;
    private const string Swap = "swap";

    private static readonly PointMm Hub = new PointMm(1200f, 700f, 300f);

    private static PointMm At(float dxMm, float dyMm, float dzMm) =>
        new PointMm(Hub.XMm + dxMm, Hub.YMm + dyMm, Hub.ZMm + dzMm);

    private static PipePort Port(string id, PipeNodeKind kind, int index, in PointMm at,
        in PipeAxis outward, string? sizeId = null) =>
        new PipePort(id, kind, index, at, outward, sizeId);

    private static PipePort Neighbour(string id, in PointMm at, in PipeAxis outward) =>
        Port(id, PipeNodeKind.Pipe, 0, at, outward, PipeSpec.Dn20);

    private static PipePort?[] CouplingNeighbours() => new PipePort?[]
    {
        Neighbour("below", At(0f, -Leg, 0f), PipeAxis.Up),
        Neighbour("above", At(0f, Leg, 0f), PipeAxis.Down),
    };

    private static PipePort[] CapAtTheTop() => new[]
    {
        Port(Swap, PipeNodeKind.Cap, 0, At(0f, Leg, 0f), PipeAxis.Up),
    };

    private static PipePort[] CapAtTheBottom() => new[]
    {
        Port(Swap, PipeNodeKind.Cap, 0, At(0f, -Leg, 0f), PipeAxis.Down),
    };

    private static PipePort[] TeeOnTheSameAxis() => new[]
    {
        Port(Swap, PipeNodeKind.Tee, 0, At(0f, -Leg, 0f), PipeAxis.Down),
        Port(Swap, PipeNodeKind.Tee, 1, At(0f, Leg, 0f), PipeAxis.Up),
        Port(Swap, PipeNodeKind.Tee, 2, At(Leg, 0f, 0f), PipeAxis.Right),
    };

    private static PipePort[] ElbowOnTheLowerLeg() => new[]
    {
        Port(Swap, PipeNodeKind.Elbow, 0, At(0f, -Leg, 0f), PipeAxis.Down),
        Port(Swap, PipeNodeKind.Elbow, 1, At(Leg, 0f, 0f), PipeAxis.Right),
    };

    private static int[] OldPortsOf(PipeSwapPlan plan)
    {
        var kept = new int[plan.Kept.Count];
        for (int i = 0; i < kept.Length; i++) kept[i] = plan.Kept[i].OldPortIndex;
        return kept;
    }

    private static IReadOnlyList<PipeFinding> Findings(IReadOnlyList<PipePort> ports) =>
        PipeRules.Collect(PipeSurvey.Of(ports), new PipeRunSegment[0], new PipeObstacle[0]);

    private static int OpenEndsOf(IReadOnlyList<PipePort> ports, string elementId)
    {
        int count = 0;
        foreach (var finding in Findings(ports))
            if (finding.Code == PipeIssueCatalog.CodeOpenEnd && finding.ElementId == elementId)
                count++;
        return count;
    }

    [Test]
    public void PipeKindSwap_CouplingToCoupling_KeepsBothLinks_AndLeavesNoPortFree()
    {
        var plan = PipeKindSwap.Plan(CouplingNeighbours(), new[]
        {
            Port(Swap, PipeNodeKind.Coupling, 0, At(0f, -Leg, 0f), PipeAxis.Down),
            Port(Swap, PipeNodeKind.Coupling, 1, At(0f, Leg, 0f), PipeAxis.Up),
        });

        CollectionAssert.AreEqual(new[] { 0, 1 }, OldPortsOf(plan),
            "новый вид воспроизвёл оба порта — рвать нечего");
        CollectionAssert.IsEmpty(plan.BrokenPorts);
        CollectionAssert.IsEmpty(plan.FreeNewPorts);
    }

    [Test]
    public void PipeKindSwap_CouplingToCap_KeepsTheLinkTheCapReproduces_AndSeversTheOther()
    {
        var plan = PipeKindSwap.Plan(CouplingNeighbours(), CapAtTheTop());

        Assert.AreEqual(0, plan.NewPortFor(1),
            "единственный порт заглушки посажен на верхнюю связь — она и выживает");
        CollectionAssert.AreEqual(new[] { 0 }, plan.BrokenPorts,
            "у заглушки один порт: вторая связь рвётся, но нижний сосед не удаляется");
        CollectionAssert.IsEmpty(plan.FreeNewPorts,
            "занятый порт заглушки не открытый конец и PIP-01 не даёт");
    }

    [Test]
    public void PipeKindSwap_CouplingToCap_AnchoredOnTheOtherEnd_SeversTheOppositeLink()
    {
        var plan = PipeKindSwap.Plan(CouplingNeighbours(), CapAtTheBottom());

        Assert.AreEqual(0, plan.NewPortFor(0));
        Assert.IsFalse(plan.Keeps(1),
            "та же пара видов, другая посадка — выживает ДРУГАЯ связь; выбор делает не правило, а команда");
        CollectionAssert.AreEqual(new[] { 1 }, plan.BrokenPorts);
    }

    [Test]
    public void PipeKindSwap_AnchorPortIndex_IsTheLowestConnectedPortOfTheOldKind()
    {
        Assert.AreEqual(0, PipeKindSwap.AnchorPortIndex(CouplingNeighbours()),
            "когда портов у нового вида меньше, выживает связь на МЛАДШЕМ занятом порту");

        var onlyUpper = new PipePort?[] { null, Neighbour("above", At(0f, Leg, 0f), PipeAxis.Down) };
        Assert.AreEqual(1, PipeKindSwap.AnchorPortIndex(onlyUpper),
            "младший ЗАНЯТЫЙ, а не младший вообще: свободный порт сажать не на что");

        Assert.AreEqual(PipeKindSwap.NoPort,
            PipeKindSwap.AnchorPortIndex(new PipePort?[] { null, null }),
            "сажать не на что — команда ставит новый вид туда, где стоял старый");
    }

    [Test]
    public void PipeKindSwap_CouplingToTee_KeepsBothLinks_AndLeavesTheSideBranchFree()
    {
        var plan = PipeKindSwap.Plan(CouplingNeighbours(), TeeOnTheSameAxis());

        CollectionAssert.AreEqual(new[] { 0, 1 }, OldPortsOf(plan),
            "соосная пара тройника воспроизводит оба порта муфты — у тройника остаётся две связи");
        CollectionAssert.IsEmpty(plan.BrokenPorts,
            "портов стало БОЛЬШЕ — рвать нечего");
        CollectionAssert.AreEqual(new[] { 2 }, plan.FreeNewPorts,
            "боковой порт тройника свободен: это и есть новый открытый конец");
    }

    [Test]
    public void PipeRules_AfterCouplingToTee_ReportsOneOpenEndOnTheSideBranchOfTheTee()
    {
        var after = new List<PipePort>(TeeOnTheSameAxis())
        {
            Neighbour("below", At(0f, -Leg, 0f), PipeAxis.Up),
            Neighbour("above", At(0f, Leg, 0f), PipeAxis.Down),
        };

        Assert.AreEqual(1, OpenEndsOf(after, Swap),
            "свободный боковой порт тройника обязан выйти PIP-01 — иначе замена молча оставит дыру");
        Assert.AreEqual(0, OpenEndsOf(after, "above"),
            "верхняя связь пережила замену, и открытым концом сосед быть не может");
    }

    [Test]
    public void PipeKindSwap_CouplingToElbow_KeepsTheSharedLeg_AndSeversTheOneThatTurned()
    {
        var plan = PipeKindSwap.Plan(CouplingNeighbours(), ElbowOnTheLowerLeg());

        Assert.AreEqual(0, plan.NewPortFor(0));
        Assert.IsFalse(plan.Keeps(1),
            "портов по-прежнему два, но второй у отвода смотрит вбок: сохранить связь можно было бы "
            + "только сдвинув соседа, а соседей замена не двигает");
        CollectionAssert.AreEqual(new[] { 1 }, plan.BrokenPorts);
        CollectionAssert.AreEqual(new[] { 1 }, plan.FreeNewPorts);
    }

    [Test]
    public void PipeKindSwap_CouplingToElbow_LeavesTheBranchBehindTheSeveredLink_WholeAndInPlace()
    {
        var after = new List<PipePort>(ElbowOnTheLowerLeg())
        {
            Neighbour("below", At(0f, -Leg, 0f), PipeAxis.Up),
            Port("riser", PipeNodeKind.Pipe, 0, At(0f, Leg, 0f), PipeAxis.Down, PipeSpec.Dn20),
            Port("riser", PipeNodeKind.Pipe, 1, At(0f, Leg + 500f, 0f), PipeAxis.Up, PipeSpec.Dn20),
            Port("top", PipeNodeKind.Cap, 0, At(0f, Leg + 500f, 0f), PipeAxis.Down),
        };

        var network = PipeSurvey.Of(after).Network;

        Assert.AreEqual(1, OpenEndsOf(after, "riser"),
            "у ветки открылся ровно ОДИН конец — тот, где связь порвали; всё остальное цело");
        Assert.AreEqual(0, OpenEndsOf(after, "top"),
            "заглушка на дальнем конце ветки не тронута — ветку не удалили и не укоротили");
        Assert.IsFalse(network.IsFree(4),
            "стык трубы с дальней заглушкой уцелел, значит ветка осталась ТАМ ЖЕ: сдвинься она, "
            + "этот стык разошёлся бы вместе с порванным");
        Assert.AreEqual(1, OpenEndsOf(after, Swap),
            "свободным остался ровно боковой порт самого отвода");
    }

    [Test]
    public void PipeKindSwap_TeeToCoupling_KeepsTheAxialPair_AndSeversTheSideBranch()
    {
        var plan = PipeKindSwap.Plan(TeeNeighbours(), new[]
        {
            Port(Swap, PipeNodeKind.Coupling, 0, At(0f, -Leg, 0f), PipeAxis.Down),
            Port(Swap, PipeNodeKind.Coupling, 1, At(0f, Leg, 0f), PipeAxis.Up),
        });

        CollectionAssert.AreEqual(new[] { 0, 1 }, OldPortsOf(plan),
            "муфта соосна, поэтому воспроизводит соосную пару тройника");
        CollectionAssert.AreEqual(new[] { 2 }, plan.BrokenPorts,
            "боковой отвод тройника муфте воспроизвести нечем");
        CollectionAssert.IsEmpty(plan.FreeNewPorts);
    }

    [Test]
    public void PipeKindSwap_TeeToCoupling_TurnedOntoTheSideBranch_KeepsOnlyThatOne()
    {
        var plan = PipeKindSwap.Plan(TeeNeighbours(), new[]
        {
            Port(Swap, PipeNodeKind.Coupling, 0, At(-Leg, 0f, 0f), PipeAxis.Left),
            Port(Swap, PipeNodeKind.Coupling, 1, At(Leg, 0f, 0f), PipeAxis.Right),
        });

        CollectionAssert.AreEqual(new[] { 2 }, OldPortsOf(plan),
            "муфту развернули на боковой отвод — выживает он один");
        Assert.AreEqual(1, plan.NewPortFor(2),
            "номер порта у нового вида свой: связь переезжает с порта 2 на порт 1");
        CollectionAssert.AreEqual(new[] { 0, 1 }, plan.BrokenPorts,
            "соосная пара тройника при таком развороте не воспроизводится ни одним портом");
        CollectionAssert.AreEqual(new[] { 0 }, plan.FreeNewPorts);
    }

    [Test]
    public void PipeKindSwap_CapToSupply_KeepsTheLink_BecauseTheSinglePortIsReproduced()
    {
        var neighbours = new PipePort?[] { Neighbour("below", At(0f, Leg, 0f), PipeAxis.Down) };

        var plan = PipeKindSwap.Plan(neighbours, new[]
        {
            Port(Swap, PipeNodeKind.Supply, 0, At(0f, Leg, 0f), PipeAxis.Up),
        });

        Assert.AreEqual(0, plan.NewPortFor(0),
            "1 в 1 с тем же местом и той же осью: связь выживает, менять больше нечего");
        CollectionAssert.IsEmpty(plan.BrokenPorts);
    }

    [Test]
    public void PipeKindSwap_CapToReturn_SeversTheLink_WhenTheNewPortFacesTheOtherWay()
    {
        var neighbours = new PipePort?[] { Neighbour("below", At(0f, Leg, 0f), PipeAxis.Down) };

        var plan = PipeKindSwap.Plan(neighbours, new[]
        {
            Port(Swap, PipeNodeKind.Return, 0, At(0f, Leg, 0f), PipeAxis.Down),
        });

        CollectionAssert.IsEmpty(plan.Kept,
            "порт на месте, но смотрит В ТУ ЖЕ сторону, что и сосед: это наложение, а не стык");
        CollectionAssert.AreEqual(new[] { 0 }, plan.BrokenPorts);
        CollectionAssert.AreEqual(new[] { 0 }, plan.FreeNewPorts);
    }

    [Test]
    public void PipeKindSwap_ElbowToElbow_TurnedIntoAnotherPlane_KeepsOnlyTheLegThatStayed()
    {
        var plan = PipeKindSwap.Plan(ElbowNeighbours(), new[]
        {
            Port(Swap, PipeNodeKind.Elbow, 0, At(0f, -Leg, 0f), PipeAxis.Down),
            Port(Swap, PipeNodeKind.Elbow, 1, At(0f, 0f, -Leg), PipeAxis.Back),
        });

        CollectionAssert.AreEqual(new[] { 0 }, OldPortsOf(plan));
        CollectionAssert.AreEqual(new[] { 1 }, plan.BrokenPorts,
            "поворот отвода в другую плоскость уводит второй порт с места старого");
    }

    [Test]
    public void PipeKindSwap_ElbowToElbow_TurnedTheOtherWay_KeepsTheOtherLeg()
    {
        var plan = PipeKindSwap.Plan(ElbowNeighbours(), new[]
        {
            Port(Swap, PipeNodeKind.Elbow, 0, At(0f, Leg, 0f), PipeAxis.Up),
            Port(Swap, PipeNodeKind.Elbow, 1, At(Leg, 0f, 0f), PipeAxis.Right),
        });

        CollectionAssert.AreEqual(new[] { 1 }, OldPortsOf(plan),
            "тот же вид, тот же счёт портов, другой поворот — и выживает ПРОТИВОПОЛОЖНАЯ связь");
        CollectionAssert.AreEqual(new[] { 0 }, plan.BrokenPorts);
    }

    [Test]
    public void PipeKindSwap_EveryPortFree_BreaksNothing_AndOffersEveryNewPort()
    {
        var plan = PipeKindSwap.Plan(new PipePort?[] { null, null }, TeeOnTheSameAxis());

        CollectionAssert.IsEmpty(plan.Kept);
        CollectionAssert.IsEmpty(plan.BrokenPorts,
            "нечего было рвать: замена одиноко стоящего фитинга не падает и ничего не сообщает");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, plan.FreeNewPorts);
    }

    [Test]
    public void PipeKindSwap_OneNewPort_CannotSaveTwoLinksAtOnce()
    {
        var shared = At(0f, Leg, 0f);
        var neighbours = new PipePort?[]
        {
            Neighbour("first", shared, PipeAxis.Down),
            Neighbour("second", shared, PipeAxis.Down),
        };

        var plan = PipeKindSwap.Plan(neighbours, CapAtTheTop());

        CollectionAssert.AreEqual(new[] { 0 }, OldPortsOf(plan),
            "один порт — одна связь; при споре выигрывает МЛАДШИЙ порт старого вида");
        CollectionAssert.AreEqual(new[] { 1 }, plan.BrokenPorts,
            "иначе два соседа оказались бы записаны на один порт и трасса раздвоилась бы");
    }

    [Test]
    public void PipeKindSwap_SamePlace_IsTheProjectContactTolerance_AndNotWider()
    {
        var neighbours = new PipePort?[] { Neighbour("above", At(0f, Leg, 0f), PipeAxis.Down) };

        var inside = PipeKindSwap.Plan(neighbours, new[]
        {
            Port(Swap, PipeNodeKind.Cap, 0, At(0f, Leg + Tolerance.ContactMm * 0.8f, 0f), PipeAxis.Up),
        });
        var outside = PipeKindSwap.Plan(neighbours, new[]
        {
            Port(Swap, PipeNodeKind.Cap, 0, At(0f, Leg + Tolerance.ContactMm * 2f, 0f), PipeAxis.Up),
        });

        Assert.IsTrue(inside.Keeps(0),
            "«то же место» здесь не заведено заново — это тот же допуск стыка, что у PipeJoint");
        Assert.IsFalse(outside.Keeps(0),
            "за допуском порт уже не воспроизведён, и связь обязана порваться");
    }

    [Test]
    public void PipeKindSwap_KeepingTheElementOrigin_ReproducesNoPortOfAnyOtherKind()
    {
        float leg = PipeFittingSpec.LegLengthMm(PipeSpec.DEFAULT_SIZE);
        var neighbours = new PipePort?[]
        {
            Neighbour("below", At(0f, -leg, 0f), PipeAxis.Up),
            Neighbour("above", At(0f, leg, 0f), PipeAxis.Down),
        };

        foreach (var kind in PipeFittingNames.Kinds)
        {
            var ports = new List<PipePort>();
            for (int i = 0; i < PipeFittingSpec.PortCount(kind); i++)
            {
                var offset = PipeFittingSpec.PortOffsetMm(kind, PipeSpec.DEFAULT_SIZE, i);
                ports.Add(Port(Swap, kind, i, At(offset.XMm, offset.YMm, offset.ZMm),
                    PipeFittingSpec.PortAxis(kind, i)));
            }

            var plan = PipeKindSwap.Plan(neighbours, ports);
            int expected = kind == PipeNodeKind.Coupling ? 2 : 0;

            Assert.AreEqual(expected, plan.Kept.Count,
                kind + ": у каждого вида своя посадка (PipeFittingSpec.HubOffsetMm), поэтому "
                + "оставить элемент в прежнем начале координат — значит потерять ВСЕ связи, даже "
                + "при замене муфты на тройник. Команда замены обязана посадить новый вид на ту "
                + "связь, которую сохраняет (PipeKindSwap.AnchorPortIndex), и подать сюда уже "
                + "расставленные порты");
        }
    }

    private static PipePort?[] TeeNeighbours() => new PipePort?[]
    {
        Neighbour("below", At(0f, -Leg, 0f), PipeAxis.Up),
        Neighbour("above", At(0f, Leg, 0f), PipeAxis.Down),
        Neighbour("side", At(Leg, 0f, 0f), PipeAxis.Left),
    };

    private static PipePort?[] ElbowNeighbours() => new PipePort?[]
    {
        Neighbour("below", At(0f, -Leg, 0f), PipeAxis.Up),
        Neighbour("side", At(Leg, 0f, 0f), PipeAxis.Left),
    };
}
