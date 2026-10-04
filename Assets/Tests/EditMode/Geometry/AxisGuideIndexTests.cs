using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Луч вдоль мировой оси и ориентированные коробки: на нём стоят и направляющие
/// расстояний при перетаскивании, и прилипание к равному зазору. Без сцены — исполняется и
/// Unity, и обычным dotnet test.</summary>
public class AxisGuideIndexTests
{
    private const float Mm = AppConstants.MM_TO_UNITS;
    private const float Tol = 0.0001f;

    private static AxisBox Board(int id, Vector3 centre, Vector3 sizeMm) =>
        new AxisBox(id, centre, Vector3.right, Vector3.up, Vector3.forward, sizeMm * (Mm * 0.5f));

    private static AxisBox TurnedAboutY(int id, Vector3 centre, Vector3 sizeMm, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        var a = new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
        var c = new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        return new AxisBox(id, centre, a, Vector3.up, c, sizeMm * (Mm * 0.5f));
    }

    [Test]
    public void Spans_AlignedBox_GivesBothFaceCoordinates()
    {
        var box = Board(1, new Vector3(2f, 1f, 0f), new Vector3(600f, 2000f, 16f));

        bool hit = box.Spans(new Vector3(0f, 1f, 0f), 0, out float enter, out float exit);

        Assert.IsTrue(hit, "линия вдоль X через центр доски обязана её пересечь");
        Assert.AreEqual(1.7f, enter, Tol, "вход — ближняя грань: 2 м минус полширины 300 мм");
        Assert.AreEqual(2.3f, exit, Tol, "выход — дальняя грань");
    }

    [Test]
    public void Spans_BoxTurned45Degrees_EntersAtTheCornerNotAtTheFace()
    {
        var box = TurnedAboutY(1, Vector3.zero, new Vector3(1000f, 1000f, 1000f), 45f);

        box.Spans(new Vector3(-5f, 0f, 0f), 0, out float enter, out float exit);

        float halfDiagonal = 0.5f * Mathf.Sqrt(2f);
        Assert.AreEqual(5f - halfDiagonal, enter, Tol,
            "повёрнутая на 45° коробка встречает луч ребром: расстояние до неё меньше, чем до "
            + "её осевого полугабарита 0,5 м, — луч обязан мерить ориентированную коробку");
        Assert.AreEqual(5f + halfDiagonal, exit, Tol, "и выходит через противоположное ребро");
    }

    [Test]
    public void Spans_LinePassingBesideTheBox_IsNoHit()
    {
        var box = Board(1, new Vector3(2f, 1f, 0f), new Vector3(600f, 2000f, 16f));

        bool hit = box.Spans(new Vector3(0f, 1f, 0.5f), 0, out _, out _);

        Assert.IsFalse(hit, "линия в 0,5 м сбоку от доски толщиной 16 мм её не пересекает");
    }

    [Test]
    public void Of_GeometryOfATurnedBox_RecoversCentreAxesAndHalfSize()
    {
        float r = 30f * Mathf.Deg2Rad * 0.5f;
        var rotation = new Quaternion(0f, Mathf.Sin(r), 0f, Mathf.Cos(r));
        var geometry = ElementGeometry.Box("B", new Vector3(1f, 0.5f, -2f),
            new Vector3(0.6f, 0.72f, 0.3f), rotation);

        var box = AxisBox.Of(geometry);

        Assert.AreEqual(0f, (box.Centre - new Vector3(1f, 0.5f, -2f)).magnitude, Tol,
            "центр коробки — среднее шести центров граней");
        Assert.AreEqual(0f, (box.Half - new Vector3(0.3f, 0.36f, 0.15f)).magnitude, Tol,
            "полугабариты по собственным осям, а не по мировым");
        Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(box.AxisA, rotation * Vector3.right)), Tol,
            "первая ось — нормаль грани 0 (контракт порядка граней: index/2 = ось)");
        Assert.AreEqual(geometry.Min.x, box.Min.x, Tol, "осевая огибающая совпадает с геометрией");
        Assert.AreEqual(geometry.Max.z, box.Max.z, Tol, "осевая огибающая совпадает с геометрией");
    }

    [Test]
    public void Cast_TwoBoxesAhead_NearIsTheCloserAndFarIsTheNextOne()
    {
        var index = new AxisGuideIndex(new[]
        {
            Board(2, new Vector3(0f, 1f, 1.2f), new Vector3(600f, 2000f, 16f)),
            Board(1, new Vector3(0f, 1f, 0.6f), new Vector3(600f, 2000f, 16f)),
        });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 2, 1, 0.008f);

        Assert.IsTrue(cast.HasNear && cast.HasFar, "обе доски на линии обязаны найтись");
        Assert.AreEqual(1, cast.Near.Id, "ближняя — та, что на 0,6 м, а не первая в списке");
        Assert.AreEqual(2, cast.Far.Id, "дальняя — следующая за ближней");
        Assert.AreEqual(0.584f, cast.NearGap, Tol, "зазор от грани до грани: 592 − 8 мм");
        Assert.AreEqual(0.584f, cast.FarGap, Tol, "зазор между соседями тоже от грани до грани");
    }

    [Test]
    public void Cast_MinusDirection_MeasuresToTheFacingFaceBehind()
    {
        var index = new AxisGuideIndex(new[] { Board(1, new Vector3(0f, 1f, -1f), new Vector3(600f, 2000f, 16f)) });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 2, -1, 0.008f);

        Assert.IsTrue(cast.HasNear, "доска позади обязана найтись лучом в минус");
        Assert.AreEqual(0.992f - 0.008f, cast.NearGap, Tol,
            "вход в минус-направлении — грань, обращённая к нам (z = −0,992), а не дальняя");
    }

    [Test]
    public void Cast_BoxBeyondTenMetres_IsNotHit()
    {
        var index = new AxisGuideIndex(new[] { Board(1, new Vector3(10.2f, 1f, 0f), new Vector3(16f, 2000f, 600f)) });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 0, 1, 0.1f);

        Assert.IsFalse(cast.HasNear,
            "грань в 10,092 м от нашей грани дальше предела 10 м — по этому направлению ничего");
    }

    [Test]
    public void Cast_BoxJustInsideTenMetres_IsHit()
    {
        var index = new AxisGuideIndex(new[] { Board(1, new Vector3(10.05f, 1f, 0f), new Vector3(16f, 2000f, 600f)) });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 0, 1, 0.1f);

        Assert.IsTrue(cast.HasNear, "9,942 м — внутри предела, обязана найтись (пара к тесту выше)");
    }

    [Test]
    public void Cast_BoxAroundTheOrigin_IsSkippedAsOurOwnHost()
    {
        var wall = Board(1, new Vector3(0f, 1.35f, 0f), new Vector3(4000f, 2700f, 250f));
        var neighbour = Board(2, new Vector3(1.5f, 1f, 0f), new Vector3(600f, 1200f, 250f));
        var index = new AxisGuideIndex(new[] { wall, neighbour });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 0, 1, 0.3f);

        Assert.AreEqual(2, cast.Near.Id,
            "окно сидит внутри стены: стена начинается позади нашей грани и не может быть "
            + "«ближайшей» — иначе направляющая окна всегда показывала бы её");
    }

    [Test]
    public void Cast_TouchingNeighbour_IsNearWithZeroGap()
    {
        var index = new AxisGuideIndex(new[]
        {
            Board(1, new Vector3(0.308f, 1f, 0f), new Vector3(600f, 2000f, 16f)),
            Board(2, new Vector3(1f, 1f, 0f), new Vector3(16f, 2000f, 600f)),
        });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 0, 1, 0.008f);

        Assert.AreEqual(1, cast.Near.Id, "сосед вплотную закрывает собой всё, что дальше");
        Assert.AreEqual(0f, cast.NearGap, Tol, "и его зазор — ноль");
    }

    [Test]
    public void Cast_FarOverlappingTheNear_IsNotTakenAsTheNextNeighbour()
    {
        var index = new AxisGuideIndex(new[]
        {
            Board(1, new Vector3(0f, 1f, 1f), new Vector3(600f, 2000f, 100f)),
            Board(2, new Vector3(0f, 1f, 1.02f), new Vector3(600f, 2000f, 16f)),
            Board(3, new Vector3(0f, 1f, 2f), new Vector3(600f, 2000f, 16f)),
        });

        var cast = index.Cast(new Vector3(0f, 1f, 0f), 2, 1, 0f);

        Assert.AreEqual(1, cast.Near.Id, "ближняя — толстая доска");
        Assert.AreEqual(3, cast.Far.Id,
            "доска, утопленная внутрь ближней, не сосед за ней — следующей идёт та, что за её "
            + "дальней гранью");
    }

    [Test]
    public void Cast_HugeFloor_IsFoundThroughTheEverywhereList()
    {
        var floor = Board(1, new Vector3(0f, -0.05f, 0f), new Vector3(60000f, 100f, 60000f));
        var index = new AxisGuideIndex(new[] { floor });

        var cast = index.Cast(new Vector3(3f, 1f, 7f), 1, -1, 0.5f);

        Assert.IsTrue(cast.HasNear,
            "пол 60×60 м занимает больше клеток, чем разрешено одной коробке, и лежит в общем "
            + "списке — луч вниз всё равно обязан его найти");
        Assert.AreEqual(0.5f, cast.NearGap, Tol, "низ детали на 0,5 м над полом");
    }

    [Test]
    public void Cast_SceneGrowsOffTheLine_BoxTestsPerCastStayFlat()
    {
        int small = BoxTestsPerCast(farBoxes: 100);
        int large = BoxTestsPerCast(farBoxes: 400);

        Assert.Greater(small, 0, "счётчик обязан что-то считать, иначе равенство ниже пустое");
        Assert.AreEqual(small, large,
            "детали в стороне от линии не должны стоить лучу ничего: индекс строится один раз "
            + "на жест, и луч спрашивает одну клетку, а не перебирает сцену. Квадратичная форма "
            + "«на каждый луч каждого кадра — вся сцена» дала бы рост вчетверо");
        Assert.Less(small, 100, "и само число проверок меньше размера сцены");
    }

    private static int BoxTestsPerCast(int farBoxes)
    {
        var boxes = new List<AxisBox>();
        for (int i = 0; i < 10; i++)
            boxes.Add(Board(i, new Vector3(0f, 1f, 0.5f + i * 0.6f), new Vector3(600f, 2000f, 16f)));
        for (int i = 0; i < farBoxes; i++)
            boxes.Add(Board(100 + i, new Vector3(20f + (i % 20) * 1f, 1f, 20f + (i / 20) * 1f),
                new Vector3(600f, 2000f, 600f)));

        var index = new AxisGuideIndex(boxes);
        index.Cast(new Vector3(0f, 1f, 0f), 2, 1, 0.008f);
        return index.BoxTests;
    }
}
