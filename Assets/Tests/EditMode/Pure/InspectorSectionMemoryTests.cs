using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class InspectorSectionMemoryTests
{
    [Test]
    public void SectionNeverTouched_ShowsItsDefault_ForBothDefaults()
    {
        var memory = new InspectorSectionMemory();

        Assert.IsTrue(memory.IsExpanded("Drawer", "Grooves", byDefault: true));
        Assert.IsFalse(memory.IsExpanded("Drawer", "Grooves", byDefault: false),
            "без записи секция показывает свой штатный вид, а не «развёрнуто» вообще");
    }

    [Test]
    public void ACollapsedSection_StaysCollapsed_ForThatElementTypeOnly()
    {
        var memory = new InspectorSectionMemory();
        memory.Remember("Wall", "Textures", expanded: false);

        Assert.IsFalse(memory.IsExpanded("Wall", "Textures", byDefault: true),
            "свёрнутость стены помнится при следующем открытии стены");
        Assert.IsTrue(memory.IsExpanded("Part", "Textures", byDefault: true),
            "у другого типа элемента та же секция живёт по-своему: память по ТИПУ, а не по секции");
    }

    [Test]
    public void TwoSectionsOfOneType_AreRememberedIndependently()
    {
        var memory = new InspectorSectionMemory();
        memory.Remember("Part", "Grooves", expanded: true);
        memory.Remember("Part", "Gaps", expanded: false);

        Assert.IsTrue(memory.IsExpanded("Part", "Grooves", byDefault: false));
        Assert.IsFalse(memory.IsExpanded("Part", "Gaps", byDefault: true));
    }

    [Test]
    public void TheLastRemembered_Wins()
    {
        var memory = new InspectorSectionMemory();
        memory.Remember("Part", "Edges", expanded: false);
        memory.Remember("Part", "Edges", expanded: true);

        Assert.IsTrue(memory.IsExpanded("Part", "Edges", byDefault: false));
    }
}
