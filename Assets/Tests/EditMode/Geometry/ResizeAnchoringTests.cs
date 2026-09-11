using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Куда уезжает деталь при правке размера в панели свойств. Правило:
/// размер РАСТЁТ и мешает сосед ровно с одной стороны — деталь уходит в
/// противоположную; размер УМЕНЬШАЕТСЯ и с одной стороны есть прилипание —
/// деталь идёт за ним (приоритет плоскость → грань → вершина); во всех
/// остальных случаях рост и убыль остаются симметричными от центра.
/// Решение чистое, поэтому весь перебор шести сторон идёт без сцены.</summary>
public class ResizeAnchoringTests
{
    private const int SideMM = 400;

    private const float HalfUnits = SideMM * 0.5f * AppConstants.MM_TO_UNITS;

    private const float Tol = 1e-5f;

    /// <summary>90° вокруг Y литералом: `Quaternion.AngleAxis` — ECall, на быстром пути его нет.</summary>
    private static readonly Quaternion QuarterTurn = new Quaternion(0f, 0.70710678f, 0f, 0.70710678f);

    private static readonly Vector3 Origin = new Vector3(0f, 0.2f, 0f);

    private static ElementGeometry Box(string name, Vector3 centre, Vector3Int dims, Quaternion rot)
        => ElementGeometry.Box(name, centre,
            new Vector3(dims.x, dims.y, dims.z) * AppConstants.MM_TO_UNITS, rot);

    private static ElementGeometry Self(Quaternion rot) =>
        Box("A", Origin, new Vector3Int(SideMM, SideMM, SideMM), rot);

    private static ElementGeometry Resized(int mm, int axis, Quaternion rot)
    {
        var dims = new Vector3Int(SideMM, SideMM, SideMM);
        if (axis == 0) dims.x = mm;
        else if (axis == 1) dims.y = mm;
        else dims.z = mm;
        return Box("A", Origin, dims, rot);
    }

    /// <summary>Сосед-куб, чья встречная грань стоит в gapUnits от грани face детали,
    /// со сдвигом вдоль двух поперечных осей: сдвиг ровно на 0,4 м (сумма полугабаритов)
    /// превращает плоскость в грань, а два таких сдвига — в вершину.</summary>
    private static ElementGeometry Facing(string name, in ElementGeometry self, int face,
        float gapUnits, float slideA = 0f, float slideB = 0f)
    {
        int axis = face / 2;
        var outward = self.Faces[face].normal;
        var lateralA = self.Faces[((axis + 1) % 3) * 2].normal;
        var lateralB = self.Faces[((axis + 2) % 3) * 2].normal;
        var centre = Origin + outward * (HalfUnits * 2f + gapUnits)
            + lateralA * slideA + lateralB * slideB;
        return Box(name, centre, new Vector3Int(SideMM, SideMM, SideMM), Rotation(self));
    }

    private static ElementGeometry Contact(string name, in ElementGeometry self, int face,
        ResizeContactRank rank) => rank switch
    {
        ResizeContactRank.Plane => Facing(name, self, face, 0f),
        ResizeContactRank.Edge => Facing(name, self, face, 0f, HalfUnits * 2f),
        ResizeContactRank.Vertex => Facing(name, self, face, 0f, HalfUnits * 2f, HalfUnits * 2f),
        _ => Facing(name, self, face, 0.01f),
    };

    private static Quaternion Rotation(in ElementGeometry self) =>
        Vector3.Dot(self.Faces[0].normal, Vector3.right) > 0.9f ? Quaternion.identity : QuarterTurn;

    private static Vector3 Shift(in ElementGeometry self, int mm, int axis, Quaternion rot,
        params ElementGeometry[] neighbours) =>
        ResizeAnchoring.ShiftUnits(self, Resized(mm, axis, rot),
            new List<ElementGeometry>(neighbours));

    private static string Name(int face) =>
        (face / 2 == 0 ? "X" : face / 2 == 1 ? "Y" : "Z") + (face % 2 == 0 ? "+" : "-");

    [Test]
    public void ResizeAnchoring_Grow_ConflictOnOneSide_ShiftsAwayByHalfTheGrowth()
    {
        var self = Self(Quaternion.identity);
        for (int face = 0; face < Face.BoxFaceCount; face++)
        {
            var blocker = Facing("B", self, face, 0f);
            var shift = Shift(self, 500, face / 2, Quaternion.identity, blocker);

            var expected = -self.Faces[face].normal * 0.05f;
            Assert.AreEqual(expected.x, shift.x, Tol, $"сторона {Name(face)}: рост 100 мм упирается "
                + "в соседа, деталь обязана уйти на 50 мм в противоположную сторону");
            Assert.AreEqual(expected.y, shift.y, Tol, $"сторона {Name(face)}: та же проверка по Y");
            Assert.AreEqual(expected.z, shift.z, Tol, $"сторона {Name(face)}: та же проверка по Z");
        }
    }

    [Test]
    public void ResizeAnchoring_Grow_ConflictOnBothSides_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        for (int axis = 0; axis < 3; axis++)
        {
            var shift = Shift(self, 500, axis, Quaternion.identity,
                Facing("B", self, axis * 2, 0f), Facing("C", self, axis * 2 + 1, 0f));

            Assert.AreEqual(0f, shift.magnitude, Tol, $"ось {axis}: конфликт возникает с обеих "
                + "сторон, уезжать некуда — поведение остаётся прежним, от центра");
        }
    }

    [Test]
    public void ResizeAnchoring_Grow_NoNeighbours_KeepsCentre()
    {
        var self = Self(Quaternion.identity);

        Assert.AreEqual(0f, Shift(self, 500, 0, Quaternion.identity).magnitude, Tol,
            "соседей нет — расти положено в обе стороны от центра");
    }

    [Test]
    public void ResizeAnchoring_Grow_NeighbourFartherThanHalfTheGrowth_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        var far = Facing("B", self, 0, 0.06f);

        Assert.AreEqual(0f, Shift(self, 500, 0, Quaternion.identity, far).magnitude, Tol,
            "сосед в 60 мм, а грань уходит вперёд на 50 — конфликта не возникает, смещать нечего");
    }

    [Test]
    public void ResizeAnchoring_Grow_OpenSideTooNarrowForTheWholeGrowth_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        var blocker = Facing("B", self, 0, 0f);
        var narrow = Facing("C", self, 1, 0.08f);

        Assert.AreEqual(0f, Shift(self, 500, 0, Quaternion.identity, blocker, narrow).magnitude, Tol,
            "уходить некуда: свободная сторона примет 80 мм из 100, и смещение внесло бы "
            + "второй конфликт вместо первого");
    }

    [Test]
    public void ResizeAnchoring_Grow_NeighbourTouchesOnlyAlongAnEdge_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        var edgeOnly = Facing("B", self, 0, 0f, HalfUnits * 2f);

        Assert.AreEqual(0f, Shift(self, 500, 0, Quaternion.identity, edgeOnly).magnitude, Tol,
            "сосед касается только по ребру: растущая деталь проезжает мимо него, "
            + "конфликта нет — значит и смещения нет");
    }

    [Test]
    public void ResizeAnchoring_Shrink_PlaneContactOnOneSide_ShiftsIntoIt()
    {
        var self = Self(Quaternion.identity);
        for (int face = 0; face < Face.BoxFaceCount; face++)
        {
            var touching = Facing("B", self, face, 0f);
            var shift = Shift(self, 300, face / 2, Quaternion.identity, touching);

            var expected = self.Faces[face].normal * 0.05f;
            Assert.AreEqual(expected.x, shift.x, Tol, $"сторона {Name(face)}: убыль 100 мм "
                + "обязана уйти щелью на дальнюю сторону, прилипание сохраняется");
            Assert.AreEqual(expected.y, shift.y, Tol, $"сторона {Name(face)}: та же проверка по Y");
            Assert.AreEqual(expected.z, shift.z, Tol, $"сторона {Name(face)}: та же проверка по Z");
        }
    }

    [Test]
    public void ResizeAnchoring_Shrink_EqualContactOnBothSides_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        for (int axis = 0; axis < 3; axis++)
        {
            var shift = Shift(self, 300, axis, Quaternion.identity,
                Facing("B", self, axis * 2, 0f), Facing("C", self, axis * 2 + 1, 0f));

            Assert.AreEqual(0f, shift.magnitude, Tol, $"ось {axis}: прилипание с обеих сторон "
                + "одного ранга — ни одна не перевешивает, убыль остаётся симметричной");
        }
    }

    [Test]
    public void ResizeAnchoring_Shrink_NeighbourBeyondContactDistance_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        var apart = Facing("B", self, 0, 0.01f);

        Assert.AreEqual(0f, Shift(self, 300, 0, Quaternion.identity, apart).magnitude, Tol,
            "сосед в 10 мм — это не контакт, идти за ним деталь не обязана");
    }

    [Test]
    public void ResizeAnchoring_Shrink_HigherRankedContactWins()
    {
        var self = Self(Quaternion.identity);
        var pairs = new[]
        {
            (strong: ResizeContactRank.Plane, weak: ResizeContactRank.Edge),
            (strong: ResizeContactRank.Plane, weak: ResizeContactRank.Vertex),
            (strong: ResizeContactRank.Edge, weak: ResizeContactRank.Vertex),
        };

        foreach (var (strong, weak) in pairs)
        {
            var toPlus = Shift(self, 300, 0, Quaternion.identity,
                Contact("B", self, 0, strong), Contact("C", self, 1, weak));
            Assert.AreEqual(0.05f, toPlus.x, Tol,
                $"{strong} против {weak}: деталь идёт за контактом более высокого приоритета");

            var toMinus = Shift(self, 300, 0, Quaternion.identity,
                Contact("B", self, 1, strong), Contact("C", self, 0, weak));
            Assert.AreEqual(-0.05f, toMinus.x, Tol,
                $"{strong} против {weak}, стороны поменяны местами: решает приоритет, "
                + "а не номер стороны");
        }
    }

    [Test]
    public void ResizeAnchoring_ContactOn_TellsPlaneFromEdgeFromVertex()
    {
        var self = Self(Quaternion.identity);
        var plane = new List<ElementGeometry> { Facing("B", self, 0, 0f) };
        var edge = new List<ElementGeometry> { Facing("B", self, 0, 0f, HalfUnits * 2f) };
        var vertex = new List<ElementGeometry>
            { Facing("B", self, 0, 0f, HalfUnits * 2f, HalfUnits * 2f) };
        var apart = new List<ElementGeometry> { Facing("B", self, 0, 0.01f) };
        var aside = new List<ElementGeometry> { Facing("B", self, 0, 0f, HalfUnits * 2f + 0.01f) };

        Assert.AreEqual(ResizeContactRank.Plane, ResizeAnchoring.ContactOn(self, 0, plane),
            "встречная грань целиком перекрыта — это плоскость");
        Assert.AreEqual(ResizeContactRank.Edge, ResizeAnchoring.ContactOn(self, 0, edge),
            "сосед сдвинут ровно на сумму полугабаритов — остаётся общее ребро");
        Assert.AreEqual(ResizeContactRank.Vertex, ResizeAnchoring.ContactOn(self, 0, vertex),
            "сдвиг по обеим поперечным осям — общей осталась одна вершина");
        Assert.AreEqual(ResizeContactRank.None, ResizeAnchoring.ContactOn(self, 0, apart),
            "10 мм зазора — контакта нет");
        Assert.AreEqual(ResizeContactRank.None, ResizeAnchoring.ContactOn(self, 0, aside),
            "разошлись на 10 мм поперёк — вершины тоже нет");
        Assert.AreEqual(ResizeContactRank.None, ResizeAnchoring.ContactOn(self, 1, plane),
            "контакт числится за своей стороной, противоположная о нём не знает");
    }

    [Test]
    public void ResizeAnchoring_AlreadyOverlapping_KeepsCentre()
    {
        var self = Self(Quaternion.identity);
        var inside = Facing("B", self, 0, -0.05f);

        Assert.AreEqual(0f, Shift(self, 500, 0, Quaternion.identity, inside).magnitude, Tol,
            "деталь уже конфликтует — правка размера остаётся прежней, от центра, "
            + "иначе чинить такую деталь стало бы нечем");
        Assert.AreEqual(0f, Shift(self, 300, 0, Quaternion.identity, inside).magnitude, Tol,
            "то же самое на убыли: пересечение — не прилипание");
    }

    [Test]
    public void ResizeAnchoring_Grow_MovesOnlyTheAxisWhoseSizeChanged()
    {
        var self = Self(Quaternion.identity);
        var alongX = Facing("B", self, 0, 0f);
        var alongY = Facing("C", self, 2, 0f);

        var shift = Shift(self, 500, 0, Quaternion.identity, alongX, alongY);

        Assert.AreEqual(-0.05f, shift.x, Tol,
            "выросла только ширина — по X деталь обязана уйти от соседа");
        Assert.AreEqual(0f, shift.y, Tol,
            "высота не менялась, и сосед сверху её не двигает: это положительный контроль "
            + "к строке выше, на той же геометрии");
    }

    [Test]
    public void ResizeAnchoring_RotatedElement_ShiftsAlongItsOwnAxis()
    {
        var self = Self(QuarterTurn);
        var blocker = Facing("B", self, 0, 0f);

        var shift = Shift(self, 500, 0, QuarterTurn, blocker);

        var expected = -self.Faces[0].normal * 0.05f;
        Assert.AreEqual(expected.x, shift.x, Tol,
            "повёрнутая деталь: ширина растёт вдоль СОБСТВЕННОЙ оси, и смещение идёт по ней же");
        Assert.AreEqual(expected.z, shift.z, Tol, "та же проверка по мировому Z");
        Assert.Greater(Mathf.Abs(expected.z), 0.04f,
            "фикстура обязана быть повёрнутой, иначе проверка ничего не отличает");
    }
}
