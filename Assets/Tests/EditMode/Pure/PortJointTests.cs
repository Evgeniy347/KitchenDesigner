using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Ports;

/// <summary>Когда два порта generic-слоя считаются соединёнными — геометрия контакта БЕЗ
/// знания предметной области (труба, воздуховод, …). Домен решает отдельно, каким родам
/// разрешено сходиться (PipeConnectionRule и её будущий аналог для вентиляции); здесь —
/// только расстояние и встречные оси.</summary>
public class PortJointTests
{
    private static readonly PointMm Origin = new PointMm(1000f, 500f, 200f);

    private static Port PortAt(string elementId, int index, in PointMm at, in PipeAxis outward) =>
        new Port(elementId, index, at, outward);

    [Test]
    public void PortJoint_Tolerance_IsTheProjectContactTolerance()
    {
        Assert.AreEqual(Tolerance.ContactMm, PortJoint.JoinToleranceMm,
            "стык портов — тот же контакт, что и у деталей; отдельной константы под порты нет");
    }

    [Test]
    public void PortJoint_Connects_WhenPortsMeetHeadOn()
    {
        Assert.IsTrue(PortJoint.Connects(
            PortAt("a", 0, Origin, PipeAxis.Right),
            PortAt("b", 0, Origin, PipeAxis.Left)));
    }

    [Test]
    public void PortJoint_DoesNotConnect_WhenAxesPointTheSameWay()
    {
        Assert.IsFalse(PortJoint.Connects(
            PortAt("a", 0, Origin, PipeAxis.Right),
            PortAt("b", 0, Origin, PipeAxis.Right)),
            "два порта, смотрящие в одну сторону из одной точки, наложены друг на друга, а не состыкованы");
    }

    [Test]
    public void PortJoint_DoesNotConnect_WhenAxesAreAtRightAngles()
    {
        Assert.IsFalse(PortJoint.Connects(
            PortAt("a", 0, Origin, PipeAxis.Right),
            PortAt("b", 0, Origin, PipeAxis.Up)));
    }

    [Test]
    public void PortJoint_Connects_WithinContactTolerance_AndNotBeyondIt()
    {
        var inside = new PointMm(Origin.XMm + Tolerance.ContactMm * 0.8f, Origin.YMm, Origin.ZMm);
        var outside = new PointMm(Origin.XMm + Tolerance.ContactMm * 2f, Origin.YMm, Origin.ZMm);

        Assert.IsTrue(PortJoint.Connects(
            PortAt("a", 0, Origin, PipeAxis.Right), PortAt("b", 0, inside, PipeAxis.Left)),
            "0,4 мм — монтажный зазор, а не разрыв трассы");
        Assert.IsFalse(PortJoint.Connects(
            PortAt("a", 0, Origin, PipeAxis.Right), PortAt("b", 0, outside, PipeAxis.Left)),
            "1,0 мм между торцами — уже дыра");
    }

    [Test]
    public void PortJoint_DoesNotConnect_TwoPortsOfOneElement()
    {
        Assert.IsFalse(PortJoint.Connects(
            PortAt("a", 0, Origin, PipeAxis.Right),
            PortAt("a", 1, Origin, PipeAxis.Left)),
            "элемент не соединяется сам с собой, даже если его торцы совпали");
    }

    [Test]
    public void PortJoint_DoesNotConnect_WhenAnAxisIsDegenerate()
    {
        Assert.IsFalse(PortJoint.Connects(
            PortAt("a", 0, Origin, new PipeAxis(0f, 0f, 0f)),
            PortAt("b", 0, Origin, PipeAxis.Left)),
            "нулевая ось не задаёт направления — соединение по ней было бы выдумкой");
    }

    [Test]
    public void PortJoint_ProfilesCompatible_100x200_DoesNotSeatOn_200x100()
    {
        Assert.IsFalse(PortJoint.ProfilesCompatible("rect100x200", "rect200x100"),
            "прямоугольное устье не поворачивается само собой: 100×200 и 200×100 — разные профили");
    }

    [Test]
    public void PortJoint_ProfilesCompatible_100x200_SeatsOn_100x200()
    {
        Assert.IsTrue(PortJoint.ProfilesCompatible("rect100x200", "rect100x200"));
    }

    [Test]
    public void PortJoint_ProfilesCompatible_WhenEitherProfileIsUnspecified()
    {
        Assert.IsTrue(PortJoint.ProfilesCompatible(null, "rect100x200"),
            "труба сегодня не заявляет профиль на стыке (диаметр выводится с трассы) — неуказанный профиль не блокирует");
        Assert.IsTrue(PortJoint.ProfilesCompatible("rect100x200", null));
        Assert.IsTrue(PortJoint.ProfilesCompatible(null, null));
    }
}
