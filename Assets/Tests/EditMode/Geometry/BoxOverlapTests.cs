using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Пересечение двух ОРИЕНТИРОВАННЫХ коробок (SAT). До него проверка
/// раскрытия сравнивала осевые габариты, и тонкая дверь 670 мм на повороте
/// раздувала свой AABB в квадрат 670×670: «столкновение» находилось между
/// двумя ПРОТИВОПОЛОЖНЫМИ углами коробки, где двери нет. Здесь проверяется,
/// что повёрнутая коробка меряется по себе, а не по своему габариту.</summary>
public class BoxOverlapTests
{
    private static readonly Quaternion Yaw45 = new Quaternion(0f, 0.38268343f, 0f, 0.92387953f);

    private static OrientedBox Box(Vector3 center, Vector3 half) =>
        new OrientedBox(center, Quaternion.identity, half);

    [Test]
    public void SeparatedBoxes_ReportNoPenetration()
    {
        var a = Box(Vector3.zero, new Vector3(0.5f, 0.5f, 0.01f));
        var b = Box(new Vector3(0f, 0f, 0.5f), new Vector3(0.5f, 0.5f, 0.01f));

        Assert.AreEqual(0f, BoxOverlap.PenetrationUnits(a, b), 0f,
            "коробки разнесены — глубина ноль, а не «почти ноль»");
    }

    [Test]
    public void OverlappingBoxes_ReportTheThinnestOverlap()
    {
        var a = Box(Vector3.zero, new Vector3(0.5f, 0.5f, 0.1f));
        var b = Box(new Vector3(0f, 0f, 0.15f), new Vector3(0.5f, 0.5f, 0.1f));

        Assert.AreEqual(0.05f, BoxOverlap.PenetrationUnits(a, b), 1e-5f,
            "глубина проникновения — минимальное перекрытие по разделяющим осям");
    }

    [Test]
    public void ThinBoxTurned45Degrees_MissesTheNeighbourItsAabbWouldHit()
    {
        var turned = new OrientedBox(Vector3.zero, Yaw45, new Vector3(0.5f, 0.5f, 0.009f));

        // Угол осевого габарита повёрнутой коробки: x и z разом по +0,36.
        // Самой коробки там нет — она идёт диагональю через начало координат.
        var atTheAabbCorner = Box(new Vector3(0.34f, 0f, 0.34f), new Vector3(0.02f, 0.5f, 0.02f));

        Assert.AreEqual(0f, BoxOverlap.PenetrationUnits(turned, atTheAabbCorner), 0f,
            "сосед в углу габарита повёрнутой двери — не столкновение: "
            + "именно на этом ложном срабатывании фасад замирал на 18° вместо 90°");
    }

    [Test]
    public void ThinBoxTurned45Degrees_HitsTheNeighbourOnItsDiagonal()
    {
        var turned = new OrientedBox(Vector3.zero, Yaw45, new Vector3(0.5f, 0.5f, 0.009f));
        var onTheDiagonal = Box(new Vector3(0.25f, 0f, -0.25f), new Vector3(0.02f, 0.5f, 0.02f));

        Assert.Greater(BoxOverlap.PenetrationUnits(turned, onTheDiagonal), 0f,
            "контроль к предыдущему: сосед НА диагонали повёрнутой коробки — "
            + "настоящее столкновение, иначе тест ловил бы «не пересекается никогда»");
    }
}
