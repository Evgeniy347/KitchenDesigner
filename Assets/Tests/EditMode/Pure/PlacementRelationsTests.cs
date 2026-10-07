using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class PlacementRelationsTests
{
    private static BoxMm Box(float x0, float y0, float z0, float x1, float y1, float z1) =>
        new BoxMm(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));

    private static NeighbourBox Named(string name, BoxMm box) => new NeighbourBox(name, box);

    private static readonly BoxMm Cabinet = Box(0, 0, 0, 600, 720, 560);

    private static List<NeighbourBox> Room() => new List<NeighbourBox>
    {
        Named("Floor", Box(-1000, -18, -100, 4000, 0, 4000)),
        Named("Wall", Box(-1000, 0, -100, 4000, 2700, 0)),
    };

    private static string Describe(PlacementRelationSet set) =>
        "touches=[" + string.Join(",", set.Touches.Select(t => t.n + "@" + t.face)) + "] gaps=["
        + string.Join(",", set.Gaps.Select(g => g.n + "@" + g.face + ":" + g.gapMm)) + "] on=" + set.On;

    [Test]
    public void Of_CabinetOnTheFloorAgainstTheWall_TouchesBothAndStandsOnTheFloor()
    {
        var set = PlacementRelations.Of(Cabinet, Room());

        CollectionAssert.AreEquivalent(new[] { "Floor@bottom", "Wall@back" },
            set.Touches.Select(t => t.n + "@" + t.face).ToList(), Describe(set));
        Assert.AreEqual("Floor", set.On, "стоит на том, что касается низа: слабой модели важен именно этот факт");
        Assert.IsEmpty(set.Gaps, "всё, что рядом, касается: зазоров нет");
        Assert.IsEmpty(set.Overlaps);
    }

    [Test]
    public void Of_BackIsTheMinimumZFace_AndFrontTheMaximum()
    {
        var inFront = Named("Island", Box(0, 0, 560, 600, 720, 1200));

        var set = PlacementRelations.Of(Cabinet, new List<NeighbourBox> { inFront });

        Assert.AreEqual("front", set.Touches.Single().face,
            "соседний по +Z касается грани front: так же зовёт её align_elements");
    }

    [Test]
    public void Of_NeighbourTwelveMillimetresAway_IsAGapOnThatFace_NotATouch()
    {
        var set = PlacementRelations.Of(Cabinet,
            new List<NeighbourBox> { Named("B2", Box(612, 0, 0, 1212, 720, 560)) });

        Assert.IsEmpty(set.Touches, "12 мм — не касание: допуск контакта 0,5 мм");
        var gap = set.Gaps.Single();
        Assert.AreEqual(("B2", "right"), (gap.n, gap.face));
        Assert.AreEqual(12f, gap.gapMm, 1e-3f);
    }

    [Test]
    public void Of_NeighbourFarBeyondReach_IsNotListed_SoADistantWallDoesNotDrownTheAnswer()
    {
        var set = PlacementRelations.Of(Cabinet,
            new List<NeighbourBox> { Named("Far", Box(600 + PlacementRelations.GapReachMm + 100f, 0, 0, 3000, 720, 560)) });

        Assert.IsEmpty(set.Gaps, Describe(set));
        Assert.IsEmpty(set.Touches);
    }

    [Test]
    public void Of_NeighbourJustInsideReach_IsListed()
    {
        var set = PlacementRelations.Of(Cabinet,
            new List<NeighbourBox> { Named("Near", Box(600 + PlacementRelations.GapReachMm - 1f, 0, 0, 3000, 720, 560)) });

        Assert.AreEqual("Near", set.Gaps.Single().n, "499 мм до соседа ещё видно: граница досягаемости не отрезает его");
    }

    [Test]
    public void Of_Overlap_GoesToOverlapsWithItsDepth_AndNeverToTouchesOrGaps()
    {
        var set = PlacementRelations.Of(Cabinet,
            new List<NeighbourBox> { Named("B2", Box(570, 0, 0, 1170, 720, 560)) });

        var overlap = set.Overlaps.Single();
        Assert.AreEqual("B2", overlap.n);
        Assert.AreEqual(30f, overlap.depthMm, 1e-3f, "глубина — наименьшее из трёх перекрытий");
        Assert.IsEmpty(set.Touches, "пересекающийся сосед — не «касается»");
        Assert.IsEmpty(set.Gaps);
    }

    [Test]
    public void Of_PenetrationBelowTheContactTolerance_IsATouch_NotAnOverlap()
    {
        var set = PlacementRelations.Of(Cabinet,
            new List<NeighbourBox> { Named("B2", Box(599.7f, 0, 0, 1200, 720, 560)) });

        Assert.IsEmpty(set.Overlaps, "0,3 мм — шум float, фильтруется как в get_violations");
        Assert.AreEqual(("B2", "right"), (set.Touches.Single().n, set.Touches.Single().face),
            "при этом сосед остаётся касающимся, а не пропадает из ответа вовсе");
    }

    [Test]
    public void Of_NeighbourOffsetOnAnotherAxis_IsNotFacingAtAll()
    {
        var set = PlacementRelations.Of(Cabinet,
            new List<NeighbourBox> { Named("Diagonal", Box(600, 0, 800, 1200, 720, 1400)) });

        Assert.IsEmpty(set.Touches, Describe(set));
        Assert.IsEmpty(set.Gaps, "проекции на оставшиеся оси не пересекаются — грань на него не смотрит");
    }

    [Test]
    public void Of_FloatingCabinet_HasNoSupport_AndReportsTheDropToTheFloor()
    {
        var floating = Box(0, 100, 0, 600, 820, 560);

        var set = PlacementRelations.Of(floating, Room());

        Assert.IsNull(set.On, "под ней ничего не касается: поле on пусто");
        var drop = set.Gaps.Single(g => g.face == "bottom");
        Assert.AreEqual(("Floor", 100f), (drop.n, drop.gapMm), "зазор до пола — как раз то, что слабая модель не видела");
    }

    [Test]
    public void Of_OnlyTheNearestOnAFaceIsAGap_AndATouchSuppressesFartherOnes()
    {
        var set = PlacementRelations.Of(Cabinet, new List<NeighbourBox>
        {
            Named("Near", Box(620, 0, 0, 900, 720, 560)),
            Named("Far", Box(700, 0, 0, 1200, 720, 560)),
            Named("Touching", Box(0, 0, -300, 600, 720, 0)),
            Named("BehindIt", Box(0, 0, -400, 600, 720, -350)),
        });

        Assert.AreEqual(new[] { "Near" }, set.Gaps.Select(g => g.n).ToArray(), Describe(set));
        Assert.AreEqual(new[] { "Touching" }, set.Touches.Select(t => t.n).ToArray(),
            "позади касается одна деталь, дальняя за ней не зазор: грань уже занята");
    }

    [Test]
    public void Of_StandingOnTwoThings_NamesTheOneUnderMoreOfTheFootprint()
    {
        var set = PlacementRelations.Of(Cabinet, new List<NeighbourBox>
        {
            Named("Small", Box(0, -50, 0, 100, 0, 560)),
            Named("Big", Box(100, -50, 0, 600, 0, 560)),
        });

        Assert.AreEqual("Big", set.On);
        Assert.AreEqual(2, set.Touches.Count(t => t.face == "bottom"), "касаются обе");
    }

    [Test]
    public void Of_ManyTouchingNeighboursOnOneFace_AreCapped()
    {
        var wallPieces = Enumerable.Range(0, 6)
            .Select(i => Named("W" + i, Box(i * 100, 0, -50, (i + 1) * 100, 720, 0)))
            .ToList();

        var set = PlacementRelations.Of(Cabinet, wallPieces);

        Assert.AreEqual(PlacementRelations.MaxTouchesPerFace, set.Touches.Count(t => t.face == "back"),
            "стена из шести кусков не должна раздувать ответ шестью строками про одну грань");
    }

    [Test]
    public void PenetrationMm_IsTheSmallestOfThreeOverlaps_AndZeroWhenAnyAxisIsApart()
    {
        Assert.AreEqual(30f, PlacementRelations.PenetrationMm(Cabinet, Box(570, 100, 100, 900, 300, 300)), 1e-3f);
        Assert.AreEqual(0f, PlacementRelations.PenetrationMm(Cabinet, Box(600, 0, 0, 900, 720, 560)),
            "касание гранью — это не проникновение");
        Assert.AreEqual(0f, PlacementRelations.PenetrationMm(Cabinet, Box(100, 800, 100, 300, 900, 300)));
    }
}
