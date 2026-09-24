using NUnit.Framework;
using KitchenDesigner.Core.Ports;

/// <summary>Именованные наборы «ноги по осям» — общий словарь раскладки фитинга, который
/// раньше жил только приватными массивами внутри PipeFittingSpec. Duct-фитинги (3.5)
/// переиспользуют его как есть; PipeFittingSpec пока не тронут — отдельная задача.</summary>
public class LegFrameTests
{
    [Test]
    public void None_HasNoLegs()
    {
        Assert.AreEqual(0, LegFrame.None.Count);
    }

    [Test]
    public void OneWay_HasASingleUpwardLeg()
    {
        Assert.AreEqual(1, LegFrame.OneWay.Count);
        Assert.AreEqual(PipeAxis.Up, LegFrame.OneWay.Axes[0]);
    }

    [Test]
    public void TwoWayStraight_GoesDownAndUp()
    {
        Assert.AreEqual(2, LegFrame.TwoWayStraight.Count);
        Assert.AreEqual(PipeAxis.Down, LegFrame.TwoWayStraight.Axes[0]);
        Assert.AreEqual(PipeAxis.Up, LegFrame.TwoWayStraight.Axes[1]);
    }

    [Test]
    public void TwoWayCorner_TurnsFromDownToRight()
    {
        Assert.AreEqual(2, LegFrame.TwoWayCorner.Count);
        Assert.AreEqual(PipeAxis.Down, LegFrame.TwoWayCorner.Axes[0]);
        Assert.AreEqual(PipeAxis.Right, LegFrame.TwoWayCorner.Axes[1]);
    }

    [Test]
    public void ThreeWay_BranchesDownUpAndRight()
    {
        Assert.AreEqual(3, LegFrame.ThreeWay.Count);
        Assert.AreEqual(PipeAxis.Down, LegFrame.ThreeWay.Axes[0]);
        Assert.AreEqual(PipeAxis.Up, LegFrame.ThreeWay.Axes[1]);
        Assert.AreEqual(PipeAxis.Right, LegFrame.ThreeWay.Axes[2]);
    }

    [Test]
    public void Indexer_AgreesWithAxes()
    {
        for (int i = 0; i < LegFrame.ThreeWay.Count; i++)
            Assert.AreEqual(LegFrame.ThreeWay.Axes[i], LegFrame.ThreeWay[i]);
    }
}
