using System;
using System.Collections.Generic;
using System.Linq;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Разбор путей иконок (<c>StrokePath</c>): тот же язык, что у SVG-атрибута d, плюс
/// команда «O cx cy r» для окружности. Иконки тулбара взяты из макета shell.html дословно,
/// поэтому разбор обязан понимать сжатую запись («a5 5 0 010 10» — флаги дуги слеплены с
/// числом) и относительные команды; ошибка разбора — это тихо кривая иконка, а не падение.</summary>
public class StrokePathTests
{
    private static void AssertPoint(Vector2 expected, Vector2 actual, string why = "", float delta = 0.001f)
    {
        Assert.AreEqual(expected.x, actual.x, delta, "x: " + why);
        Assert.AreEqual(expected.y, actual.y, delta, "y: " + why);
    }

    [Test]
    public void Parse_RelativeAndAbsoluteLines_BuildOneClosedOutline()
    {
        var strokes = StrokePath.Parse("M5 2h7l3 3v13H5z");

        Assert.AreEqual(1, strokes.Count);
        var p = strokes[0];
        Assert.AreEqual(6, p.Count, "пять вершин и возврат в начало по «z»");
        AssertPoint(new Vector2(5, 2), p[0]);
        AssertPoint(new Vector2(12, 2), p[1], "h7 — относительно");
        AssertPoint(new Vector2(15, 5), p[2], "l3 3 — относительно");
        AssertPoint(new Vector2(15, 18), p[3], "v13 — относительно");
        AssertPoint(new Vector2(5, 18), p[4], "H5 — абсолютно: x равен 5, а не 20");
        AssertPoint(new Vector2(5, 2), p[5], "z замыкает контур в начальную точку");
    }

    [Test]
    public void Parse_CoordinatePairsAfterMoveTo_AreImplicitLines()
    {
        var p = StrokePath.Parse("M0 0 4 0 4 4")[0];

        Assert.AreEqual(3, p.Count);
        AssertPoint(new Vector2(4, 0), p[1]);
        AssertPoint(new Vector2(4, 4), p[2]);
    }

    [Test]
    public void Parse_EachMoveTo_StartsANewStroke()
    {
        var strokes = StrokePath.Parse("M3 5h14 M3 10h14 M3 15h14");

        Assert.AreEqual(3, strokes.Count, "три строки гамбургера — три независимых штриха, без перемычек");
        Assert.AreEqual(new[] { 5f, 10f, 15f }, strokes.Select(s => s[0].y).ToArray());
    }

    [Test]
    public void Parse_NumbersGluedBySignOrDot_AreSeparatedLikeInSvg()
    {
        var neg = StrokePath.Parse("M3 9l4 4-4 4")[0];
        var dot = StrokePath.Parse("M10 14.5v.5")[0];

        AssertPoint(new Vector2(7, 13), neg[1]);
        AssertPoint(new Vector2(3, 17), neg[2], "«4-4» — это 4 и −4, а не одно число");
        AssertPoint(new Vector2(10, 15), dot[1], "«.5» без нуля — число 0,5");
    }

    [Test]
    public void Parse_ArcWithPackedFlags_StaysOnTheCircleAndEndsAtTheTarget()
    {
        var p = StrokePath.Parse("M3 9h9a5 5 0 010 10H9")[0];
        var center = new Vector2(12, 14);

        var arc = p.Skip(2).Take(p.Count - 3).ToList();
        Assert.Greater(arc.Count, 8, "дуга разложена на отрезки, а не прямой хордой");
        foreach (var point in arc)
            Assert.AreEqual(5f, Vector2.Distance(center, point), 0.01f, "каждая точка дуги на радиусе 5 от центра (12;14)");
        AssertPoint(new Vector2(12, 19), arc[arc.Count - 1], "дуга приходит ровно в заданную точку");
        Assert.AreEqual(17f, arc.Max(a => a.x), 0.05f, "флаг sweep=1 ведёт дугу вправо, а не влево");
        AssertPoint(new Vector2(9, 19), p[p.Count - 1], "после дуги команда H продолжает от её конца");
    }

    [Test]
    public void Parse_SweepFlagZero_BulgesTheOtherWay()
    {
        var right = StrokePath.Parse("M12 9a5 5 0 010 10")[0];
        var left = StrokePath.Parse("M12 9a5 5 0 000 10")[0];

        Assert.Greater(right.Max(a => a.x), 16.9f);
        Assert.Less(left.Min(a => a.x), 7.1f, "sweep=0 — дуга уходит влево");
    }

    [Test]
    public void Parse_LargeArcFlag_TakesTheLongWay()
    {
        var small = StrokePath.Parse("M15 9A5.5 5.5 0 0 1 5 9")[0];
        var large = StrokePath.Parse("M15 9A5.5 5.5 0 1 1 5 9")[0];

        float smallDepth = small.Max(a => a.y) - small.Min(a => a.y);
        float largeDepth = large.Max(a => a.y) - large.Min(a => a.y);
        Assert.Greater(largeDepth, smallDepth + 4f, "большая дуга уходит дальше от хорды");
    }

    [Test]
    public void Parse_ArcWhoseRadiusIsTooSmall_IsScaledUpToReachTheTarget()
    {
        var p = StrokePath.Parse("M0 0a1 1 0 010 10")[0];

        AssertPoint(new Vector2(0, 10), p[p.Count - 1], "радиус 1 не достаёт на 10 — SVG увеличивает его, а не рвёт контур");
    }

    [Test]
    public void Parse_Circle_IsAClosedRingAtTheGivenRadius()
    {
        var ring = StrokePath.Parse("O10 10 4")[0];

        Assert.GreaterOrEqual(ring.Count, 16);
        AssertPoint(ring[0], ring[ring.Count - 1], "кольцо замкнуто");
        foreach (var point in ring)
            Assert.AreEqual(4f, Vector2.Distance(new Vector2(10, 10), point), 0.001f);
    }

    [Test]
    public void Parse_MoveToAfterAClosedShape_IsRelativeToTheStartOfThatShape()
    {
        var strokes = StrokePath.Parse("M2 2h4v4z m1 1h2");

        AssertPoint(new Vector2(3, 3), strokes[1][0], "m после z отсчитывается от точки, куда вернулся z");
    }

    [Test]
    public void Parse_AnUnsupportedCommand_IsRefusedByName_NotDrawnWrong()
    {
        var ex = Assert.Throws<FormatException>(() => StrokePath.Parse("M0 0c1 1 2 2 3 3"));

        StringAssert.Contains("'c'", ex!.Message);
    }

    [Test]
    public void Parse_DataWithoutALeadingCommand_IsRefused()
    {
        Assert.Throws<FormatException>(() => StrokePath.Parse("5 5 10 10"));
    }

    [Test]
    public void Parse_AnArcFlagThatIsNotZeroOrOne_IsRefused()
    {
        Assert.Throws<FormatException>(() => StrokePath.Parse("M0 0a5 5 0 2 1 4 4"));
    }

    [Test]
    public void Parse_EmptyData_GivesNoStrokes()
    {
        Assert.IsEmpty(StrokePath.Parse(""));
    }
}
