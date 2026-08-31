using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Равномерная сетка broad-phase: она решает, какие пары деталей вообще
/// дойдут до проверок. Пропущенная пара — это НЕ ложная тревога, а молчание:
/// пересечение просто не находится. Поэтому здесь пинается ровно то, на чём
/// сетка держится — упаковка ключа ячейки, расширение габарита на contactDist
/// и на короб врезной техники, и порядок пар на выходе.</summary>
public class ValidationBroadPhaseTests
{
    private const float MM = 0.001f;

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
        var geometry = ElementGeometry.Box(name, center, size);
        return new ValidationElement(geometry, Corners(center, size), kind, 0, null,
            Span.FromCenter(center.y, size.y), -1);
    }

    private static ValidationElement Recessed(string name, Vector3 centerMm, Vector3 sizeMm,
        Vector3 bodyCenterMm, Vector3 bodySizeMm, int hostIndex)
    {
        Vector3 center = centerMm * MM;
        Vector3 size = sizeMm * MM;
        var geometry = ElementGeometry.Box(name, center, size);
        var body = ElementGeometry.Box(name + "/body", bodyCenterMm * MM, bodySizeMm * MM);
        return new ValidationElement(geometry, Corners(center, size), ElementKind.Recessed,
            0, null, Span.FromCenter(center.y, size.y), -1, body, true, hostIndex);
    }

    private static int OverlapsBetween(CoreValidationResult r, int i, int j) =>
        r.Diagnostics == null ? 0 : r.Diagnostics.Count(d => d.Kind == ViolationKind.Overlap
            && ((d.Element == i && d.Other == j) || (d.Element == j && d.Other == i)));

    [Test]
    public void CellKey_IsInjective_OverTheWholeSignedCellRange()
    {
        int edge = ValidationBroadPhase.CellsPerAxisFromOrigin - 1;
        var cells = new List<(int x, int y, int z)>
        {
            (0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1),
            (-1, 0, 0), (0, -1, 0), (0, 0, -1),
            (edge, edge, edge), (-edge, -edge, -edge),
            (edge, -edge, edge), (-edge, edge, -edge),
            (edge, 0, 0), (0, edge, 0), (0, 0, edge),
            (-edge, 0, 0), (0, -edge, 0), (0, 0, -edge),
        };

        var seen = new Dictionary<long, (int, int, int)>();
        foreach (var c in cells)
        {
            long key = ValidationBroadPhase.CellKey(c.x, c.y, c.z);
            Assert.IsFalse(seen.ContainsKey(key),
                "ячейки " + c + " и " + (seen.TryGetValue(key, out var prev) ? prev.ToString() : "?")
                + " делят один ключ: 21 бит на ось и сдвиг +CellsPerAxisFromOrigin существуют "
                + "именно затем, чтобы отрицательные координаты не наезжали на чужие биты");
            seen[key] = c;
        }
    }

    [Test]
    public void TouchingPair_SplitByACellBoundary_IsStillFound()
    {
        // Зазор 0.4 мм (меньше ContactMm) — это контакт. Граница ячейки проходит
        // ВНУТРИ зазора, поэтому тела лежат в разных ячейках, и в общую их
        // сводит только расширение габарита на contactDist при заносе в сетку.
        float boundaryMm = ValidationBroadPhase.CellSizeUnits * 1000f;
        var left = Part("Left", new Vector3(boundaryMm - 0.2f - 400f, 9, 0),
            new Vector3(800, 18, 400));
        var right = Part("Right", new Vector3(boundaryMm + 0.2f + 400f, 9, 0),
            new Vector3(800, 18, 400));

        Assert.AreNotEqual(
            ValidationBroadPhase.CellFloor(left.Geometry.Max.x),
            ValidationBroadPhase.CellFloor(right.Geometry.Min.x),
            "сцена собрана неправильно: тела обязаны лежать по разные стороны границы ячейки, "
            + "иначе тест зелен независимо от расширения габарита");

        var r = ValidationCore.Validate(new[] { left, right });

        Assert.Greater(r.Contacts.Count, 0,
            "деталь заносится во ВСЕ ячейки, которых касается её AABB, РАСШИРЕННЫЙ на "
            + "contactDist — иначе стык на границе ячейки не находится вовсе");
    }

    [Test]
    public void RecessedBody_ReachingIntoAnotherCell_IsFoundByBroadPhase()
    {
        // Габарит врезной техники — только бортик на пласти (5 мм). Её короб
        // уходит вглубь на полтора метра, то есть ячейкой ниже. Без расширения
        // габарита на короб пара «техника ↔ боковина» в общую ячейку не попадает,
        // и наезд короба на боковину остаётся ненайденным.
        var scene = new[]
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Part("Side", new Vector3(0, 400, 0), new Vector3(18, 800, 400)),
            Part("Worktop", new Vector3(0, 1900, 0), new Vector3(2000, 38, 600)),
            Recessed("Hob", new Vector3(0, 1900, 0), new Vector3(500, 5, 400),
                new Vector3(0, 1100, 0), new Vector3(500, 1600, 400), hostIndex: 2),
        };

        Assert.AreNotEqual(
            ValidationBroadPhase.CellFloor(scene[3].Geometry.Min.y),
            ValidationBroadPhase.CellFloor(scene[1].Geometry.Max.y),
            "сцена собрана неправильно: бортик техники и боковина обязаны лежать в разных "
            + "ячейках, иначе пара находится и без учёта короба");

        var r = ValidationCore.Validate(scene);

        Assert.AreEqual(1, OverlapsBetween(r, 3, 1),
            "короб врезной техники налез на боковину — это пересечение, и найти его может "
            + "только сетка, знающая про короб");
    }

    [Test]
    public void RecessedBody_StoppingShortOfTheCarcass_IsNotOverlap()
    {
        // Положительный контроль к предыдущему на ТОЙ ЖЕ геометрии: короб короче
        // и до боковины не достаёт. Без него тест выше был бы зелёным и на коде,
        // который метит любую врезную технику рядом с корпусной деталью.
        var scene = new[]
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(3000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
            Part("Side", new Vector3(0, 400, 0), new Vector3(18, 800, 400)),
            Part("Worktop", new Vector3(0, 1900, 0), new Vector3(2000, 38, 600)),
            Recessed("Hob", new Vector3(0, 1900, 0), new Vector3(500, 5, 400),
                new Vector3(0, 1750, 0), new Vector3(500, 300, 400), hostIndex: 2),
        };

        var r = ValidationCore.Validate(scene);

        Assert.AreEqual(0, OverlapsBetween(r, 3, 1), "короб не дошёл до боковины");
    }

    [Test]
    public void CandidatePairs_ComeOutInNestedLoopOrder_NotInGridOrder()
    {
        // Пары сортируются по (lo,hi), чтобы порядок контактов совпадал с
        // порядком двойного цикла for(i){for(j=i+1)}. На него опираются тесты и
        // подсветка. Детали здесь ПЕРЕЧИСЛЕНЫ справа налево, а сетка наполняется
        // слева направо, поэтому без сортировки пары выходят в обратном порядке.
        var scene = new List<ValidationElement>
        {
            Part("Floor", new Vector3(0, -9, 0), new Vector3(8000, 18, 3000),
                ElementKind.Anchor | ElementKind.FloorAnchor),
        };
        for (int i = 0; i < 6; i++)
            scene.Add(Part("B" + i, new Vector3(2500f - i * 1000f, 9, 0),
                new Vector3(800, 18, 400)));

        var pairs = ValidationBroadPhase.CandidatePairsInNestedLoopOrder(
            scene, Tolerance.ContactMm * AppConstants.MM_TO_UNITS).ToList();
        ValidationBroadPhase.Clear();

        for (int k = 1; k < pairs.Count; k++)
            Assert.IsTrue(pairs[k - 1].lo < pairs[k].lo
                          || (pairs[k - 1].lo == pairs[k].lo && pairs[k - 1].hi < pairs[k].hi),
                "пара " + pairs[k] + " идёт после " + pairs[k - 1]
                + " — порядок утёк из раскладки сетки по ячейкам");
    }

    /// <summary>Найдено мутационным прогоном: обе половины объединения с коробом
    /// переживали замену Max на Min. Тесты на короб были, строку исполняли — а
    /// РЕЗУЛЬТАТ не проверял никто, и объединение можно было подменить
    /// пересечением, не покраснев ни разу.
    ///
    /// Короб берётся заведомо НЕСИММЕТРИЧНЫМ и вылезающим за габарит на разные
    /// стороны. Короб, целиком помещающийся внутри детали по горизонтали, прячет
    /// ровно этот класс дефекта: Vector3.Max(габарит, короб.Max) и
    /// Vector3.Max(габарит, короб.Min) дают на нём один и тот же ответ. Метод
    /// общий и про размеры короба ничего не обещает.</summary>
    [Test]
    public void SolidBounds_WithARecessedBody_IsTheUnion_NotTheIntersection()
    {
        var hob = Recessed("Hob", new Vector3(0, 900, 0), new Vector3(400, 20, 400),
            new Vector3(50, 750, 0), new Vector3(600, 300, 500), -1);

        ValidationBroadPhase.SolidBoundsIncludingRecessedBody(hob, out var min, out var max);

        Assert.AreEqual(-250f * MM, min.x, 1e-5f,
            "короб шире детали слева — габарит обязан расшириться до него");
        Assert.AreEqual(600f * MM, min.y, 1e-5f,
            "низ габарита задаёт короб: он и есть тело, уходящее в столешницу");
        Assert.AreEqual(-250f * MM, min.z, 1e-5f, "то же по глубине");

        Assert.AreEqual(350f * MM, max.x, 1e-5f,
            "короб вылезает справа — Vector3.Max, не Vector3.Min: пересечение "
            + "СУЗИЛО бы габарит до детали, и наезд короба перестал бы находиться");
        Assert.AreEqual(910f * MM, max.y, 1e-5f,
            "верх задаёт сама деталь: объединение берёт максимум из двух, "
            + "а не подменяет габарит коробом");
        Assert.AreEqual(250f * MM, max.z, 1e-5f, "то же по глубине");
    }

    [Test]
    public void SolidBounds_WithoutARecessedBody_IsTheGeometryItself()
    {
        var board = Part("B", new Vector3(0, 900, 0), new Vector3(400, 20, 400));

        ValidationBroadPhase.SolidBoundsIncludingRecessedBody(board, out var min, out var max);

        Assert.AreEqual(-200f * MM, min.x, 1e-5f,
            "у детали без короба габарит равен её собственному: пустой RecessedBody "
            + "лежит в нуле и, попав в объединение, растянул бы габарит до начала координат");
        Assert.AreEqual(890f * MM, min.y, 1e-5f, "то же по высоте");
        Assert.AreEqual(200f * MM, max.x, 1e-5f, "то же с другой стороны");
        Assert.AreEqual(910f * MM, max.y, 1e-5f, "то же по высоте");
    }

    [Test]
    public void FarApartElements_ProduceNoCandidatePair()
    {
        var a = Part("A", new Vector3(0, 9, 0), new Vector3(800, 18, 400));
        var b = Part("B", new Vector3(50000, 9, 0), new Vector3(800, 18, 400));

        var r = ValidationCore.Validate(new[] { a, b });

        Assert.AreEqual(0, r.Contacts.Count,
            "детали в 50 м друг от друга не должны попадать в общую ячейку");
    }
}
