using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class TreeRowLayoutTests
{
    private static readonly TreeMetrics Mockup = new TreeMetrics(0f, 12f, 12f, 4f, 14f, 8f);

    [Test]
    public void Level_ChevronAtTheLead_LabelRightAfterIt_NoIcon()
    {
        var slots = TreeRowLayout.For(TreeRowKind.Level, 0, Mockup);

        Assert.IsTrue(slots.HasChevron);
        Assert.IsFalse(slots.HasIcon, "этаж — заголовок группы, иконки типа у него нет (D8)");
        Assert.AreEqual(0f, slots.ChevronX);
        Assert.AreEqual(16f, slots.LabelX);
    }

    [Test]
    public void ElementUnderALevel_SitsUnderTheLevelLabel_BecauseItHasNoChevronOfItsOwn()
    {
        var level = TreeRowLayout.For(TreeRowKind.Level, 0, Mockup);
        var element = TreeRowLayout.For(TreeRowKind.Element, 1, Mockup);

        Assert.IsFalse(element.HasChevron);
        Assert.AreEqual(level.LabelX, element.IconX,
            "иконка детали стоит ровно под названием этажа: колонка стрелки у детали пустая, но место держит");
    }

    [Test]
    public void GroupAndItsMembers_StepByOneIndentPerLevel()
    {
        var group = TreeRowLayout.For(TreeRowKind.Group, 1, Mockup);
        var member = TreeRowLayout.For(TreeRowKind.Element, 2, Mockup);
        var sibling = TreeRowLayout.For(TreeRowKind.Element, 1, Mockup);

        Assert.AreEqual(sibling.IconX, group.IconX,
            "значок группы стоит там же, где значок её соседа вне группы: стрелка группы уходит влево, а не сдвигает строку");
        Assert.AreEqual(sibling.IconX + 12f, member.IconX, "член группы — на один отступ правее");
        Assert.AreEqual(group.LabelX + 12f, member.LabelX);
    }

    [Test]
    public void Group_ChevronPrecedesTheIcon_ByTheChevronColumnAndItsGap()
    {
        var group = TreeRowLayout.For(TreeRowKind.Group, 1, Mockup);

        Assert.AreEqual(group.ChevronX + 12f + 4f, group.IconX);
        Assert.AreEqual(group.IconX + 14f + 8f, group.LabelX);
    }

    [TestCase(TreeRowKind.Group)]
    [TestCase(TreeRowKind.Element)]
    public void DeeperRows_NeverStepBackLeft(TreeRowKind kind)
    {
        float previous = float.MinValue;
        for (int depth = 0; depth < 6; depth++)
        {
            float x = TreeRowLayout.For(kind, depth, Mockup).LabelX;
            Assert.GreaterOrEqual(x, previous, "глубина " + depth + ": строка глубже не может стоять левее мелкой");
            previous = x;
        }
    }
}
