using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Направляющие расстояний и прилипание к равному зазору — чистая геометрия.
/// Расстояние везде от ГРАНИ до ГРАНИ: от грани движущейся детали до обращённой к ней грани
/// соседа, и между соседями так же. На досках одной толщины это совпадает с разницей центров
/// за вычетом толщины, поэтому пользовательские 598 мм (центр—центр) и 582 мм (грань—грань)
/// описывают одну и ту же расстановку — см. тест на сцене Polka.</summary>
public class EqualGapSnapTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;
    private const float Tol = 0.0001f;
    private const float Threshold = 50f * Mm;

    private static AxisBox Box(int id, Vector3 centre, Vector3 sizeMm) =>
        new AxisBox(id, centre, Vector3.right, Vector3.up, Vector3.forward, sizeMm * (Mm * 0.5f));

    private static readonly Vector3 PolkaSize = new Vector3(600f, 2700f, 16f);
    private const float PolkaX = 3.176f;
    private const float PolkaY = 1.35f;

    private static AxisBox Polka(int id, float z) => Box(id, new Vector3(PolkaX, PolkaY, z), PolkaSize);

    private static AxisGuideIndex PolkaNeighbours() => new AxisGuideIndex(new[]
    {
        Polka(5, -0.647f),
        Polka(29, -1.245f),
        Polka(22, -3.612f),
        AxisBox.Aligned(100, new Vector3(2.9f, 0f, -0.639f), new Vector3(3.6f, 2.7f, -0.514f)),
        AxisBox.Aligned(101, new Vector3(2.9f, 0f, -3.745f), new Vector3(3.6f, 2.7f, -3.62f)),
    });

    [Test]
    public void PolkaScene_GapsAreSurfaceToSurface_582NotTheCentreDistance598()
    {
        var index = PolkaNeighbours();
        var polka30 = Polka(30, -1.843f);

        var cast = DistanceGuides.Cast(index, polka30, 2, 1);

        Assert.AreEqual(29, cast.Near.Id, "по +Z от Polka_30 первой стоит Polka_29");
        Assert.AreEqual(5, cast.Far.Id, "за ней — Polka_5");
        Assert.AreEqual(582f, cast.NearGap / Mm, 0.1f,
            "Polka_30 ↔ Polka_29: −1,253 − (−1,835) = 582 мм от грани до грани");
        Assert.AreEqual(582f, cast.FarGap / Mm, 0.1f,
            "Polka_29 ↔ Polka_5: −0,655 − (−1,237) = 582 мм. 598 мм пользователя — это шаг "
            + "ЦЕНТРОВ (−0,647 − (−1,245)); на досках одной толщины 16 мм это одно и то же место");
    }

    [Test]
    public void PolkaScene_DraggedTwentyMillimetresTowardPolka29_SnapsBackToCentreMinus1843()
    {
        var index = PolkaNeighbours();
        var free = new Vector3(PolkaX, PolkaY, -1.843f + 0.020f);
        var moving = Polka(30, free.z);

        var settled = EqualGapSnap.Resolve(index, moving, free, false, free,
            EqualGapSnap.AxisZ, Threshold, out int pulled);

        Assert.AreEqual(EqualGapSnap.AxisZ, pulled, "равный зазор обязан сработать по Z");
        Assert.AreEqual(-1.843f, settled.z, Tol,
            "цель прилипания — центр Polka_30 на z = −1843 мм (грань на −1835): тогда "
            + "Polka_30↔Polka_29 = Polka_29↔Polka_5 = 582 мм");
        Assert.AreEqual(PolkaX, settled.x, Tol, "по X равный зазор ничего не двигает");
    }

    [Test]
    public void PolkaScene_DraggedBeyondTheThreshold_IsNotPulled()
    {
        var index = PolkaNeighbours();
        var free = new Vector3(PolkaX, PolkaY, -1.843f + 0.080f);

        var settled = EqualGapSnap.Resolve(index, Polka(30, free.z), free, false, free,
            EqualGapSnap.AxisZ, Threshold, out int pulled);

        Assert.AreEqual(0, pulled, "80 мм разницы зазоров больше порога 50 мм — не прилипает");
        Assert.AreEqual(free.z, settled.z, Tol, "позиция остаётся свободной");
    }

    [Test]
    public void Resolve_AxisOutsideTheMask_IsNotPulled()
    {
        var index = PolkaNeighbours();
        var free = new Vector3(PolkaX, PolkaY, -1.843f + 0.020f);

        EqualGapSnap.Resolve(index, Polka(30, free.z), free, false, free,
            EqualGapSnap.AxisX, Threshold, out int pulled);

        Assert.AreEqual(0, pulled,
            "жест двигает деталь только по X — равный зазор по Z не имеет права её сдвинуть");
    }

    [Test]
    public void Resolve_FaceSnapCloserOnTheSameAxis_KeepsTheFaceSnap()
    {
        var index = PolkaNeighbours();
        var free = new Vector3(PolkaX, PolkaY, -1.843f + 0.020f);
        var faceSnapped = new Vector3(PolkaX, PolkaY, free.z + 0.005f);

        var settled = EqualGapSnap.Resolve(index, Polka(30, free.z), free, true, faceSnapped,
            EqualGapSnap.AxisZ, Threshold, out int pulled);

        Assert.AreEqual(0, pulled, "прилипание грани сдвигает на 5 мм, равный зазор на 20 — грань ближе");
        Assert.AreEqual(faceSnapped.z, settled.z, Tol, "и её результат остаётся");
    }

    [Test]
    public void Resolve_EqualGapCloserThanTheFaceSnap_WinsOnItsAxisOnly()
    {
        var index = PolkaNeighbours();
        var free = new Vector3(PolkaX, PolkaY, -1.843f + 0.020f);
        var faceSnapped = new Vector3(PolkaX + 0.010f, PolkaY, free.z + 0.040f);

        var settled = EqualGapSnap.Resolve(index, Polka(30, free.z), free, true, faceSnapped,
            EqualGapSnap.AxisX | EqualGapSnap.AxisZ, Threshold, out int pulled);

        Assert.AreEqual(EqualGapSnap.AxisZ, pulled, "по Z равный зазор (20 мм) ближе грани (40 мм)");
        Assert.AreEqual(-1.843f, settled.z, Tol, "по Z — равный зазор");
        Assert.AreEqual(faceSnapped.x, settled.x, Tol, "по X остаётся сдвиг прилипания грани");
    }

    [Test]
    public void TryAlong_ExistingGapZero_IsNotAnEqualGap()
    {
        var index = new AxisGuideIndex(new[]
        {
            Box(1, new Vector3(0f, 1f, 0.5f), new Vector3(600f, 2000f, 16f)),
            Box(2, new Vector3(0f, 1f, 0.633f), new Vector3(600f, 2000f, 250f)),
        });
        var moving = Box(9, new Vector3(0f, 1f, 0f), new Vector3(600f, 2000f, 16f));

        var cast = DistanceGuides.Cast(index, moving, 2, 1);

        Assert.AreEqual(0f, cast.FarGap, Tol, "посылка: доска вплотную к стене");
        Assert.IsFalse(EqualGapSnap.TryAlong(cast, Threshold, out _),
            "нулевой зазор между соседями — это касание, его держит обычное прилипание грани; "
            + "«равный» ему зазор притянул бы деталь вплотную через полметра");
    }

    [Test]
    public void TryForFace_ResizedFace_StopsAtTheEqualGap()
    {
        var index = PolkaNeighbours();
        var centre = new Vector3(PolkaX, PolkaY, -1.843f);
        var faceAfterRawDelta = new Vector3(PolkaX, PolkaY, -1.835f + 0.030f);

        bool found = EqualGapSnap.TryForFace(index, centre, Vector3.forward, faceAfterRawDelta,
            Threshold, out float alongNormal);

        Assert.IsTrue(found, "растянутая на 30 мм грань в пределах порога от равного зазора");
        Assert.AreEqual(-0.030f, alongNormal, Tol,
            "грань возвращается туда, где её зазор до Polka_29 равен 582 мм");
    }

    [Test]
    public void TryForFace_TurnedNormal_IsNotAnAxisGuide()
    {
        var index = PolkaNeighbours();
        var normal = new Vector3(0.6f, 0f, 0.8f);

        bool found = EqualGapSnap.TryForFace(index, Vector3.zero, normal, normal * 0.3f,
            Threshold, out _);

        Assert.IsFalse(found, "направляющие идут только по мировым осям; косая грань их не получает");
    }

    [Test]
    public void ResizeMath_WithSpacing_EndsAtTheEqualGapThickness()
    {
        var index = PolkaNeighbours();
        var self = ElementGeometry.Box("Polka_30", new Vector3(PolkaX, PolkaY, -1.843f), PolkaSize * Mm);
        var face = self.Faces[4];

        ResizeMath.Compute(new Vector3Int(600, 2700, 16), 2, face.normal, face.center,
            face.rightAxis, face.upAxis, face.size, new Vector3(PolkaX, PolkaY, -1.843f),
            16f * Mm, 0.030f, new List<ElementGeometry>(), self, true, Threshold,
            out var dims, out _, out bool snapped, index);

        Assert.IsTrue(snapped, "ресайз грани +Z обязан прилипнуть к равному зазору");
        Assert.AreEqual(16, dims.z, "толщина возвращается к 16 мм: при ней зазоры равны");
    }

    [Test]
    public void ResizeMath_WithoutSpacing_KeepsTheFreeDelta()
    {
        var self = ElementGeometry.Box("Polka_30", new Vector3(PolkaX, PolkaY, -1.843f), PolkaSize * Mm);
        var face = self.Faces[4];

        ResizeMath.Compute(new Vector3Int(600, 2700, 16), 2, face.normal, face.center,
            face.rightAxis, face.upAxis, face.size, new Vector3(PolkaX, PolkaY, -1.843f),
            16f * Mm, 0.030f, new List<ElementGeometry>(), self, true, Threshold,
            out var dims, out _, out bool snapped);

        Assert.IsFalse(snapped, "пара к тесту выше: без индекса прилипать не к чему");
        Assert.AreEqual(46, dims.z, "толщина растёт на свободные 30 мм");
    }

    [Test]
    public void Collect_PolkaAtEqualGap_DrawsTheSecondLineAsAContinuation()
    {
        var lines = new List<GuideLine>();
        DistanceGuides.Collect(PolkaNeighbours(), Polka(30, -1.843f), lines);

        var plusZ = lines.FindAll(l => l.A.z > -1.84f && l.B.z > l.A.z);
        Assert.AreEqual(2, plusZ.Count, "по +Z две линии: до Polka_29 и Polka_29 ↔ Polka_5");
        Assert.IsTrue(plusZ[0].EqualGap && plusZ[1].EqualGap, "обе отмечены как равный зазор");
        Assert.AreEqual(582f, plusZ[0].Length / Mm, 0.1f, "первая — текущий зазор");
        Assert.AreEqual(582f, plusZ[1].Length / Mm, 0.1f, "вторая — существующий зазор, своя подпись");
        Assert.AreEqual(-1.237f, plusZ[1].A.z, Tol,
            "вторая начинается от дальней грани Polka_29 — продолжение первой по той же прямой");
        Assert.AreEqual(plusZ[0].A.x, plusZ[1].A.x, Tol, "на той же прямой");
    }

    [Test]
    public void Collect_PolkaOffTheEqualGap_DrawsOnlyTheDistanceLine()
    {
        var lines = new List<GuideLine>();
        DistanceGuides.Collect(PolkaNeighbours(), Polka(30, -1.843f + 0.080f), lines);

        var plusZ = lines.FindAll(l => l.B.z > l.A.z && l.A.z > -1.84f);
        Assert.AreEqual(1, plusZ.Count, "зазоры не равны — вторая линия обязана исчезнуть");
        Assert.IsFalse(plusZ[0].EqualGap, "и первая не помечена равной");
        Assert.AreEqual(502f, plusZ[0].Length / Mm, 0.1f, "582 − 80 мм");
    }

    [Test]
    public void Collect_TouchingAndEmptyDirections_DrawNothing()
    {
        var lines = new List<GuideLine>();
        var index = new AxisGuideIndex(new[]
        {
            Box(1, new Vector3(0.308f, 1f, 0f), new Vector3(600f, 2000f, 16f)),
        });

        DistanceGuides.Collect(index, Box(9, Vector3.up, new Vector3(16f, 2000f, 600f)), lines);

        Assert.AreEqual(0, lines.Count,
            "сосед вплотную по +X даёт нулевую линию, остальные пять направлений пусты в пределах "
            + "10 м — рисовать нечего");
    }

    [Test]
    public void Collect_MovingBoxPresentInTheIndex_IsNotMeasuredAgainstItself()
    {
        var self = Box(9, new Vector3(0f, 1f, 0f), new Vector3(600f, 2000f, 16f));
        var lines = new List<GuideLine>();

        DistanceGuides.Collect(new AxisGuideIndex(new[] { self }), self, lines);

        Assert.AreEqual(0, lines.Count,
            "луч стартует из центра детали: её собственная коробка начинается позади её грани и "
            + "отбрасывается тем же правилом, что и стена-хозяин окна");
    }

    [Test]
    public void Collect_FloorBelowAHangingCabinet_GivesTheHeightAboveTheFloor()
    {
        var index = new AxisGuideIndex(new[]
        {
            AxisBox.Aligned(1, new Vector3(-5f, -0.1f, -5f), new Vector3(5f, 0f, 5f)),
        });
        var cabinet = Box(9, new Vector3(0f, 1.76f, 0f), new Vector3(600f, 720f, 300f));
        var lines = new List<GuideLine>();

        DistanceGuides.Collect(index, cabinet, lines);

        Assert.AreEqual(1, lines.Count, "вниз — пол, остальные направления пусты");
        Assert.AreEqual(1400f, lines[0].Length / Mm, 0.1f,
            "вертикальная направляющая: от дна шкафа (1,4 м) до верха пола");
    }
}
