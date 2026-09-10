using NUnit.Framework;
using KitchenDesigner.Core.UI;

/// <summary>
/// «i» встаёт сразу за текстом подписи, а не в фиксированной колонке: колонка подписи
/// шириной 300 px, а подписи бывают в 40 px («Стены») и в 260 px («Скрывать окна и
/// двери»), и значок на дальнем краю читался бы как относящийся к соседнему контролу.
/// Второе требование — значок НИКОГДА не заезжает в колонку контрола: там он перекрыл
/// бы клик по самому переключателю.
/// </summary>
public class HintBadgeLaneTests
{
    private const float LabelCentre = -90f;
    private const float LabelWidth = 300f;
    private const float Badge = 24f;
    private const float Gap = 8f;

    private static float X(float textWidth) =>
        HintBadgeLane.AfterLabel(LabelCentre, LabelWidth, textWidth, Badge, Gap);

    [Test]
    public void ShortLabel_PutsTheBadgeRightAfterTheText()
    {
        float left = LabelCentre - LabelWidth * 0.5f;
        Assert.AreEqual(left + 40f + Gap + Badge * 0.5f, X(40f), 0.01f,
            "значок отступает от конца текста ровно на общий внутренний зазор — иначе он "
            + "прилипает к букве и читается как её часть");
    }

    [Test]
    public void LongerLabel_MovesTheBadgeFurtherRight()
    {
        Assert.Greater(X(200f), X(40f),
            "значок идёт за текстом — иначе он висел бы в пустоте посреди подписи");
    }

    [Test]
    public void LabelThatFillsTheColumn_KeepsTheBadgeInsideIt()
    {
        float rightmost = LabelCentre + LabelWidth * 0.5f - Badge * 0.5f;
        Assert.LessOrEqual(X(LabelWidth), rightmost + 0.01f,
            "значок не должен выехать в колонку контрола и перехватить клик по нему");
        Assert.LessOrEqual(X(LabelWidth * 3f), rightmost + 0.01f,
            "даже если TMP насчитал ширину больше самой колонки");
    }

    [Test]
    public void EmptyLabel_StillLeavesTheBadgeInsideTheColumn()
    {
        float leftmost = LabelCentre - LabelWidth * 0.5f + Badge * 0.5f;
        Assert.GreaterOrEqual(X(0f), leftmost - 0.01f,
            "у безымянной строки значок прижимается к началу колонки, а не выходит левее её");
    }
}
