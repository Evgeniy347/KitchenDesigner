using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Plumbing;

/// <summary>Когда два порта считаются соединёнными.
///
/// Допуск НЕ заведён заново: стык — это контакт двух торцов, и мерой контакта в
/// проекте уже служит Tolerance.ContactMm (0,5 мм). Соосность мерится тем же
/// Tolerance.ParallelDot, что и параллельность граней в снэпе. Заводить третий
/// набор чисел под трубы значило бы, что «касается» в разных подсистемах
/// означает разное.</summary>
public class PipeJointTests
{
    private static readonly PointMm Origin = new PointMm(1000f, 500f, 200f);

    private static PipePort Port(string id, in PointMm at, in PipeAxis outward) =>
        new PipePort(id, PipeNodeKind.Pipe, 0, at, outward, PipeSpec.Dn20);

    [Test]
    public void PipeJoint_Tolerance_IsTheProjectContactTolerance()
    {
        Assert.AreEqual(Tolerance.ContactMm, PipeJoint.JoinToleranceMm,
            "стык труб — тот же контакт, что и у деталей; отдельной константы под трубы нет");
    }

    [Test]
    public void PipeJoint_Connects_WhenPortsMeetHeadOn()
    {
        Assert.IsTrue(PipeJoint.Connects(
            Port("a", Origin, PipeAxis.Right),
            Port("b", Origin, PipeAxis.Left)));
    }

    [Test]
    public void PipeJoint_DoesNotConnect_WhenAxesPointTheSameWay()
    {
        Assert.IsFalse(PipeJoint.Connects(
            Port("a", Origin, PipeAxis.Right),
            Port("b", Origin, PipeAxis.Right)),
            "две трубы, идущие в одну сторону из одной точки, наложены друг на друга, а не состыкованы");
    }

    [Test]
    public void PipeJoint_DoesNotConnect_WhenAxesAreAtRightAngles()
    {
        Assert.IsFalse(PipeJoint.Connects(
            Port("a", Origin, PipeAxis.Right),
            Port("b", Origin, PipeAxis.Up)));
    }

    [Test]
    public void PipeJoint_Connects_WithinContactTolerance_AndNotBeyondIt()
    {
        var inside = new PointMm(Origin.XMm + Tolerance.ContactMm * 0.8f, Origin.YMm, Origin.ZMm);
        var outside = new PointMm(Origin.XMm + Tolerance.ContactMm * 2f, Origin.YMm, Origin.ZMm);

        Assert.IsTrue(PipeJoint.Connects(
            Port("a", Origin, PipeAxis.Right), Port("b", inside, PipeAxis.Left)),
            "0,4 мм — монтажный зазор, а не разрыв трассы");
        Assert.IsFalse(PipeJoint.Connects(
            Port("a", Origin, PipeAxis.Right), Port("b", outside, PipeAxis.Left)),
            "1,0 мм между торцами — уже дыра: вода пойдёт мимо");
    }

    [Test]
    public void PipeJoint_DoesNotConnect_TwoPortsOfOneElement()
    {
        Assert.IsFalse(PipeJoint.Connects(
            new PipePort("a", PipeNodeKind.Pipe, 0, Origin, PipeAxis.Right, PipeSpec.Dn20),
            new PipePort("a", PipeNodeKind.Pipe, 1, Origin, PipeAxis.Left, PipeSpec.Dn20)),
            "элемент не соединяется сам с собой, даже если его торцы совпали");
    }

    [Test]
    public void PipeJoint_DoesNotConnect_WhenAnAxisIsDegenerate()
    {
        Assert.IsFalse(PipeJoint.Connects(
            Port("a", Origin, new PipeAxis(0f, 0f, 0f)),
            Port("b", Origin, PipeAxis.Left)),
            "нулевая ось не задаёт направления — соединение по ней было бы выдумкой");
    }

    [Test]
    public void PipeAxis_AreOpposite_IgnoresLength_ButNotDirection()
    {
        Assert.IsTrue(PipeAxis.AreOpposite(new PipeAxis(7f, 0f, 0f), new PipeAxis(-0.25f, 0f, 0f)),
            "оси приходят из сцены ненормированными; важно направление, а не длина");
        Assert.IsFalse(PipeAxis.AreOpposite(new PipeAxis(7f, 0f, 0f), new PipeAxis(0.25f, 0f, 0f)));
    }
}
