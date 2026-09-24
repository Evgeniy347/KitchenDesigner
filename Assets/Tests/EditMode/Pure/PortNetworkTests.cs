using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;
using KitchenDesigner.Core.Ports;

/// <summary>Generic-алгоритм сети портов: кто с кем сошёлся и какие концы остались
/// открытыми — параметризован правилом связи, домен ничего об этом не знает. Это тот же
/// алгоритм, что раньше жил только в PipeNetwork; PipeNetwork теперь тонкая обёртка над
/// PortNetwork.Build с PipeJoint.Connects в качестве правила.</summary>
public class PortNetworkTests
{
    private static Port[] Corner()
    {
        var start = new PointMm(0f, 0f, 0f);
        var corner = new PointMm(1000f, 0f, 0f);
        var top = new PointMm(1000f, 800f, 0f);

        return new[]
        {
            new Port("p1", 0, start, PipeAxis.Left),
            new Port("p1", 1, corner, PipeAxis.Right),
            new Port("p2", 0, corner, PipeAxis.Down),
            new Port("p2", 1, top, PipeAxis.Up),
            new Port("elbow", 0, corner, PipeAxis.Left),
            new Port("elbow", 1, corner, PipeAxis.Up),
        };
    }

    private static bool Connects(Port a, Port b) => PortJoint.Connects(a, b);

    [Test]
    public void PortNetwork_Build_LinksPortsThatMeet()
    {
        var ports = Corner();
        var network = PortNetwork.Build(ports, Connects);

        Assert.AreEqual(2, network.Links.Count, "два стыка: p1-elbow и p2-elbow");
    }

    [Test]
    public void PortNetwork_Build_IsSymmetric()
    {
        var ports = Corner();
        var network = PortNetwork.Build(ports, Connects);

        foreach (var link in network.Links)
        {
            Assert.AreEqual(link.BPortIndex, network.PartnerOf(link.APortIndex));
            Assert.AreEqual(link.APortIndex, network.PartnerOf(link.BPortIndex));
        }
    }

    [Test]
    public void PortNetwork_FreePorts_AreTheEndsLeftOpen()
    {
        var ports = Corner();
        var network = PortNetwork.Build(ports, Connects);

        CollectionAssert.AreEquivalent(new[] { 0, 3 }, network.FreePorts,
            "свободны только дальние торцы p1[0] и p2[1]");
        Assert.IsFalse(network.IsFree(1));
        Assert.IsTrue(network.IsFree(0));
    }

    [Test]
    public void PortNetwork_Build_LeavesPortsFree_WhenTheGapExceedsTheTolerance()
    {
        var start = new PointMm(0f, 0f, 0f);
        var corner = new PointMm(1000f, 0f, 0f);
        var moved = new PointMm(1002f, 0f, 0f);

        var ports = new[]
        {
            new Port("p1", 0, start, PipeAxis.Left),
            new Port("p1", 1, corner, PipeAxis.Right),
            new Port("elbow", 0, moved, PipeAxis.Left),
            new Port("elbow", 1, moved, PipeAxis.Up),
        };

        var network = PortNetwork.Build(ports, Connects);
        Assert.AreEqual(0, network.Links.Count, "2 мм между торцами — не стык");
        Assert.AreEqual(ports.Length, network.FreePorts.Count);
    }

    [Test]
    public void PortNetwork_PartnerOf_IsNoPartner_OutsideTheRange()
    {
        var network = PortNetwork.Build(new List<Port>(), Connects);
        Assert.AreEqual(PortNetwork.NoPartner, network.PartnerOf(0));
        Assert.AreEqual(PortNetwork.NoPartner, network.PartnerOf(-1));
    }
}
