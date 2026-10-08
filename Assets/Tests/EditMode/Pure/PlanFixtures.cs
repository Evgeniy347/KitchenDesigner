using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public static class PlanFixtures
{
    public static DigestEntry Entry(string name, string kind, float x0, float y0, float z0, float x1, float y1, float z1,
        string? issue = null, int parts = 0)
    {
        var entry = new DigestEntry
        {
            Name = name,
            Kind = parts > 0 ? "module" : kind,
            PartsCount = parts,
            Box = new BoxMm(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1)),
        };
        if (issue != null) entry.Placement.issues.Add(issue);
        return entry;
    }

    public static DigestInput Kitchen()
    {
        var input = new DigestInput();
        input.Entries.Add(Entry("Floor", "floor", 0, -20, 0, 4000, 0, 3000));
        input.Entries.Add(Entry("Wall_S", "wall", -100, 0, -100, 4100, 2700, 0));
        input.Entries.Add(Entry("Wall_N", "wall", -100, 0, 3000, 4100, 2700, 3100));
        input.Entries.Add(Entry("Wall_W", "wall", -100, 0, 0, 0, 2700, 3000));
        input.Entries.Add(Entry("Wall_E", "wall", 4000, 0, 0, 4100, 2700, 3000));
        input.Entries.Add(Entry("Window_1", "window", 1500, 900, -100, 2500, 2100, 0));
        input.Entries.Add(Entry("Door_1", "door", 3000, 0, 3000, 3900, 2100, 3100));
        input.Entries.Add(Entry("Cab01", "board", 0, 0, 0, 600, 720, 560));
        input.Entries.Add(Entry("Cab02", "board", 600, 0, 0, 1200, 720, 560));
        input.Entries.Add(Entry("Cab03", "board", 1190, 0, 0, 1790, 720, 560, issue: "overlap Cab02 10mm"));
        input.Entries.Add(Entry("Sink1", "sink", 1800, 0, 0, 2400, 850, 560));
        input.Entries.Add(Entry("Cooktop1", "cooktop", 2400, 0, 0, 3000, 40, 560));
        input.Entries.Add(Entry("Cab_Left_Tall_Section", "board", 3000, 0, 0, 3600, 2100, 560));
        input.Entries.Add(Entry("Upper", "board", 0, 1400, 0, 1800, 2100, 320, parts: 4));
        input.Entries.Add(Entry("Table1", "table", 1200, 0, 1500, 2400, 750, 2100));
        input.Entries.Add(Entry("Chair1", "chair", 1500, 0, 2150, 1900, 900, 2550));
        input.Rooms.Add(new DigestGroup { Id = "kitchen", PolygonXz = new[] { 0, 0, 4000, 0, 4000, 3000, 0, 3000 } });
        return input;
    }
}
