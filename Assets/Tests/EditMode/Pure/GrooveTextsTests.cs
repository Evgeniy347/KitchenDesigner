using KitchenDesigner.Core;
using NUnit.Framework;

public class GrooveTextsTests
{
    /// <summary>Обозначение попадает в спецификацию, поэтому вид паза в нём
    /// обязан различаться — иначе глухой и сквозной сольются в одну строку.</summary>
    [Test]
    public void Designation_DistinguishesKinds()
    {
        Assert.AreNotEqual(GrooveTexts.Designation(GrooveKind.Blind), GrooveTexts.Designation(GrooveKind.Through));
    }

    [Test]
    public void SideLabel_IsDistinctForEverySide()
    {
        var labels = new[]
        {
            GrooveTexts.SideLabel(GrooveSide.Top),
            GrooveTexts.SideLabel(GrooveSide.Bottom),
            GrooveTexts.SideLabel(GrooveSide.Left),
            GrooveTexts.SideLabel(GrooveSide.Right),
        };

        CollectionAssert.AllItemsAreUnique(labels);
        foreach (var label in labels)
            Assert.IsNotEmpty(label, "пустая подпись стороны не отличима от отсутствующей");
    }

    [Test]
    public void Of_NamesKindAndSide_ForTheSpecification()
    {
        var text = GrooveTexts.Of(new GrooveSpec(GrooveKind.Blind, GrooveSide.Top));
        StringAssert.StartsWith(GrooveTexts.Designation(GrooveKind.Blind), text);
        StringAssert.EndsWith(GrooveTexts.SideLabel(GrooveSide.Top), text);
    }
}
