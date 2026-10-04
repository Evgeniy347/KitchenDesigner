using KitchenDesigner.Core.UI;
using NUnit.Framework;

public class ToastLayoutMathTests
{
    private static readonly ToastMetrics M = new ToastMetrics(minHeight: 48f, padX: 12f, padY: 12f,
        gap: 8f, stripeWidth: 3f, iconSize: 16f, dismissSize: 24f, maxWidth: 560f);

    [Test]
    public void ToastLayout_ElementsFollowEachOther_LeftToRight_WithoutOverlap()
    {
        var layout = ToastLayout.For(M, textWidth: 200f, textHeight: 20f, actionWidth: 70f);

        Assert.AreEqual(12f + 3f + 8f, layout.IconX, 1e-4f, "значок — после полосы уровня");
        Assert.GreaterOrEqual(layout.TextX, layout.IconX + 16f + 8f - 1e-4f);
        Assert.GreaterOrEqual(layout.ActionX, layout.TextX + layout.TextWidth + 8f - 1e-4f,
            "действие не заходит на текст");
        Assert.GreaterOrEqual(layout.DismissX, layout.ActionX + layout.ActionWidth + 8f - 1e-4f,
            "× не заходит на действие");
        Assert.AreEqual(layout.DismissX + 24f + 12f, layout.Width, 1e-4f, "× — у правого края с паддингом");
    }

    [Test]
    public void ToastLayout_WithoutAnAction_LeavesNoGapForIt()
    {
        var with = ToastLayout.For(M, 200f, 20f, actionWidth: 70f);
        var without = ToastLayout.For(M, 200f, 20f, actionWidth: 0f);

        Assert.AreEqual(70f + 8f, with.Width - without.Width, 1e-4f,
            "тост без действия на столько уже: пустого места под невидимую кнопку нет");
        Assert.AreEqual(without.TextX + without.TextWidth + 8f, without.DismissX, 1e-4f);
    }

    [Test]
    public void ToastLayout_LongText_IsClampedToTheBudget_AndTheWidthStopsAtTheMaximum()
    {
        var layout = ToastLayout.For(M, textWidth: 5000f, textHeight: 60f, actionWidth: 70f);

        Assert.AreEqual(M.MaxWidth, layout.Width, 1e-3f, "длинный текст не растягивает тост за максимум");
        Assert.AreEqual(ToastLayout.TextWidthBudget(M, 70f), layout.TextWidth, 1e-3f);
        Assert.AreEqual(60f + 2f * 12f, layout.Height, 1e-4f, "перенос строк растит высоту");
    }

    [Test]
    public void ToastLayout_OneLine_HasTheMinimumHeight()
    {
        Assert.AreEqual(48f, ToastLayout.For(M, 100f, 20f, 0f).Height, 1e-4f);
    }
}
