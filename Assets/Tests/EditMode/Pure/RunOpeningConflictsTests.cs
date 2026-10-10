using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class RunOpeningConflictsTests
{
    private static BoxMm Box(float x0, float y0, float z0, float x1, float y1, float z1) =>
        new BoxMm(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));

    private static readonly RunWall Wall = new RunWall("Wall", Box(0, 0, -100, 4000, 2700, 100), 0, true);

    private static readonly NeighbourBox Window = new NeighbourBox("Win1", Box(1500, 900, -100, 2700, 2100, 100));

    private static readonly NeighbourBox Door = new NeighbourBox("Door1", Box(3000, 0, -100, 3900, 2000, 100));

    private static RunPlaced Cabinet(string name, RunKind kind, float x0, float x1, float y0, float y1) =>
        new RunPlaced(name, kind, Box(x0, y0, 100, x1, y1, 420));

    private static List<string> Find(RunPlaced cabinet, params NeighbourBox[] openings) =>
        RunOpeningConflicts.Find(Wall, new[] { cabinet }, openings);

    [Test]
    public void Find_AWallCabinetOverTheWindow_IsAConflict_NamingBothAndAWayOut()
    {
        var problems = Find(Cabinet("W2", RunKind.Wall, 1200, 1800, 1400, 2120), Window);

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains("wall cabinet 'W2'", problems[0]);
        StringAssert.Contains("window 'Win1'", problems[0]);
        StringAssert.Contains("start_mm", problems[0], "что сдвинуть");
        StringAssert.Contains("kind base", problems[0], "и чем заменить");
    }

    [Test]
    public void Find_ABaseCabinetUnderTheSill_IsNotAConflict()
    {
        Assert.IsEmpty(Find(Cabinet("B2", RunKind.Base, 1200, 1800, 0, 720), Window),
            "720 < подоконник 900: шкаф стоит под окном, и сценарий «три шкафа под окном» обязан проходить");
    }

    [Test]
    public void Find_ATallCabinetInFrontOfTheWindow_IsAConflict()
    {
        var problems = Find(Cabinet("T1", RunKind.Tall, 1500, 2100, 0, 2100), Window);

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains("tall cabinet 'T1'", problems[0]);
    }

    [Test]
    public void Find_AWallCabinetBesideTheWindow_IsNotAConflict()
    {
        Assert.IsEmpty(Find(Cabinet("W1", RunKind.Wall, 900, 1500, 1400, 2120), Window),
            "касание по x (1500) — не пересечение");
        Assert.IsEmpty(Find(Cabinet("W3", RunKind.Wall, 2700, 3300, 1400, 2120), Window));
    }

    [Test]
    public void Find_AWallCabinetUnderTheWindowSill_IsNotAConflict_WhenItsTopIsBelowTheSill()
    {
        Assert.IsEmpty(Find(Cabinet("W9", RunKind.Wall, 1500, 2100, 100, 800), Window));
    }

    [Test]
    public void Find_ABaseCabinetInFrontOfADoor_IsAConflict_AndSuggestsStartingAfterTheDoor()
    {
        var problems = Find(Cabinet("B7", RunKind.Base, 3200, 3800, 0, 720), Door);

        Assert.AreEqual(1, problems.Count);
        StringAssert.Contains("door 'Door1'", problems[0]);
        StringAssert.Contains("from:'Door1'", problems[0]);
    }

    [Test]
    public void Find_EveryBlockedPair_IsReportedOnce()
    {
        var cabinets = new[]
        {
            Cabinet("W1", RunKind.Wall, 1400, 2000, 1400, 2120),
            Cabinet("W2", RunKind.Wall, 2000, 2600, 1400, 2120),
            Cabinet("W3", RunKind.Wall, 2600, 3200, 1400, 2120),
        };

        var problems = RunOpeningConflicts.Find(Wall, cabinets, new[] { Window });

        Assert.AreEqual(3, problems.Count, "W1, W2 и W3 заходят на окно 1500..2700 (W3 — 2600..2700)");
    }
}
