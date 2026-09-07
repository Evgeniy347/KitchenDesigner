using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Какие стороны обвязки проёма скрывает соседний проём на той же стене.
///
/// Здесь живёт причина, которая раньше была комментарием в WindowElement.cs (и
/// вторым его экземпляром в DoorElement.cs): без скрытия обвязка одного окна
/// видна в проёме соседнего — между ними встаёт лишняя полоса.</summary>
public class OpeningNeighbourSidesTests
{
    private static Rect Box(float xMin, float yMin, float xMax, float yMax)
        => Rect.MinMaxRect(xMin, yMin, xMax, yMax);

    private static readonly Rect Self = Box(-1f, 0f, 1f, 2f);

    [Test]
    public void NeighbourAtTheSameHeight_HidesTheSideItTouches()
    {
        var onTheLeft = Box(-3f, 0.5f, -1f, 1.5f);
        var onTheRight = Box(1f, 0.5f, 3f, 1.5f);

        Assert.AreEqual(OpeningNeighbourSides.Left,
            OpeningNeighbourSides.HiddenBy(Self, onTheLeft),
            "сосед слева упирается в левую обвязку — она бы торчала полосой в его проёме");
        Assert.AreEqual(OpeningNeighbourSides.Right,
            OpeningNeighbourSides.HiddenBy(Self, onTheRight),
            "и симметрично справа");
    }

    [Test]
    public void NeighbourStackedAbove_Or_Below_HidesTopOrBottom()
    {
        var above = Box(-0.5f, 2f, 0.5f, 3.5f);
        var below = Box(-0.5f, -1.5f, 0.5f, 0f);

        Assert.AreEqual(OpeningNeighbourSides.Top,
            OpeningNeighbourSides.HiddenBy(Self, above),
            "сосед СВЕРХУ упирается в верхнюю обвязку — скрывать надо ВЕРХ, а не низ");
        Assert.AreEqual(OpeningNeighbourSides.Bottom,
            OpeningNeighbourSides.HiddenBy(Self, below),
            "и симметрично снизу — ровно как у левого и правого соседа");
    }

    [Test]
    public void DistantNeighbour_HidesNothing()
    {
        var farAway = Box(5f, 0.5f, 7f, 1.5f);

        Assert.AreEqual(0, OpeningNeighbourSides.HiddenBy(Self, farAway),
            "положительный контроль к тестам выше: сосед на другом конце стены обвязку "
            + "не закрывает, и она обязана остаться на месте");
    }

    [Test]
    public void NeighbourOverlappingInBothAxes_HidesNothing()
    {
        var onTop = Box(-0.5f, 0.5f, 0.5f, 1.5f);

        Assert.AreEqual(0, OpeningNeighbourSides.HiddenBy(Self, onTop),
            "проём внутри проёма — это ошибка расстановки, а не примыкание: срезать "
            + "обвязку здесь не по чему");
    }

    [Test]
    public void EmptyNeighbour_HidesNothing()
    {
        Assert.AreEqual(0, OpeningNeighbourSides.HiddenBy(Self, Rect.zero),
            "вырожденный сосед (его в списке нет или это мы сами) стороны не скрывает");
    }

    [Test]
    public void LocalRect_IsCentredOnTheOpening_AndSizedInWallLocalUnits()
    {
        var wallGo = new GameObject("Стена");
        var openingGo = new GameObject("Окно");
        try
        {
            wallGo.transform.SetPositionAndRotation(new Vector3(1f, 0f, 2f),
                ManagedRotation.Euler(0f, 90f, 0f));
            var opening = openingGo.AddComponent<KitchenElement>();
            opening.DimensionsMM = new Vector3Int(800, 1200, 100);
            openingGo.transform.position = wallGo.transform.TransformPoint(new Vector3(0.3f, 1.1f, 0f));

            var rect = OpeningNeighbourSides.LocalRect(wallGo.transform, opening);

            Assert.AreEqual(0.3f, rect.center.x, 1e-4f, "прямоугольник берётся в осях СТЕНЫ");
            Assert.AreEqual(1.1f, rect.center.y, 1e-4f);
            Assert.AreEqual(0.8f, rect.width, 1e-4f, "ширина проёма в юнитах");
            Assert.AreEqual(1.2f, rect.height, 1e-4f);
        }
        finally
        {
            Object.DestroyImmediate(openingGo);
            Object.DestroyImmediate(wallGo);
        }
    }
}
