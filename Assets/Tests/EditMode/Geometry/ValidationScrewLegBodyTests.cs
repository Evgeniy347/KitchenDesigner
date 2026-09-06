using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Винтовая опора описана ДВУМЯ телами: пятак Ø25 × 8 и резьба Ø6 × 37.
/// Одной коробкой её описать нельзя — габарит Ø25 × 45 закрашивает пустоту вокруг
/// резьбы, и всё, что стоит рядом с ней, но не касается её, ядро объявляло
/// пересечением. Три из четырёх нарушений у опоры в рабочем проекте были ровно
/// этим: сосед в габарите пятака, но в сантиметре от резьбы.
///
/// Тесты идут парой: «сосед в 3,5 мм от резьбы — не COL-01» и «сосед, режущий
/// резьбу, — COL-01» на ОДНОЙ сцене, где меняется только X соседа. Без второго
/// первый был бы зелёным и на коде, который резьбу не проверяет вовсе.
///
/// Сосед стоит в X = 6,5..24,5 мм: это ВНУТРИ старого габарита Ø25
/// (±12,5 мм) и снаружи резьбы (±3 мм). Числа выбраны так, чтобы тест был
/// красным на однокоробочном ядре.</summary>
public class ValidationScrewLegBodyTests
{
    private const float MM = 0.001f;

    private const int Floor = 0;
    private const int Host = 1;
    private const int Neighbour = 2;
    private const int Leg = 3;

    private static Vector3[] Corners(Vector3 center, Vector3 size)
    {
        var half = size * 0.5f;
        var verts = new Vector3[8];
        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    verts[i++] = center + new Vector3(half.x * sx, half.y * sy, half.z * sz);
        return verts;
    }

    private static ValidationElement Part(string name, Vector3 centerMm, Vector3 sizeMm,
        ElementKind kind = ElementKind.None)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        return new ValidationElement(ElementGeometry.Box(name, center, size), Corners(center, size),
            kind, 0, null, Span.FromCenter(center.y, size.y), -1);
    }

    private static ValidationElement ScrewLeg(string? hostName, int hostIndex)
    {
        Vector3 baseCentre = new Vector3(0, 4, 0) * MM;
        Vector3 baseSize = new Vector3(25, 8, 25) * MM;
        var pad = ElementGeometry.Box("Leg", baseCentre, baseSize);
        var thread = ElementGeometry.Box("Leg" + "/thread",
            new Vector3(0, 26.5f, 0) * MM, new Vector3(6, 37, 6) * MM);
        return new ValidationElement(pad, Corners(baseCentre, baseSize), ElementKind.ScrewLeg,
            0, hostName, Span.FromCenter(22.5f * MM, 45 * MM), -1, thread, true, hostIndex);
    }

    private static CoreValidationResult SceneWithNeighbourAtX(float centreXMm,
        string? hostName = "Host", int hostIndex = Host)
    {
        return ValidationCore.Validate(new[]
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Part("Host", new Vector3(0, 45, 0), new Vector3(40, 30, 400)),
            Part("Neighbour", new Vector3(centreXMm, 19, 0), new Vector3(18, 18, 200)),
            ScrewLeg(hostName, hostIndex),
        });
    }

    private const float ClearOfTheThread = 15.5f;

    private const float ThroughTheThread = 7f;

    private static int OverlapsBetween(CoreValidationResult r, int i, int j) =>
        r.Diagnostics == null ? 0 : r.Diagnostics.Count(d => d.Kind == ViolationKind.Overlap
            && ((d.Element == i && d.Other == j) || (d.Element == j && d.Other == i)));

    [Test]
    public void NeighbourBesideTheThread_InsideThePadEnvelope_IsNotOverlap()
    {
        var r = SceneWithNeighbourAtX(ClearOfTheThread);

        Assert.AreEqual(0, OverlapsBetween(r, Leg, Neighbour),
            "деталь стоит в 3,5 мм от резьбы Ø6 — металла между ними нет ни в одной точке; "
            + "COL-01 здесь означал бы, что опору всё ещё описывает габарит Ø25");
    }

    [Test]
    public void NeighbourCuttingTheThread_IsOverlap()
    {
        var r = SceneWithNeighbourAtX(ThroughTheThread);

        Assert.AreEqual(1, OverlapsBetween(r, Leg, Neighbour),
            "та же деталь, сдвинутая на резьбу: положительный контроль к тесту выше");
    }

    [Test]
    public void SecondElementOnTheThread_IsOverlap_NotASecondHost()
    {
        var r = SceneWithNeighbourAtX(ThroughTheThread);

        Assert.AreEqual(0, OverlapsBetween(r, Leg, Host),
            "хозяин ровно один — тот, чей индекс объявлен");
        Assert.AreEqual(1, OverlapsBetween(r, Leg, Neighbour),
            "резьба, вошедшая во второй элемент, — коллизия, а не вторая связь");
    }

    [Test]
    public void ThreadInsideItsHost_IsNotOverlap()
    {
        var r = SceneWithNeighbourAtX(ClearOfTheThread);

        Assert.AreEqual(0, OverlapsBetween(r, Leg, Host),
            "в хозяина резьба и вкручена");
    }

    [Test]
    public void LegAndHost_AreInFaceToFaceContact_ThroughTheThread()
    {
        var r = SceneWithNeighbourAtX(ClearOfTheThread);

        Assert.IsTrue(r.Contacts.Any(c => c.IsFaceToFace
                && ((c.A == Leg && c.B == Host) || (c.A == Host && c.B == Leg))),
            "пятак хозяина не касается — опора держится резьбой, и связность обязана "
            + "считать это опорой, иначе опора становится «висит в воздухе»");
    }

    [Test]
    public void UnpairedLeg_ThreadInsideABoard_IsOverlap()
    {
        var r = SceneWithNeighbourAtX(ClearOfTheThread, hostName: null,
            hostIndex: ValidationElement.NoIndex);

        Assert.AreEqual(1, OverlapsBetween(r, Leg, Host),
            "без связи хозяин перестаёт быть хозяином: резьба внутри чужой детали — COL-01");
    }

    [Test]
    public void PadRestingOnTheFloor_IsNotOverlap()
    {
        var r = SceneWithNeighbourAtX(ClearOfTheThread);

        Assert.AreEqual(0, OverlapsBetween(r, Leg, Floor),
            "пятак стоит НА полу, а не в полу");
    }
}
