using NUnit.Framework;
using KitchenDesigner.Core.Plumbing;

/// <summary>Сеть трассы: кто с кем сошёлся и какие концы остались открытыми.
///
/// Сеть строится ТОЛЬКО из портов — ядру не нужна ни сцена, ни трансформы. Порт
/// берёт не больше одного партнёра: три торца, сошедшихся в одной точке, — это
/// тройник, а не тройное соединение труб, и ядро не должно делать вид, что
/// понимает такую геометрию.</summary>
public class PipeNetworkTests
{
    private static PipeTestScene Corner(string secondPipeSize)
    {
        var start = PipeTestScene.At(0f, 0f, 0f);
        var corner = PipeTestScene.At(1000f, 0f, 0f);
        var top = PipeTestScene.At(1000f, 800f, 0f);

        return new PipeTestScene()
            .Pipe("p1", start, corner, PipeSpec.Dn20)
            .Pipe("p2", corner, top, secondPipeSize)
            .Fitting("elbow", PipeNodeKind.Elbow,
                (corner, PipeAxis.Left), (corner, PipeAxis.Up));
    }

    [Test]
    public void PipeNetwork_Build_LinksBothPipesToTheElbow()
    {
        var scene = Corner(PipeSpec.Dn20);
        var network = PipeNetwork.Build(scene.Ports());

        Assert.AreEqual(2, network.Links.Count, "две трубы и отвод дают ровно два стыка");
        Assert.AreEqual(scene.IndexOf("elbow", 0), network.PartnerOf(scene.IndexOf("p1", 1)));
        Assert.AreEqual(scene.IndexOf("elbow", 1), network.PartnerOf(scene.IndexOf("p2", 0)));
    }

    [Test]
    public void PipeNetwork_Build_IsSymmetric()
    {
        var scene = Corner(PipeSpec.Dn20);
        var network = PipeNetwork.Build(scene.Ports());

        foreach (var link in network.Links)
        {
            Assert.AreEqual(link.BPortIndex, network.PartnerOf(link.APortIndex));
            Assert.AreEqual(link.APortIndex, network.PartnerOf(link.BPortIndex));
        }
    }

    [Test]
    public void PipeNetwork_FreePorts_AreTheTwoEndsOfTheTrace()
    {
        var scene = Corner(PipeSpec.Dn20);
        var network = PipeNetwork.Build(scene.Ports());

        CollectionAssert.AreEquivalent(
            new[] { scene.IndexOf("p1", 0), scene.IndexOf("p2", 1) },
            network.FreePorts,
            "свободны только дальние торцы: у угла всё сошлось");
        Assert.IsFalse(network.IsFree(scene.IndexOf("p1", 1)));
        Assert.IsTrue(network.IsFree(scene.IndexOf("p1", 0)));
    }

    [Test]
    public void PipeNetwork_Build_LeavesPortsFree_WhenTheGapExceedsTheTolerance()
    {
        var start = PipeTestScene.At(0f, 0f, 0f);
        var corner = PipeTestScene.At(1000f, 0f, 0f);
        var moved = PipeTestScene.At(1002f, 0f, 0f);

        var scene = new PipeTestScene()
            .Pipe("p1", start, corner, PipeSpec.Dn20)
            .Fitting("elbow", PipeNodeKind.Elbow, (moved, PipeAxis.Left), (moved, PipeAxis.Up));

        var network = PipeNetwork.Build(scene.Ports());
        Assert.AreEqual(0, network.Links.Count, "2 мм между торцами — не стык");
        Assert.AreEqual(scene.Ports().Count, network.FreePorts.Count);
    }

    [Test]
    public void PipeNetwork_PartnerOf_IsNoPartner_OutsideTheRange()
    {
        var network = PipeNetwork.Build(new PipePort[0]);
        Assert.AreEqual(PipeNetwork.NoPartner, network.PartnerOf(0));
        Assert.AreEqual(PipeNetwork.NoPartner, network.PartnerOf(-1));
    }
}
