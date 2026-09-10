using System.Collections.Generic;
using KitchenDesigner.Core.Plumbing;
using NUnit.Framework;

/// <summary>Схема концов фитинга обязана иметь СТОЛЬКО позиций, сколько у него портов, и
/// каждая позиция обязана указывать в ту же сторону, что и нога фитинга
/// (<c>PipeFittingSpec.Legs</c>) — а не в сторону, подобранную по виду фитинга вручную.
/// Если бы форма определялась списком из шести случаев, седьмой вид (фитинг воздуховода из
/// части 3) молча остался бы без своей формы; здесь форма читается из тех же данных, что и
/// сама посадка, поэтому новый вид получает её бесплатно.</summary>
public class PipeFittingDiagramLayoutTests
{
    private static IReadOnlyList<PipeAxis> LegsOf(PipeNodeKind kind) => PipeFittingSpec.Legs(kind);

    [Test]
    public void ElbowsTwoSlots_PointTheSameWayAsItsTwoLegs()
    {
        var slots = PipeFittingDiagramLayout.SlotsFor(PipeNodeKind.Elbow);
        var legs = LegsOf(PipeNodeKind.Elbow);

        Assert.AreEqual(2, slots.Count, "у отвода две ноги — схема обязана дать два порта");
        for (int i = 0; i < legs.Count; i++)
        {
            Assert.AreEqual(legs[i].X, slots[i].DirX, "порт " + i + " смотрит не туда, куда нога");
            Assert.AreEqual(legs[i].Y, slots[i].DirY, "порт " + i + " смотрит не туда, куда нога");
        }
    }

    [Test]
    public void TeesThreeSlots_MatchItsThreeLegs()
    {
        var slots = PipeFittingDiagramLayout.SlotsFor(PipeNodeKind.Tee);
        var legs = LegsOf(PipeNodeKind.Tee);

        Assert.AreEqual(3, slots.Count, "у тройника три ноги — схема обязана дать три порта");
        for (int i = 0; i < legs.Count; i++)
        {
            Assert.AreEqual(legs[i].X, slots[i].DirX);
            Assert.AreEqual(legs[i].Y, slots[i].DirY);
        }
    }

    [Test]
    public void CapSupplyAndReturn_EachGetExactlyOneSlot()
    {
        Assert.AreEqual(1, PipeFittingDiagramLayout.SlotsFor(PipeNodeKind.Cap).Count);
        Assert.AreEqual(1, PipeFittingDiagramLayout.SlotsFor(PipeNodeKind.Supply).Count);
        Assert.AreEqual(1, PipeFittingDiagramLayout.SlotsFor(PipeNodeKind.Return).Count);
    }

    [Test]
    public void CouplingsTwoSlots_PointOppositeWays()
    {
        var slots = PipeFittingDiagramLayout.SlotsFor(PipeNodeKind.Coupling);

        Assert.AreEqual(2, slots.Count);
        Assert.AreEqual(-slots[0].DirX, slots[1].DirX);
        Assert.AreEqual(-slots[0].DirY, slots[1].DirY);
    }

    [Test]
    public void MaxPortCount_IsTheWidestFittingsPortCount_NotAFixedNumber()
    {
        int expected = 0;
        foreach (var kind in PipeFittingNames.Kinds)
        {
            int count = PipeFittingSpec.PortCount(kind);
            if (count > expected) expected = count;
        }

        Assert.AreEqual(expected, PipeFittingDiagramLayout.MaxPortCount(),
            "максимум обязан читаться из PipeFittingSpec.PortCount по всем видам, а не быть "
            + "вписанной тройкой: седьмой вид с бОльшим числом портов обязан её раздвинуть");
    }
}
