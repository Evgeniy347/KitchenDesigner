using NUnit.Framework;
using KitchenDesigner.Core.Ports;
using KitchenDesigner.Core.Ventilation;

/// <summary>DuctNodeKind — СВОЙ род узлов (3.5: «Не расширять PipeNodeKind»), не ветка
/// существующего pipe-switch'а. Раскладка фитингов повторяет геометрию трубных (отвод —
/// поворот, тройник — три ноги), но заведена отдельным switch'ем над DuctFittingSpec.</summary>
public class DuctFittingSpecTests
{
    [Test]
    public void Duct_IsNotAFitting()
    {
        Assert.IsFalse(DuctFittingSpec.IsFitting(DuctNodeKind.Duct));
    }

    [Test]
    public void EveryOtherKind_IsAFitting()
    {
        Assert.IsTrue(DuctFittingSpec.IsFitting(DuctNodeKind.Elbow));
        Assert.IsTrue(DuctFittingSpec.IsFitting(DuctNodeKind.Transition));
        Assert.IsTrue(DuctFittingSpec.IsFitting(DuctNodeKind.Tee));
        Assert.IsTrue(DuctFittingSpec.IsFitting(DuctNodeKind.Cap));
        Assert.IsTrue(DuctFittingSpec.IsFitting(DuctNodeKind.Grille));
    }

    [Test]
    public void Duct_HasNoCatalogedLegs()
    {
        Assert.AreEqual(0, DuctFittingSpec.Legs(DuctNodeKind.Duct).Count,
            "прямой участок воздуховода — не фитинг, его устья не каталогизируются здесь, как и у трубы");
    }

    [Test]
    public void Elbow_TurnsFromDownToRight_LikeAPipeElbow()
    {
        Assert.AreEqual(2, DuctFittingSpec.PortCount(DuctNodeKind.Elbow));
        Assert.AreEqual(PipeAxis.Down, DuctFittingSpec.Legs(DuctNodeKind.Elbow).Axes[0]);
        Assert.AreEqual(PipeAxis.Right, DuctFittingSpec.Legs(DuctNodeKind.Elbow).Axes[1]);
    }

    [Test]
    public void Transition_GoesStraightThrough()
    {
        Assert.AreEqual(2, DuctFittingSpec.PortCount(DuctNodeKind.Transition));
    }

    [Test]
    public void Tee_HasThreePorts()
    {
        Assert.AreEqual(3, DuctFittingSpec.PortCount(DuctNodeKind.Tee));
    }

    [Test]
    public void Cap_HasOnePort()
    {
        Assert.AreEqual(1, DuctFittingSpec.PortCount(DuctNodeKind.Cap));
    }

    [Test]
    public void Grille_HasOnePort()
    {
        Assert.AreEqual(1, DuctFittingSpec.PortCount(DuctNodeKind.Grille));
    }

    [Test]
    public void Kinds_ListsAllSixInDeclarationOrder()
    {
        CollectionAssert.AreEqual(new[]
        {
            DuctNodeKind.Duct,
            DuctNodeKind.Elbow,
            DuctNodeKind.Transition,
            DuctNodeKind.Tee,
            DuctNodeKind.Cap,
            DuctNodeKind.Grille,
        }, DuctFittingSpec.Kinds);
    }
}
