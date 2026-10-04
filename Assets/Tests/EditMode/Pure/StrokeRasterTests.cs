using System.Collections.Generic;
using KitchenDesigner.Core.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>Растеризация штриха иконки (<c>StrokeRaster</c>): расстояние до отрезка даёт
/// сглаженный край и круглые концы без отдельной геометрии. Строка пикселей 0 — верх.</summary>
public class StrokeRasterTests
{
    private const float Grid = 20f;
    private const int Px = 80;

    private static byte[] Line(float x0, float y0, float x1, float y1, float width = 1.6f) =>
        StrokeRaster.Alpha(new List<List<Vector2>> { new List<Vector2> { new Vector2(x0, y0), new Vector2(x1, y1) } },
            Grid, Px, width);

    private static byte At(byte[] alpha, int x, int y) => alpha[y * Px + x];

    [Test]
    public void AHorizontalLine_IsFullyInkedOnItsAxis_AndEmptyFarAway()
    {
        var alpha = Line(2, 10, 18, 10);

        Assert.AreEqual(255, At(alpha, 40, 39), "пиксель на оси линии закрашен целиком");
        Assert.AreEqual(255, At(alpha, 40, 40));
        Assert.AreEqual(0, At(alpha, 40, 10), "в шести единицах сетки от оси пусто");
        Assert.AreEqual(0, At(alpha, 40, 70));
    }

    [Test]
    public void TheEdge_IsAntialiased_NotAHardStep()
    {
        var alpha = Line(2, 10, 18, 10, 1.6f);

        int partial = 0;
        for (int y = 0; y < Px; y++)
            if (At(alpha, 40, y) > 0 && At(alpha, 40, y) < 255) partial++;

        Assert.GreaterOrEqual(partial, 1, "на границе штриха есть полупрозрачный пиксель — край сглажен");
        Assert.LessOrEqual(partial, 4, "и их не больше пары на край: размытие не должно расползаться");
    }

    [Test]
    public void StrokeThickness_FollowsTheWidthInGridUnits()
    {
        int Thickness(float width)
        {
            var alpha = Line(2, 10, 18, 10, width);
            int total = 0;
            for (int y = 0; y < Px; y++) total += At(alpha, 40, y);
            return total / 255;
        }

        Assert.AreEqual(6.4f, Thickness(1.6f), 1.1f, "1,6 единицы сетки × 4 текселя = 6,4 текселя поперёк");
        Assert.Greater(Thickness(3.2f), Thickness(1.6f) + 4, "вдвое шире штрих — вдвое толще след");
    }

    [Test]
    public void TheEndsOfALine_AreRound_NotCutSquare()
    {
        var alpha = Line(10, 10, 10, 10);

        Assert.AreEqual(255, At(alpha, 40, 40), "нулевой отрезок — точка");
        Assert.AreEqual(0, At(alpha, 40 + 5, 40 + 5), "угол воображаемого квадрата 1,6×1,6 пуст: конец круглый");
    }

    [Test]
    public void ASinglePointStroke_PaintsADot()
    {
        var alpha = StrokeRaster.Alpha(new List<List<Vector2>> { new List<Vector2> { new Vector2(10, 10) } },
            Grid, Px, 1.6f);

        Assert.AreEqual(255, At(alpha, 40, 40));
    }

    [Test]
    public void OverlappingStrokes_DoNotAddUpBeyondFullInk()
    {
        var strokes = new List<List<Vector2>>
        {
            new List<Vector2> { new Vector2(2, 10), new Vector2(18, 10) },
            new List<Vector2> { new Vector2(2, 10), new Vector2(18, 10) },
        };

        var alpha = StrokeRaster.Alpha(strokes, Grid, Px, 1.6f);

        Assert.AreEqual(255, At(alpha, 40, 40));
    }

    [Test]
    public void AStrokeNearTheBorder_IsClippedByTheCanvas_NotWrapped()
    {
        var alpha = Line(0, 0, 0, 20);

        Assert.AreEqual(255, At(alpha, 0, 40), "левый столбец закрашен");
        Assert.AreEqual(0, At(alpha, Px - 1, 40), "правый край не получил зеркального следа");
    }

    [Test]
    public void TheResult_IsAPixelsSquareBuffer()
    {
        Assert.AreEqual(Px * Px, Line(2, 2, 3, 3).Length);
    }
}
