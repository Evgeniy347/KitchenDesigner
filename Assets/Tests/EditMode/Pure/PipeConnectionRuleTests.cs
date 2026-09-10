using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Правило «что с чем соединяется» — ЕДИНСТВЕННОЕ его описание живёт в
/// <see cref="PipeConnectionRule"/>, а панель портов только спрашивает. Раньше список
/// «что подключено к порту» перечислял фитинги вручную, поэтому у отвода второй порт,
/// занятый ТРУБОЙ, показывался пустым: трубы в списке просто не было.</summary>
[TestFixture]
public class PipeConnectionRuleTests
{
    [Test]
    public void PipeToPipe_IsForbidden()
    {
        Assert.IsFalse(PipeConnectionRule.CanConnect(PipeNodeKind.Pipe, PipeNodeKind.Pipe),
            "труба в трубу не соединяется — стык двух труб делает муфта");
    }

    [Test]
    public void PipeToElbow_IsAllowed_BothWaysRound()
    {
        Assert.IsTrue(PipeConnectionRule.CanConnect(PipeNodeKind.Pipe, PipeNodeKind.Elbow));
        Assert.IsTrue(PipeConnectionRule.CanConnect(PipeNodeKind.Elbow, PipeNodeKind.Pipe));
    }

    [Test]
    public void ElbowToTee_IsAllowed()
    {
        Assert.IsTrue(PipeConnectionRule.CanConnect(PipeNodeKind.Elbow, PipeNodeKind.Tee));
    }

    [Test]
    public void EveryFittingPair_IsAllowed()
    {
        foreach (var a in PipeFittingNames.Kinds)
        foreach (var b in PipeFittingNames.Kinds)
            Assert.IsTrue(PipeConnectionRule.CanConnect(a, b), a + " + " + b);
    }

    [Test]
    public void APipeIsOfferedOnAFittingPort_ButNotOnAPipeEnd()
    {
        Assert.Contains(PipeNodeKind.Pipe,
            new List<PipeNodeKind>(PipeConnectionRule.ChoicesFor(PipeNodeKind.Elbow)),
            "к порту фитинга можно подключить трубу");

        CollectionAssert.DoesNotContain(PipeConnectionRule.ChoicesFor(PipeNodeKind.Pipe),
            PipeNodeKind.Pipe, "к концу трубы трубу подключить нельзя");
    }

    [Test]
    public void APipeEnd_OffersEveryFitting()
    {
        CollectionAssert.AreEquivalent(PipeFittingNames.Kinds,
            PipeConnectionRule.ChoicesFor(PipeNodeKind.Pipe));
    }

    [Test]
    public void EveryFittingKind_OffersTheSameChoices()
    {
        var reference = PipeConnectionRule.ChoicesForAnyFitting();
        foreach (var kind in PipeFittingNames.Kinds)
            CollectionAssert.AreEqual(reference, PipeConnectionRule.ChoicesFor(kind),
                "список у " + kind + " разошёлся с общим для фитингов");
    }

    [Test]
    public void OptionZeroIsNothing_AndTheRoundTripHolds()
    {
        var choices = PipeConnectionRule.ChoicesForAnyFitting();
        Assert.IsNull(PipeConnectionRule.KindAt(choices, PipeConnectionRule.NoChoice));
        Assert.AreEqual(PipeConnectionRule.NoChoice,
            PipeConnectionRule.OptionOf(choices, null));

        for (int i = 0; i < choices.Count; i++)
        {
            int option = PipeConnectionRule.OptionOf(choices, choices[i]);
            Assert.AreEqual(choices[i], PipeConnectionRule.KindAt(choices, option));
        }
    }

    [Test]
    public void AKindOutsideTheList_ReadsAsNothing()
    {
        var pipeEnd = PipeConnectionRule.ChoicesFor(PipeNodeKind.Pipe);
        Assert.AreEqual(PipeConnectionRule.NoChoice,
            PipeConnectionRule.OptionOf(pipeEnd, PipeNodeKind.Pipe));
        Assert.IsNull(PipeConnectionRule.KindAt(pipeEnd, pipeEnd.Count + 1));
    }
}
