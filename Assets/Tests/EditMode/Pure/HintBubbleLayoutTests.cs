using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.UI;

/// <summary>
/// Облачко подсказки не должно вылезать за экран. Базовый экран продукта — 1366×768,
/// и подсказка у правого края инспектора (сайдбар стоит справа) — как раз тот случай,
/// где наивное «поставить справа от i» уводит половину текста за границу. Координаты
/// здесь канвасные: центр канваса — ноль, вправо и вверх — плюс.
/// </summary>
public class HintBubbleLayoutTests
{
    private static readonly Vector2 Screen1366 = new Vector2(1366f, 768f);
    private static readonly Vector2 Bubble = new Vector2(320f, 90f);

    private static float Right(Vector2 center, Vector2 size) => center.x + size.x * 0.5f;
    private static float Left(Vector2 center, Vector2 size) => center.x - size.x * 0.5f;
    private static float Top(Vector2 center, Vector2 size) => center.y + size.y * 0.5f;
    private static float Bottom(Vector2 center, Vector2 size) => center.y - size.y * 0.5f;

    [Test]
    public void NearTheCentre_TheBubbleStandsToTheRightOfTheBadge()
    {
        var badge = new Vector2(-200f, 100f);
        var pos = HintBubbleLayout.Beside(badge, 24f, Bubble, Screen1366);

        Assert.Greater(Left(pos, Bubble), badge.x,
            "по умолчанию облачко встаёт справа от «i» — там его читают, не закрывая подпись");
        Assert.AreEqual(badge.y, pos.y, 0.01f, "и по вертикали держится центра значка");
    }

    [Test]
    public void AtTheRightEdge_TheBubbleFlipsToTheLeft_InsteadOfLeavingTheScreen()
    {
        var badge = new Vector2(Screen1366.x * 0.5f - 40f, 0f);
        var pos = HintBubbleLayout.Beside(badge, 24f, Bubble, Screen1366);

        Assert.Less(Right(pos, Bubble), badge.x,
            "у правого края облачко обязано перевернуться влево, а не срезаться");
        Assert.LessOrEqual(Right(pos, Bubble), Screen1366.x * 0.5f,
            "и всё равно остаться внутри экрана");
    }

    [Test]
    public void AtTheTopEdge_TheBubbleSlidesDown_ButKeepsItsSide()
    {
        var badge = new Vector2(0f, Screen1366.y * 0.5f - 10f);
        var pos = HintBubbleLayout.Beside(badge, 24f, Bubble, Screen1366);

        Assert.LessOrEqual(Top(pos, Bubble), Screen1366.y * 0.5f,
            "верхний край облачка не выходит за экран");
        Assert.Greater(Left(pos, Bubble), badge.x, "сторона при этом не меняется — места справа хватает");
    }

    [Test]
    public void AtTheBottomEdge_TheBubbleSlidesUp()
    {
        var badge = new Vector2(0f, -Screen1366.y * 0.5f + 10f);
        var pos = HintBubbleLayout.Beside(badge, 24f, Bubble, Screen1366);

        Assert.GreaterOrEqual(Bottom(pos, Bubble), -Screen1366.y * 0.5f,
            "нижний край облачка не уходит под экран");
    }

    [Test]
    public void InEveryCornerOfTheBaseScreen_TheBubbleStaysInside()
    {
        float hx = Screen1366.x * 0.5f;
        float hy = Screen1366.y * 0.5f;
        var corners = new[]
        {
            new Vector2(-hx + 12f, hy - 12f), new Vector2(hx - 12f, hy - 12f),
            new Vector2(-hx + 12f, -hy + 12f), new Vector2(hx - 12f, -hy + 12f),
        };

        foreach (var badge in corners)
        {
            var pos = HintBubbleLayout.Beside(badge, 24f, Bubble, Screen1366);
            Assert.GreaterOrEqual(Left(pos, Bubble), -hx, "левый край, значок " + badge);
            Assert.LessOrEqual(Right(pos, Bubble), hx, "правый край, значок " + badge);
            Assert.GreaterOrEqual(Bottom(pos, Bubble), -hy, "низ, значок " + badge);
            Assert.LessOrEqual(Top(pos, Bubble), hy, "верх, значок " + badge);
        }
    }

    [Test]
    public void ABubbleWiderThanTheScreen_IsCentred_NotPushedOut()
    {
        var huge = new Vector2(Screen1366.x + 200f, 90f);
        var pos = HintBubbleLayout.Beside(new Vector2(600f, 0f), 24f, huge, Screen1366);

        Assert.AreEqual(0f, pos.x, 0.01f,
            "текст, который шире экрана, нельзя разместить целиком — тогда он хотя бы "
            + "не съезжает в одну сторону");
    }
}
