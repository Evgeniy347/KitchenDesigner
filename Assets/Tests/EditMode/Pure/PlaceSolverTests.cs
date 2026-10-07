using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class PlaceSolverTests
{
    private static BoxMm Box(float x0, float y0, float z0, float x1, float y1, float z1) =>
        new BoxMm(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));

    private static readonly Vector3 Cabinet = new Vector3(600f, 720f, 560f);

    private static List<NeighbourBox> Kitchen() => new List<NeighbourBox>
    {
        new NeighbourBox("Floor", Box(-1000, -20, -1000, 6000, 0, 4000)),
        new NeighbourBox("Wall", Box(0, 0, -100, 4000, 2700, 100)),
        new NeighbourBox("Win1", Box(1500, 900, -100, 2700, 2100, 100)),
    };

    private static PlaceSpec Item(params object[] ops)
    {
        var item = new PlaceSpec { Name = "Cab" };
        foreach (var op in ops)
        {
            if (op is PlaceAgainstSpec a) item.Against.Add(a);
            if (op is PlaceAlignSpec l) item.Align.Add(l);
        }
        return item;
    }

    private static PlaceAgainstSpec Against(string target, string face, float gap = 0f) =>
        new PlaceAgainstSpec(target, face, gap);

    private static PlaceAlignSpec Align(string target, string axis, string at, float offset = 0f) =>
        new PlaceAlignSpec(target, axis, at, offset);

    private static PlaceOutcome Solve(PlaceSpec item, List<NeighbourBox>? scene = null, Vector3? current = null) =>
        PlaceSolver.Solve("Cab", item, Cabinet, current, scene ?? Kitchen(), 0f);

    [Test]
    public void Solve_CentredUnderTheWindow_AgainstTheWallsFront_OnTheFloor_NeedsNoCoordinate()
    {
        var outcome = Solve(Item(Against("Wall", "front"), Align("Win1", "x", "center")));

        Assert.IsTrue(outcome.Ok, string.Join(" | ", outcome.Problems));
        Assert.AreEqual(2100f - 300f, outcome.MinMm.x, 1e-3f, "центр шкафа на центре окна 1500..2700");
        Assert.AreEqual(0f, outcome.MinMm.y, 1e-3f, "по умолчанию стоит на полу");
        Assert.AreEqual(100f, outcome.MinMm.z, 1e-3f, "вплотную к лицевой грани стены (z=100)");
    }

    [Test]
    public void Solve_NextToANeighbour_UsesTheNeighboursFaceAndTheGap()
    {
        var scene = Kitchen();
        scene.Add(new NeighbourBox("Cab2", Box(1800, 0, 100, 2400, 720, 660)));

        var left = Solve(Item(Against("Wall", "front"), Against("Cab2", "left", 12f)), scene);
        var right = Solve(Item(Against("Wall", "front"), Against("Cab2", "right")), scene);

        Assert.IsTrue(left.Ok, string.Join(" | ", left.Problems));
        Assert.AreEqual(1800f - 12f - 600f, left.MinMm.x, 1e-3f, "слева от соседа: наш max = его min минус зазор");
        Assert.IsTrue(right.Ok, string.Join(" | ", right.Problems));
        Assert.AreEqual(2400f, right.MinMm.x, 1e-3f, "справа: наш min = его max");
    }

    [Test]
    public void Solve_OnAnotherPart_RestsOnItsTop_AndLiftRaisesIt()
    {
        var scene = Kitchen();
        scene.Add(new NeighbourBox("Base", Box(0, 0, 100, 600, 720, 660)));
        var item = Item(Against("Wall", "front"), Align("Base", "x", "min"));
        item.On = "Base";
        item.LiftMm = 5f;

        var outcome = Solve(item, scene);

        Assert.IsTrue(outcome.Ok, string.Join(" | ", outcome.Problems));
        Assert.AreEqual(725f, outcome.MinMm.y, 1e-3f);
    }

    [Test]
    public void Solve_LiftWithTheFloorDefault_HangsThePartAboveTheFloor()
    {
        var item = Item(Against("Wall", "front"), Align("Win1", "x", "min"));
        item.LiftMm = 1400f;

        var outcome = PlaceSolver.Solve("Cab", item, new Vector3(600, 400, 300), null, Kitchen(), 0f);

        Assert.AreEqual(1400f, outcome.MinMm.y, 1e-3f, "навесной шкаф: дно на lift_mm над полом");
    }

    [Test]
    public void Solve_NewPartWithoutX_IsRefusedNamingTheMissingAxisAndHowToFixIt()
    {
        var outcome = Solve(Item(Against("Wall", "front")));

        Assert.IsFalse(outcome.Ok);
        var problem = outcome.Problems.Single();
        StringAssert.StartsWith("x is not determined", problem);
        StringAssert.Contains("align", problem, "отказ предлагает конкретный параметр, а не «неполно»");
    }

    [Test]
    public void Solve_AnExistingPart_KeepsItsCurrentCoordinateOnAnAxisNobodyFixes()
    {
        var outcome = Solve(Item(Align("Win1", "x", "center")), null, new Vector3(0f, 0f, 500f));

        Assert.IsTrue(outcome.Ok, string.Join(" | ", outcome.Problems));
        Assert.AreEqual(500f, outcome.MinMm.z, 1e-3f, "перестановка существующей детали не обязана перечислять все оси");
        Assert.AreEqual(0f, outcome.MinMm.y, 1e-3f, "высота тоже осталась, а не провалилась на пол");
    }

    [Test]
    public void Solve_TwoConstraintsOnOneAxisThatDisagree_AreRefusedWithBothSourcesNamed()
    {
        var scene = Kitchen();
        scene.Add(new NeighbourBox("Cab2", Box(1800, 0, 100, 2400, 720, 660)));

        var outcome = Solve(Item(Against("Wall", "front"), Against("Cab2", "left"), Align("Win1", "x", "center")), scene);

        Assert.IsFalse(outcome.Ok);
        var problem = outcome.Problems.Single();
        StringAssert.Contains("x is fixed twice", problem);
        StringAssert.Contains("against 'Cab2' face left", problem);
        StringAssert.Contains("align 'Win1' x center", problem);
    }

    [Test]
    public void Solve_UnknownTarget_NamesTheClosestExistingParts()
    {
        var outcome = Solve(Item(Against("Wal", "front"), Align("Win1", "x", "center")));

        Assert.IsFalse(outcome.Ok);
        StringAssert.Contains("Element not found: Wal", outcome.Problems[0]);
        StringAssert.Contains("closest names: Wall", outcome.Problems[0]);
    }

    [Test]
    public void Solve_UnknownFace_ListsTheValidOnes()
    {
        var outcome = Solve(Item(Against("Wall", "inside"), Align("Win1", "x", "center")));

        StringAssert.Contains("Valid faces: left|right (X), bottom|top (Y), back|front (Z)", outcome.Problems[0]);
    }

    [Test]
    public void Solve_FaceNamesTheTargetsSide_FrontIsPlusZ_BackIsMinusZ()
    {
        var front = Solve(Item(Against("Wall", "front"), Align("Win1", "x", "center")));
        var back = Solve(Item(Against("Wall", "back"), Align("Win1", "x", "center")));

        Assert.AreEqual(100f, front.MinMm.z, 1e-3f, "front: деталь по +Z от стены");
        Assert.AreEqual(-100f - 560f, back.MinMm.z, 1e-3f, "back: деталь по -Z от стены, её max z = -100");
    }

    [Test]
    public void Solve_ASpotInsideANeighbour_IsRefusedWithAWayToStandNextToIt()
    {
        var scene = Kitchen();
        scene.Add(new NeighbourBox("Cab2", Box(1800, 0, 100, 2400, 720, 660)));

        var outcome = Solve(Item(Against("Wall", "front"), Align("Win1", "x", "center")), scene);

        Assert.IsFalse(outcome.Ok);
        var problem = outcome.Problems.Single();
        StringAssert.Contains("overlaps 'Cab2'", problem);
        StringAssert.Contains("against {target:'Cab2', face:", problem, "подсказка называет грань соседа, у которой встать");
    }

    [Test]
    public void Solve_OverlapWithTheFloorSlab_SuggestsStandingOnIt()
    {
        var scene = Kitchen();
        var item = Item(Against("Wall", "front"), Align("Win1", "x", "center"));
        item.LiftMm = -50f;

        var outcome = Solve(item, scene);

        Assert.IsFalse(outcome.Ok);
        StringAssert.Contains("on:'Floor'", outcome.Problems.Single());
    }

    [Test]
    public void Solve_APartCannotBeRelativeToItself()
    {
        var outcome = Solve(Item(Against("Cab", "left"), Align("Win1", "x", "center")));

        StringAssert.Contains("relative to itself", outcome.Problems[0]);
    }

    [Test]
    public void Solve_NegativeGap_IsRefusedWithAHint()
    {
        var outcome = Solve(Item(Against("Wall", "front", -5f), Align("Win1", "x", "center")));

        StringAssert.Contains("use 0 for flush contact", outcome.Problems[0]);
    }

    [Test]
    public void FaceToStandBy_PicksTheFaceOfTheOtherPartOnOurSideOfTheShallowestPenetration()
    {
        var theirs = Box(1000, 0, 0, 1600, 720, 560);

        Assert.AreEqual("left", PlaceSolver.FaceToStandBy(Box(900, 0, 0, 1100, 720, 560), theirs));
        Assert.AreEqual("right", PlaceSolver.FaceToStandBy(Box(1500, 0, 0, 1700, 720, 560), theirs));
        Assert.AreEqual("top", PlaceSolver.FaceToStandBy(Box(1000, 700, 0, 1600, 900, 560), theirs));
    }
}
