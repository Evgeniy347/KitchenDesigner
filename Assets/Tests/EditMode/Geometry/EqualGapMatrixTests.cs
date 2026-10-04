using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Матрица направляющих и равного зазора: ось (X, Y, Z) × направление (+/−) ×
/// поворот движущейся детали на 90° вокруг вертикали × форма первого соседа × форма второго.
///
/// Каждый случай описан в «своей» системе координат вдоль луча: u — вдоль оси луча (от
/// центра движущейся детали в сторону направления), v и w — поперёк. Ожидаемые числа
/// считаются здесь простой арифметикой осевых интервалов, без AxisBox и без индекса, —
/// поэтому реализация, перепутавшая оси или меряющая от центров вместо граней, расходится
/// с ними: толщины соседей и полугабарит детали вдоль каждой оси здесь РАЗНЫЕ (16 и 400 мм,
/// 300/360/150 мм), и замена «грань → центр» сдвигает каждый ответ на свою величину.
///
/// Попадание — когда поперечное сечение соседа накрывает ось луча, проходящую через центр
/// движущейся детали. Сосед, перекрывающий проекцию детали только краем, мимо этой оси, —
/// не попадание: так задана механика (лучи из центра), и это закреплено отдельным случаем.</summary>
public class EqualGapMatrixTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;
    private const float Tol = 0.0002f;
    private const float Threshold = 50f * Mm;
    private const float FirstGapMm = 470f;
    private const float ExistingGapMm = 450f;

    private static readonly Vector3 MovingCentre = new Vector3(1.1f, 1.5f, -2.3f);
    private static readonly Vector3 MovingLocalSizeMm = new Vector3(600f, 720f, 300f);
    private static readonly Quaternion TurnedAboutY = new Quaternion(0f, Mathf.Sqrt(0.5f), 0f, Mathf.Sqrt(0.5f));

    public sealed class Shape
    {
        public readonly string Name;
        public readonly float ThicknessMm;
        public readonly float CrossVMm;
        public readonly float CrossWMm;
        public readonly float OffsetVMm;
        public readonly float OffsetWMm;
        public readonly bool Turned;

        public Shape(string name, float thickness, float crossV, float crossW, float offsetV,
            float offsetW, bool turned = false)
        {
            Name = name;
            ThicknessMm = thickness;
            CrossVMm = crossV;
            CrossWMm = crossW;
            OffsetVMm = offsetV;
            OffsetWMm = offsetW;
            Turned = turned;
        }

        public override string ToString() => Name;
    }

    private static readonly Shape[] OnTheLine =
    {
        new Shape("thin-wide", 16f, 2000f, 1800f, 0f, 0f),
        new Shape("thick-narrow", 400f, 100f, 120f, 0f, 0f),
        new Shape("long-offset", 60f, 1200f, 900f, 500f, -380f),
        new Shape("partial-overlap", 25f, 400f, 300f, 170f, 120f),
        new Shape("turned-90", 200f, 500f, 700f, -100f, 60f, turned: true),
    };

    private static readonly Shape BesideTheLine = new Shape("beside-the-line", 30f, 200f, 200f, 260f, 0f);

    public sealed class Case
    {
        public readonly int Axis;
        public readonly int Sign;
        public readonly bool MovingTurned;
        public readonly Shape First;
        public readonly Shape Second;

        public Case(int axis, int sign, bool movingTurned, Shape first, Shape second)
        {
            Axis = axis;
            Sign = sign;
            MovingTurned = movingTurned;
            First = first;
            Second = second;
        }

        public override string ToString() =>
            $"{"XYZ"[Axis]}{(Sign > 0 ? "+" : "-")}{(MovingTurned ? " turned" : "")} {First}/{Second}";
    }

    public static IEnumerable<Case> Matrix()
    {
        for (int axis = 0; axis < 3; axis++)
            foreach (int sign in new[] { 1, -1 })
                foreach (bool turned in new[] { false, true })
                    foreach (var first in OnTheLine)
                        foreach (var second in OnTheLine)
                            yield return new Case(axis, sign, turned, first, second);
    }

    public static IEnumerable<Case> Directions()
    {
        for (int axis = 0; axis < 3; axis++)
            foreach (int sign in new[] { 1, -1 })
                foreach (bool turned in new[] { false, true })
                    yield return new Case(axis, sign, turned, OnTheLine[0], OnTheLine[1]);
    }

    private static Vector3 MovingWorldSizeMm(bool turned) =>
        turned
            ? new Vector3(MovingLocalSizeMm.z, MovingLocalSizeMm.y, MovingLocalSizeMm.x)
            : MovingLocalSizeMm;

    private static ElementGeometry MovingGeometry(bool turned, Vector3 centre) =>
        ElementGeometry.Box("M", centre, MovingLocalSizeMm * Mm,
            turned ? TurnedAboutY : Quaternion.identity);

    private static float MovingHalfAlong(Case c) => MovingWorldSizeMm(c.MovingTurned)[c.Axis] * 0.5f * Mm;

    private static void Across(int axis, out int b, out int w)
    {
        b = (axis + 1) % 3;
        w = (axis + 2) % 3;
    }

    private static Vector3 World(Case c, float u, float v, float w, Vector3 from)
    {
        Across(c.Axis, out int bi, out int wi);
        var p = from;
        p[c.Axis] += c.Sign * u;
        p[bi] += v;
        p[wi] += w;
        return p;
    }

    private static AxisBox Neighbour(Case c, int id, Shape shape, float nearU)
    {
        Across(c.Axis, out int bi, out int wi);
        var centre = World(c, nearU + shape.ThicknessMm * 0.5f * Mm,
            shape.OffsetVMm * Mm, shape.OffsetWMm * Mm, MovingCentre);
        var size = Vector3.zero;
        size[c.Axis] = shape.ThicknessMm * Mm;
        size[bi] = shape.CrossVMm * Mm;
        size[wi] = shape.CrossWMm * Mm;
        if (!shape.Turned) return AxisBox.Aligned(id, centre - size * 0.5f, centre + size * 0.5f);

        var local = new Vector3(size.z, size.y, size.x);
        var turned = AxisBox.Of(ElementGeometry.Box("N" + id, centre, local, TurnedAboutY));
        return new AxisBox(id, turned.Centre, turned.AxisA, turned.AxisB, turned.AxisC, turned.Half);
    }

    private static AxisBox Distractor(Case c, int id, float nearU)
    {
        float clear = 0.5f * MovingWorldSizeMm(c.MovingTurned).magnitude * Mm + 0.2f;
        Across(c.Axis, out int bi, out int wi);
        var centre = World(c, nearU + 0.05f, clear, clear, MovingCentre);
        return AxisBox.Aligned(id, centre - Vector3.one * 0.05f, centre + Vector3.one * 0.05f);
    }

    private sealed class Scene
    {
        public AxisGuideIndex Index = null!;
        public float FirstNearU;
        public float FirstFarU;
        public float SecondNearU;
    }

    private static Scene Build(Case c, float firstGapMm, float existingGapMm)
    {
        float half = MovingHalfAlong(c);
        float firstNear = half + firstGapMm * Mm;
        float firstFar = firstNear + c.First.ThicknessMm * Mm;
        float secondNear = firstFar + existingGapMm * Mm;
        var boxes = new List<AxisBox>
        {
            Distractor(c, 90, half + firstGapMm * Mm * 0.25f),
            Neighbour(c, 2, c.Second, secondNear),
            Distractor(c, 91, firstFar + existingGapMm * Mm * 0.5f),
            Neighbour(c, 1, c.First, firstNear),
        };
        return new Scene
        {
            Index = new AxisGuideIndex(boxes),
            FirstNearU = firstNear,
            FirstFarU = firstFar,
            SecondNearU = secondNear,
        };
    }

    private static AxisBox MovingAt(Case c, Vector3 centre) => AxisBox.Of(MovingGeometry(c.MovingTurned, centre));

    [TestCaseSource(nameof(Matrix))]
    public void Guide_EveryAxisDirectionAndShape_MeasuresFaceToFace(Case c)
    {
        var scene = Build(c, FirstGapMm, ExistingGapMm);

        var cast = DistanceGuides.Cast(scene.Index, MovingAt(c, MovingCentre), c.Axis, c.Sign);

        Assert.IsTrue(cast.HasNear && cast.Near.Id == 1,
            $"{c}: ближний сосед на луче — N1; отвлекающая коробка ближе него, но в другом ряду, "
            + $"а нашлось id={(cast.HasNear ? cast.Near.Id : -1)}");
        Assert.AreEqual(FirstGapMm, cast.NearGap / Mm, 0.2f,
            $"{c}: зазор деталь↔N1 от грани до грани ({FirstGapMm} мм), а не от центров");
        Assert.IsTrue(cast.HasFar && cast.Far.Id == 2,
            $"{c}: следующий за N1 на той же прямой — N2, а не отвлекающая коробка между ними");
        Assert.AreEqual(ExistingGapMm, cast.FarGap / Mm, 0.2f,
            $"{c}: существующий зазор N1↔N2 тоже от грани до грани ({ExistingGapMm} мм)");

        var lines = new List<GuideLine>();
        DistanceGuides.Collect(scene.Index, MovingAt(c, MovingCentre), lines);
        var expectedFrom = World(c, MovingHalfAlong(c), 0f, 0f, MovingCentre);
        var expectedTo = World(c, scene.FirstNearU, 0f, 0f, MovingCentre);
        Assert.IsTrue(lines.Exists(l => (l.A - expectedFrom).magnitude < Tol && (l.B - expectedTo).magnitude < Tol),
            $"{c}: линия направляющей обязана идти от грани детали {expectedFrom} до грани N1 "
            + $"{expectedTo} по оси луча");
    }

    [TestCaseSource(nameof(Matrix))]
    public void Drag_TowardAndAwayFromTheFirstNeighbour_SettlesAtTheEqualSurfaceGap(Case c)
    {
        foreach (float overshootMm in new[] { 20f, -20f })
        {
            var scene = Build(c, ExistingGapMm + overshootMm, ExistingGapMm);
            var free = MovingCentre;

            var settled = EqualGapSnap.Resolve(scene.Index, MovingAt(c, free), free, false, free,
                EqualGapSnap.MaskOf(c.Axis), Threshold, out int pulled);

            var expected = World(c, overshootMm * Mm, 0f, 0f, MovingCentre);
            Assert.AreEqual(EqualGapSnap.MaskOf(c.Axis), pulled,
                $"{c}, зазор {ExistingGapMm + overshootMm} мм при существующем {ExistingGapMm}: "
                + "разница в пределах порога — обязан прилипнуть");
            Assert.AreEqual(0f, (settled - expected).magnitude, Tol,
                $"{c}: деталь встаёт так, что её грань в {ExistingGapMm} мм от грани N1 — "
                + $"ожидалось {expected}, получено {settled}");
        }
    }

    [TestCaseSource(nameof(Matrix))]
    public void Resize_FaceFacingTheNeighbours_StopsAtTheEqualSurfaceGap(Case c)
    {
        var scene = Build(c, ExistingGapMm, ExistingGapMm);
        var geometry = MovingGeometry(c.MovingTurned, MovingCentre);
        var direction = World(c, 1f, 0f, 0f, Vector3.zero);
        int faceIndex = FaceFacing(geometry, direction);
        var face = geometry.Faces[faceIndex];
        int localAxis = faceIndex / 2;
        var dims = Vector3Int.RoundToInt(MovingLocalSizeMm);
        float sizeStart = MovingLocalSizeMm[localAxis] * Mm;
        float overshoot = 30f * Mm;

        ResizeMath.Compute(dims, localAxis, face.normal.normalized, face.center, face.rightAxis,
            face.upAxis, face.size, MovingCentre, sizeStart, overshoot,
            new List<ElementGeometry>(), geometry, true, Threshold,
            out var newDims, out var newCentre, out bool snapped, scene.Index);

        Assert.IsTrue(snapped, $"{c}: грань, утянутая на 30 мм за равный зазор, обязана прилипнуть");
        Assert.AreEqual(dims[localAxis], newDims[localAxis],
            $"{c}: размер возвращается к исходному — при нём грань в {ExistingGapMm} мм от N1, "
            + "как N1 от N2");
        Assert.AreEqual(0f, (newCentre - MovingCentre).magnitude, Tol, $"{c}: и центр тоже");
    }

    [TestCaseSource(nameof(Directions))]
    public void Guide_NeighbourBesideTheCentreLine_GivesNoLineAndNoSnap(Case c)
    {
        var miss = new Case(c.Axis, c.Sign, c.MovingTurned, BesideTheLine, OnTheLine[0]);
        var scene = Build(miss, ExistingGapMm + 20f, ExistingGapMm);

        var cast = DistanceGuides.Cast(scene.Index, MovingAt(miss, MovingCentre), c.Axis, c.Sign);

        Assert.IsTrue(!cast.HasNear || cast.Near.Id != 1,
            $"{miss}: сосед 200 мм шириной, сдвинутый на 260 мм от оси луча, её не накрывает — "
            + "попаданием быть не может");
        var settled = EqualGapSnap.Resolve(scene.Index, MovingAt(miss, MovingCentre), MovingCentre,
            false, MovingCentre, EqualGapSnap.MaskOf(c.Axis), Threshold, out int pulled);
        Assert.AreEqual(0, pulled, $"{miss}: без N1 на луче нет и равного зазора");
        Assert.AreEqual(0f, (settled - MovingCentre).magnitude, Tol, $"{miss}: деталь не сдвинута");
    }

    [TestCaseSource(nameof(Directions))]
    public void Guide_TenMetreBoundary_HitsAt9999AndNotAt10001(Case c)
    {
        foreach (var (gapMm, expectHit) in new[] { (9999f, true), (10001f, false) })
        {
            var scene = Build(c, gapMm, ExistingGapMm);

            var cast = DistanceGuides.Cast(scene.Index, MovingAt(c, MovingCentre), c.Axis, c.Sign);

            bool hit = cast.HasNear && cast.Near.Id == 1;
            Assert.AreEqual(expectHit, hit,
                $"{c}: грань соседа в {gapMm} мм — {(expectHit ? "внутри" : "за")} предела 10 м");
            if (expectHit)
                Assert.AreEqual(gapMm, cast.NearGap / Mm, 1f, $"{c}: и расстояние от грани до грани");
        }
    }

    [TestCaseSource(nameof(Directions))]
    public void Drag_SecondNeighbourBeyondTenMetres_IsNoEqualGap(Case c)
    {
        var scene = Build(c, 5000f, 5600f);

        EqualGapSnap.Resolve(scene.Index, MovingAt(c, MovingCentre), MovingCentre, false,
            MovingCentre, EqualGapSnap.MaskOf(c.Axis), 700f * Mm, out int pulled);

        Assert.AreEqual(0, pulled,
            $"{c}: N2 начинается в 10,6+ м от детали — смотреть дальше 10 м нельзя и ради N2");
    }

    private static int FaceFacing(in ElementGeometry geometry, Vector3 direction)
    {
        for (int i = 0; i < geometry.Faces.Length; i++)
            if (Vector3.Dot(geometry.Faces[i].normal.normalized, direction) > Tolerance.ParallelDot)
                return i;
        Assert.Fail($"у коробки нет грани с нормалью {direction}");
        return -1;
    }
}
