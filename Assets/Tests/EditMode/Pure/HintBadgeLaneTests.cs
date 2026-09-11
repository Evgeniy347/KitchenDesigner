using NUnit.Framework;
using UnityEngine;
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

    private const float PropertyDropdownLabelWidth = 126f;
    private const float PropertyFieldLabelWidth = 140f;

    /// <summary>Ширина «Длина» в Liberation Sans 15 px: Д+л+и+н+а = 2946/1000 em,
    /// то есть 44,2 px. Именно это число ставило значок в ЦЕНТР колонки подписи.</summary>
    private const float DlinaTextWidth = 44.2f;

    /// <summary>Допуск инварианта золотых снимков (<c>UiNodeOverlap.CellPixels</c>):
    /// два узла ближе двух пикселей по обеим осям считаются стоящими в одной точке.
    /// Константа продублирована числом, потому что быстрый путь собирается без
    /// сборки тестов Unity; EditMode-страж в <c>HintBadgeVisibilityTests</c> берёт
    /// её из самого <c>UiNodeOverlap</c>.</summary>
    private const float OverlapTolerance = 2f;

    private static float NarrowedX(float labelWidth, float textWidth)
    {
        float narrowed = HintBadgeLane.LabelWidthWithLane(labelWidth, textWidth, Badge, Gap);
        return HintBadgeLane.AfterLabel(0f, narrowed, textWidth, Badge, Gap);
    }

    /// <summary>Противоположный вход: показывает, ПОЧЕМУ подпись приходится ужимать.
    /// В колонке во всю ширину значок «Длины» садился ровно в центр прямоугольника
    /// подписи — а центр прямоугольника и есть точка узла и в снимке, и в
    /// <c>UiNodeOverlap</c>. Два узла в одной точке, и виноват не значок, а подпись:
    /// она занимает 126 px под слово в 44 px, и её точка попадает в пустое место
    /// внутри неё самой — ровно туда, куда становится значок.</summary>
    [Test]
    public void FullWidthColumn_PutTheBadgeOnTheVeryCentreOfTheLabel()
    {
        float x = HintBadgeLane.AfterLabel(0f, PropertyDropdownLabelWidth, DlinaTextWidth,
            Badge, Gap);
        Assert.Less(Mathf.Abs(x), OverlapTolerance,
            "если это утверждение стало красным, изменилась ширина колонки или шрифт — "
            + "дефект «значок в центре подписи ЯЩИКА» больше не воспроизводится этими "
            + "числами, и сторож ниже проверяет уже не тот случай");
    }

    [Test]
    public void NarrowedLabel_KeepsTheBadgeClearOfTheLabelCentre_AtEveryTextWidth()
    {
        var offenders = new System.Collections.Generic.List<string>();

        foreach (float column in new[]
                     { PropertyDropdownLabelWidth, PropertyFieldLabelWidth, LabelWidth })
        {
            int hits = 0;
            for (float textWidth = 0f; textWidth <= column * 2f; textWidth += 0.5f)
                if (Mathf.Abs(NarrowedX(column, textWidth)) < OverlapTolerance) hits++;

            if (hits > 0) offenders.Add($"колонка {column} px: {hits} ширин текста");
        }

        Assert.IsEmpty(offenders,
            "Значок «i» не смеет вставать в центр прямоугольника своей подписи: центр — "
            + "это точка узла, по которой UiNodeOverlap ищет узлы, стоящие в одной точке. "
            + "Подпись с подсказкой ужимается до «текст + дорожка значка», поэтому её центр "
            + "уезжает влево от дорожки на (текст + зазор)/2 ≥ " + (Gap * 0.5f)
            + " px при любой ширине текста. Нарушено: " + string.Join(" | ", offenders));
    }

    [Test]
    public void LabelWidthWithLane_NeverWidens_OnlyTrimsTheEmptyPartOfTheColumn()
    {
        Assert.AreEqual(PropertyDropdownLabelWidth,
            HintBadgeLane.LabelWidthWithLane(PropertyDropdownLabelWidth, 400f, Badge, Gap), 0.01f,
            "длинная подпись остаётся во всю колонку — иначе значок выехал бы в колонку "
            + "контрола и перехватил клик по нему");
        Assert.AreEqual(DlinaTextWidth + Gap + Badge,
            HintBadgeLane.LabelWidthWithLane(PropertyDropdownLabelWidth, DlinaTextWidth,
                Badge, Gap), 0.01f,
            "короткая подпись отдаёт пустую часть колонки: за словом остаётся ровно зазор "
            + "и дорожка значка");
    }

    [Test]
    public void NarrowedLabel_StillPutsTheBadgeRightAfterTheText()
    {
        float narrowed = HintBadgeLane.LabelWidthWithLane(PropertyDropdownLabelWidth,
            DlinaTextWidth, Badge, Gap);
        float textEnd = -narrowed * 0.5f + DlinaTextWidth;
        float badgeLeft = NarrowedX(PropertyDropdownLabelWidth, DlinaTextWidth) - Badge * 0.5f;

        Assert.AreEqual(Gap, badgeLeft - textEnd, 0.01f,
            "ужимание подписи не должно двигать значок: он стоит там же, где и стоял, — "
            + "на общий внутренний зазор за концом слова");
    }
}
