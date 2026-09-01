using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Где именно всплывает подсказка иконной кнопки: под кнопкой, не
/// вылезая за края канвы, а если снизу места нет — над кнопкой.</summary>
public class TooltipPlacementTests
{
    private static readonly Vector2 CanvasSize = new Vector2(1920, 1080);
    private static readonly Vector2 Size = new Vector2(120, 30);

    [Test]
    public void Below_ButtonInTheMiddle_TooltipHangsUnderItsBottomEdge()
    {
        var pos = TooltipPlacement.Below(new Vector2(0, 100), 40f, Size, CanvasSize);

        Assert.AreEqual(0f, pos.x, 0.001f, "подсказка центрируется по кнопке");
        Assert.AreEqual(100f - TooltipPlacement.GapPx, pos.y, 0.001f,
            "у панели подсказки pivot сверху: её верх встаёт под низ кнопки с зазором");
    }

    [Test]
    public void Below_ButtonAtRightEdge_TooltipStaysInsideCanvas()
    {
        var pos = TooltipPlacement.Below(new Vector2(CanvasSize.x * 0.5f, 0), 40f, Size, CanvasSize);

        float rightEdge = pos.x + Size.x * 0.5f;
        Assert.LessOrEqual(rightEdge, CanvasSize.x * 0.5f - TooltipPlacement.ScreenPadPx + 0.001f,
            "подсказка у крайней кнопки тулбара обязана прижаться к краю экрана, "
            + "а не уехать за него — там её просто не видно");
    }

    [Test]
    public void Below_NoRoomUnderButton_TooltipFlipsAboveIt()
    {
        float bottomOfCanvas = -CanvasSize.y * 0.5f;
        const float buttonHeight = 40f;
        float buttonBottom = bottomOfCanvas + 10f;
        var pos = TooltipPlacement.Below(new Vector2(0, buttonBottom), buttonHeight, Size, CanvasSize);

        Assert.Greater(pos.y - Size.y, bottomOfCanvas,
            "у кнопки в самом низу подсказка уходит НАД кнопкой: снизу её было бы "
            + "не видно");
        Assert.AreEqual(buttonBottom + buttonHeight + TooltipPlacement.GapPx, pos.y - Size.y, 0.001f,
            "сместить надо на полную высоту подсказки плюс высоту кнопки — pivot у "
            + "подсказки сверху, и без этого она легла бы на саму кнопку");
    }
}
