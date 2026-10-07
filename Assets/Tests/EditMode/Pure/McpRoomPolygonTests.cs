using NUnit.Framework;
using KitchenDesigner.Core.MCP;

public class McpRoomPolygonTests
{
    private static readonly int[] Square = { 0, 0, 4000, 0, 4000, 3000, 0, 3000 };

    private static readonly int[] LShape = { 0, 0, 4000, 0, 4000, 1500, 2000, 1500, 2000, 3000, 0, 3000 };

    [TestCase(2000f, 1500f, true)]
    [TestCase(10f, 10f, true)]
    [TestCase(-10f, 1500f, false)]
    [TestCase(4010f, 1500f, false)]
    [TestCase(2000f, 3010f, false)]
    [TestCase(2000f, -10f, false)]
    public void Contains_PointAgainstARectangularRoom(float xMm, float zMm, bool expected)
    {
        Assert.AreEqual(expected, McpRoomPolygon.Contains(Square, xMm, zMm),
            $"точка ({xMm}, {zMm}) мм относительно комнаты 4000x3000");
    }

    [Test]
    public void Contains_TheNotchOfAnLShapedRoom_IsOutside()
    {
        Assert.IsTrue(McpRoomPolygon.Contains(LShape, 1000f, 2500f), "нога буквы Г внутри");
        Assert.IsFalse(McpRoomPolygon.Contains(LShape, 3000f, 2500f),
            "вырез буквы Г снаружи: габаритный прямоугольник этого не различает, а полигон обязан");
    }

    [TestCase(null)]
    [TestCase(new int[0])]
    [TestCase(new[] { 0, 0, 100, 0 })]
    [TestCase(new[] { 0, 0, 100, 0, 100 })]
    public void Contains_ADegeneratePolygon_ContainsNothing(int[]? polygon)
    {
        Assert.IsFalse(McpRoomPolygon.Contains(polygon, 50f, 10f),
            "меньше трёх вершин или нечётное число координат — не комната: ответ «нет», а не исключение");
    }
}
