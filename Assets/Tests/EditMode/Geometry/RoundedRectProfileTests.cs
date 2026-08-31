using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Замкнутый контур «прямоугольник с радиусами по углам» в плоскости XZ —
/// общий примитив радиусной полки (скруглён один угол), радиусного стола (все
/// четыре по min(Ш,Г)/2) и будущего табурета (все четыре одинаковые, ноль = квадрат).
///
/// Порядок параметров = порядок обхода: (−X,−Z) → (+X,−Z) → (+X,+Z) → (−X,+Z).
/// Обход ПРОТИВ часовой стрелки (площадь по формуле шнурков положительна) — от
/// этого зависит направление внешних нормалей боковин: ProfileExtrusionMesh берёт
/// нормаль как (dir.y, −dir.x), и при обратном обходе вся боковая поверхность
/// вывернется внутрь детали.
///
/// Число точек: 4 + (число НЕнулевых радиусов) × segments. Нулевой радиус даёт
/// ровно одну точку в углу — ни дуги, ни отрезка нулевой длины. Совпавшие точки
/// (стык дуг у окружности и у стадиона) схлопываются.
///
/// Подрезка радиусов — ОДНО правило: сумма двух радиусов на одной стороне не
/// превышает её длину, иначе контур самопересекается. Отдельного потолка на угол
/// нет намеренно: при нулевых соседях это правило само даёт r ≤ min(Ш,Г) (столько
/// и разрешает радиусная полка на своём единственном углу), а при четырёх
/// одинаковых — r ≤ min(Ш,Г)/2 (ровно потолок табурета и стола).</summary>
public class RoundedRectProfileTests
{
    private const int Seg = RoundedRectProfile.DefaultSegments;

    private static readonly Vector2[] SharpCorners =
    {
        new Vector2(-300f, -200f), new Vector2(300f, -200f),
        new Vector2(300f, 200f), new Vector2(-300f, 200f),
    };

    private static bool Has(Vector2[] points, Vector2 wanted)
        => points.Any(p => (p - wanted).sqrMagnitude < 1e-6f);

    private static Vector2 Size(Vector2[] points)
        => new Vector2(points.Max(p => p.x) - points.Min(p => p.x),
                       points.Max(p => p.y) - points.Min(p => p.y));

    private static Vector2 Centre(Vector2[] points)
        => new Vector2((points.Max(p => p.x) + points.Min(p => p.x)) * 0.5f,
                       (points.Max(p => p.y) + points.Min(p => p.y)) * 0.5f);

    /// <summary>Радиус, который контур получил на самом деле: дуга угла (+X,+Z)
    /// начинается в точке (Ш/2, Г/2 − r), то есть последней на правой стороне.</summary>
    private static float EffectiveRadius(Vector2[] points, float width, float depth)
    {
        float onRightEdge = points.Where(p => Mathf.Abs(p.x - width * 0.5f) < 1e-3f).Max(p => p.y);
        return depth * 0.5f - onRightEdge;
    }

    [Test]
    public void AllRadiiZero_GivesPlainRectangleOfFourPoints()
    {
        var points = RoundedRectProfile.Build(600f, 400f, 0f, 0f, 0f, 0f, Seg);

        Assert.AreEqual(4, points.Length,
            "нулевой радиус обязан давать ОДНУ точку в углу: дуга из совпадающих точек "
            + "порождает отрезки нулевой длины, а на них не построить нормаль боковины");
        CollectionAssert.AreEqual(SharpCorners, points, "порядок обхода зафиксирован: (−X,−Z) → (+X,−Z) → (+X,+Z) → (−X,+Z), и по нему же идут параметры радиусов");
    }

    [Test]
    public void NegativeRadius_IsTreatedAsSquareCorner()
    {
        var points = RoundedRectProfile.Build(600f, 400f, -50f, 0f, 0f, 0f, Seg);

        Assert.AreEqual(4, points.Length, "отрицательный радиус не даёт дуги");
        Assert.IsTrue(Has(points, SharpCorners[0]),
            "отрицательный радиус — ошибка вызывающего, и угол обязан остаться острым, "
            + "а не вывернуться наружу габарита");
    }

    [Test]
    public void EachRadiusParameter_CutsItsOwnCornerAndOnlyIt()
    {
        for (int cut = 0; cut < 4; cut++)
        {
            var r = new float[4];
            r[cut] = 100f;
            var points = RoundedRectProfile.Build(600f, 400f, r[0], r[1], r[2], r[3], Seg);

            Assert.AreEqual(4 + Seg, points.Length, "3 острых угла + дуга из segments+1 точек");

            for (int corner = 0; corner < 4; corner++)
                Assert.AreEqual(corner != cut, Has(points, SharpCorners[corner]),
                    "радиус №" + cut + " обязан срезать угол №" + corner + " и только его: "
                    + "порядок параметров = порядок обхода (−X,−Z) → (+X,−Z) → (+X,+Z) → (−X,+Z)");
        }
    }

    [Test]
    public void PointCount_IsFourPlusSegmentsPerRoundedCorner()
    {
        var cases = new[]
        {
            new[] { 0f, 0f, 0f, 0f },
            new[] { 50f, 0f, 0f, 0f },
            new[] { 50f, 60f, 0f, 0f },
            new[] { 50f, 60f, 70f, 0f },
            new[] { 50f, 60f, 70f, 80f },
        };

        foreach (var r in cases)
        {
            var points = RoundedRectProfile.Build(600f, 400f, r[0], r[1], r[2], r[3], Seg);
            Assert.AreEqual(4 + r.Count(v => v > 0f) * Seg, points.Length,
                "формула числа точек: 4 + (ненулевых углов) × segments");
        }
    }

    [Test]
    public void WindingIsCounterClockwise_ForEveryCombinationOfRadii()
    {
        var radii = new[] { 0f, 30f, 150f, 400f };

        foreach (var a in radii)
        foreach (var b in radii)
        foreach (var c in radii)
        foreach (var d in radii)
        {
            var points = RoundedRectProfile.Build(600f, 400f, a, b, c, d, Seg);
            Assert.Greater(RoundedRectProfile.SignedArea(points), 0f,
                "обход обязан идти ПРОТИВ часовой стрелки при любых радиусах: внешняя "
                + "нормаль боковины строится как (dir.y, −dir.x), и при обратном обходе "
                + "боковины смотрят внутрь детали. Радиусы: " + a + " " + b + " " + c + " " + d);
        }
    }

    [Test]
    public void BoundingBox_IsExactlyWidthByDepth_AndCentredOnZero()
    {
        var radii = new[] { 0f, 30f, 200f, 5000f };

        foreach (var a in radii)
        foreach (var b in radii)
        {
            var points = RoundedRectProfile.Build(600f, 400f, a, b, a, b, Seg);

            Assert.AreEqual(600f, Size(points).x, 1e-3f, "габарит по X = ширина, радиусы "
                + a + "/" + b);
            Assert.AreEqual(400f, Size(points).y, 1e-3f, "габарит по Z = глубина, радиусы "
                + a + "/" + b);
            Assert.AreEqual(0f, Centre(points).x, 1e-3f, "контур строится сразу в центрированной "
                + "системе — радиусная полка обязана встать на то же место, что и раньше");
            Assert.AreEqual(0f, Centre(points).y, 1e-3f, "то же по Z: смещать контур после сборки больше некому");
        }
    }

    [Test]
    public void NoTwoConsecutivePointsCoincide_EvenWhereArcsMeet()
    {
        var cases = new[]
        {
            new Vector3(600f, 400f, 200f),
            new Vector3(400f, 400f, 200f),
            new Vector3(400f, 400f, 5000f),
            new Vector3(1200f, 400f, 200f),
            new Vector3(600f, 400f, 0f),
        };

        foreach (var c in cases)
        {
            var points = RoundedRectProfile.Uniform(c.x, c.y, c.z, Seg);
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                Assert.Greater((a - b).magnitude, RoundedRectProfile.MinPointSpacing(c.x, c.y),
                    "контур замкнут и не содержит повторов подряд: на стыке дуг точки "
                    + "совпадают, и оставленный дубликат даёт вырожденный треугольник крышки "
                    + "и нулевую нормаль боковины. Случай Ш×Г×R = " + c);
            }
        }
    }

    [Test]
    public void SingleRoundedCorner_IsFittedToTheShorterSide()
    {
        var points = RoundedRectProfile.Build(600f, 400f, 0f, 0f, 10000f, 0f, Seg);

        Assert.AreEqual(400f, EffectiveRadius(points, 600f, 400f), 1e-3f,
            "у одиночного угла соседи нулевые, поэтому правило «сумма на стороне ≤ её "
            + "длина» разрешает ровно min(Ш,Г) — столько же разрешает и сама радиусная полка");
        Assert.AreEqual(600f, Size(points).x, 1e-3f, "дуга не вправе уходить за габарит");
        Assert.AreEqual(400f, Size(points).y, 1e-3f, "и по Z тоже");
        Assert.IsTrue(Has(points, SharpCorners[0]), "противоположный угол остался острым");
    }

    [Test]
    public void AdjacentRadiiOnOneEdge_AreFittedSoTheirSumNeverExceedsIt()
    {
        foreach (float asked in new[] { 100f, 199f, 201f, 260f, 1000f })
        {
            var points = RoundedRectProfile.Uniform(600f, 400f, asked, Seg);
            float actual = EffectiveRadius(points, 600f, 400f);

            Assert.AreEqual(Mathf.Min(asked, 200f), actual, 1e-3f,
                "два радиуса на короткой стороне (400) обязаны быть подрезаны до 200 каждый: "
                + "иначе дуги перехлёстываются и контур самопересекается. Запрошено " + asked);
            Assert.AreEqual(600f, Size(points).x, 1e-3f, "подрезка не вправе менять габарит детали");
            Assert.AreEqual(400f, Size(points).y, 1e-3f, "подрезка не вправе менять габарит детали");
        }
    }

    [Test]
    public void UnequalRadii_AreFittedProportionally_KeepingTheirRatio()
    {
        var points = RoundedRectProfile.Build(600f, 400f, 0f, 100f, 300f, 0f, Seg);

        Assert.AreEqual(300f, EffectiveRadius(points, 600f, 400f), 1e-3f,
            "100 + 300 = 400 — ровно правая сторона, подрезать нечего");

        var tight = RoundedRectProfile.Build(600f, 400f, 0f, 200f, 600f, 0f, Seg);
        Assert.AreEqual(300f, EffectiveRadius(tight, 600f, 400f), 1e-3f,
            "200 + 600 = 800 при стороне 400: оба радиуса делятся пополам, соотношение 1:3 "
            + "сохраняется");
    }

    [Test]
    public void SquareWithHalfRadius_IsACircle()
    {
        var points = RoundedRectProfile.Uniform(400f, 400f, 200f, Seg);

        Assert.AreEqual(4 * Seg, points.Length,
            "у круга стыки соседних дуг совпадают, и дубликаты обязаны схлопнуться: "
            + "4 × (segments + 1) − 4");
        foreach (var p in points)
            Assert.AreEqual(200f, p.magnitude, 1e-3f,
                "каждая точка круга удалена от центра ровно на min(Ш,Г)/2");
        Assert.AreEqual(1, points.Count(p => Mathf.Abs(p.y - 200f) < 1e-3f),
            "у круга ровно одна верхняя точка — этим он и отличается от стадиона");
    }

    [Test]
    public void StretchedRectangleWithHalfRadius_IsAStadiumNotAnEllipse()
    {
        var points = RoundedRectProfile.Uniform(1000f, 400f, 200f, Seg);

        var onTop = points.Where(p => Mathf.Abs(p.y - 200f) < 1e-3f).OrderBy(p => p.x).ToArray();
        Assert.AreEqual(2, onTop.Length,
            "у стадиона длинная сторона ПРЯМАЯ, и её концы — две разные точки на максимуме Z; "
            + "у эллипса точка максимума одна");
        Assert.AreEqual(-300f, onTop[0].x, 1e-3f, "прямой участок начинается там, где кончается дуга: Ш/2 − R");
        Assert.AreEqual(300f, onTop[1].x, 1e-3f, "и кончается симметрично");
        Assert.AreEqual(200f, EffectiveRadius(points, 1000f, 400f), 1e-3f,
            "торцы — полуокружности радиусом Г/2");
    }

    [Test]
    public void SegmentsBelowOne_StillProducesAClosedContour()
    {
        var points = RoundedRectProfile.Uniform(600f, 400f, 100f, 0);

        Assert.AreEqual(8, points.Length, "segments < 1 вырождается в один сегмент на угол");
        Assert.Greater(RoundedRectProfile.SignedArea(points), 0f, "вырожденное число сегментов не вправе вывернуть обход");
    }

    [Test]
    public void SignedArea_IsNegativeForAClockwiseContour()
    {
        var ccw = RoundedRectProfile.Build(600f, 400f, 0f, 0f, 0f, 0f, Seg);
        var cw = ccw.Reverse().ToArray();

        Assert.AreEqual(600f * 400f, RoundedRectProfile.SignedArea(ccw), 1e-1f, "площадь прямоугольника со знаком плюс — это и есть обход против часовой");
        Assert.AreEqual(-600f * 400f, RoundedRectProfile.SignedArea(cw), 1e-1f,
            "знак площади — то самое, чем тест отличает обход по часовой от обхода против; "
            + "без этого проверка направления обхода ничего не проверяет");
    }
}
