using System;
using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Keybinding;

/// <summary>`ProjectJson.Serialize` used to run twelve JSON trims one after another, each of
/// them re-parsing and re-copying the whole project: the root object, the elements array, the
/// basePlate, the undo/redo history. On the user's project (412 elements, 1267 undo records,
/// 4.1 M characters before trimming) that chain took 2.35 s in the editor and allocated ~1 GB
/// per save, and autosave ran it every interval — the 5 s freeze in
/// test-results/perf/perf_20260927_095510.csv (frame 23032: 4988 ms, 995 MB of GC).
///
/// `ProjectJsonTrim.Apply` does all of it in ONE walk over the document. These tests keep two
/// promises: the bytes on disk are exactly what the chain wrote (the chain is the reference),
/// and the walk stays one walk — counted in characters read and bytes allocated, never in
/// milliseconds (conventions/PERFORMANCE.md).</summary>
public class ProjectJsonTrimTests
{
    private const int RealElementCount = 412;
    private const int RealRecordCount = 1267;

    private static readonly string[] FamilyFlags =
    {
        "isFoundation", "isFloorSlab", "isFence", "isInsulation", "isVentGap", "isCladding",
        "isRoof", "isDuct", "isGrille",
    };

    private static readonly string[] FamilyDetails =
    {
        "foundationSoilKind", "foundationSandMm", "foundationGravelMm", "foundationCompacted",
        "foundationConcreteGrade", "foundationRebarDiameterMm", "foundationRebarStepMm", "foundationCoverMm",
        "slabTechnology", "slabConcreteGrade", "slabRebarDiameterMm", "slabRebarStepMm",
        "fencePostSectionMm", "fencePostStepMm", "fencePitDepthMm", "fenceSheetMark",
        "wallLayerHostWallName", "ventGapBattenStepMm",
        "roofType", "roofRidgeAxis", "roofPitchDeg", "roofOverhangMm", "roofRafterStepMm",
        "ductProfileKind", "ductDiameterMm", "ductWidthMm", "ductHeightMm", "ductAirflowM3PerHour",
        "grilleAirflowM3PerHour",
    };

    [Test]
    public void Apply_OnEveryFamilyAndHistoryShape_EqualsTheOldTrimChain_ByteForByte()
    {
        string project = BuildProject(40, 30, emptyRootValues: true);
        string expected = OldChain(project);

        Assert.AreEqual(expected, ProjectJsonTrim.Apply(project),
            "the single pass must write exactly what the twelve chained trims wrote");
        Assert.AreNotEqual(project, expected, "the fixture must actually contain something to trim");
        StringAssert.DoesNotContain("\"createdAtUtc\"", expected, "the empty creation date is trimmed");
        StringAssert.DoesNotContain("\"keyBindings\"", expected, "empty key bindings are trimmed");
        StringAssert.Contains("\"isRoof\": true", expected, "a roof keeps its own family");
    }

    [Test]
    public void Apply_WhenRootValuesAreFilled_KeepsThem_AndStillEqualsTheOldChain()
    {
        string project = BuildProject(10, 5, emptyRootValues: false);
        string trimmed = ProjectJsonTrim.Apply(project);

        Assert.AreEqual(OldChain(project), trimmed);
        StringAssert.Contains("\"createdAtUtc\": \"2026-09-10T12:11:01Z\"", trimmed);
        StringAssert.Contains("\"keyBindings\": [", trimmed);
        StringAssert.Contains("\"levels\": [", trimmed);
    }

    [Test]
    public void Apply_OnARealSizeProject_ReadsEachObjectOnce_AndNeverRebuildsTheDocument()
    {
        string project = BuildProject(RealElementCount, RealRecordCount, emptyRootValues: true);
        TakeCounters(out _, out _);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        string trimmed = ProjectJsonTrim.Apply(project);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        TakeCounters(out long objectChars, out long removeMemberChars);

        Assert.Greater(project.Length, 3_000_000, "the fixture must be the size of the user's project");
        Assert.Less(trimmed.Length, project.Length, "and must have something to trim");
        Assert.AreEqual(0, removeMemberChars,
            "RemoveMember rebuilds its whole input per removed key — the save path must not call it at all");
        Assert.LessOrEqual(objectChars, project.Length,
            $"element objects are disjoint slices of the document, so reading each ONCE totals at most its "
            + $"length ({project.Length}); {objectChars} means some object was parsed again");
        long projectBytes = project.Length * 2L;
        Assert.Less(allocated, projectBytes * 4,
            $"one pass allocates the output and the element keys — {allocated} bytes for a "
            + $"{projectBytes}-byte document. The chain it replaced allocated ~120x the document per save");
    }

    [Test]
    public void Apply_CharactersRead_GrowLinearly_AsTheProjectQuadruples()
    {
        TakeCounters(out _, out _);
        ProjectJsonTrim.Apply(BuildProject(100, 300, emptyRootValues: true));
        TakeCounters(out long small, out _);
        ProjectJsonTrim.Apply(BuildProject(400, 1200, emptyRootValues: true));
        TakeCounters(out long large, out _);

        Assert.Greater(small, 0);
        Assert.Less(large, small * 5, $"4x the project must cost ~4x the reading, not {large}/{small}");
    }

    private static void TakeCounters(out long objectChars, out long removeMemberChars)
    {
        objectChars = JsonObjectEdit.TakeCharsRead();
        removeMemberChars = JsonText.TakeCharsProcessedByRemoveMember();
    }

    private static string OldChain(string json) =>
        LevelsJsonTrim.RemoveEmptyLevelsArray(
            ConvertStateHistoryJsonTrim.RemoveWhenNotNeeded(
                BasePlateFamilyJsonTrim.RemoveWhenNotNeeded(
                    KeyBindingsJsonTrim.RemoveWhenEmpty(
                        DuctJsonTrim.RemoveFromElementsArray(
                            RoofJsonTrim.RemoveFromElementsArray(
                                WallLayerJsonTrim.RemoveFromElementsArray(
                                    FenceJsonTrim.RemoveFromElementsArray(
                                        FloorSlabJsonTrim.RemoveFromElementsArray(
                                            FoundationJsonTrim.RemoveFromElementsArray(
                                                LevelsJsonTrim.RemoveEmptyLevelIdFromElements(
                                                    CreatedAtUtcJsonTrim.RemoveWhenEmpty(json))))))))))));

    private static string BuildProject(int elementCount, int recordCount, bool emptyRootValues)
    {
        var sb = new StringBuilder();
        sb.Append("{\n    \"version\": 1,\n    \"appVersion\": \"0.2022\",\n");
        sb.Append("    \"createdAtUtc\": ").Append(emptyRootValues ? "\"\"" : "\"2026-09-10T12:11:01Z\"").Append(",\n");
        sb.Append("    \"elements\": [");
        for (int i = 0; i < elementCount; i++)
        {
            sb.Append(i == 0 ? "\n        " : ",\n        ");
            AppendElement(sb, i, "        ");
        }
        sb.Append("\n    ],\n    \"groups\": [],\n");
        sb.Append("    \"levels\": ").Append(emptyRootValues ? "[]" : "[\n        {\n            \"id\": \"L1\"\n        }\n    ]").Append(",\n");
        sb.Append("    \"basePlate\": ");
        AppendElement(sb, 8, "    ");
        sb.Append(",\n    \"undoHistory\": [");
        for (int r = 0; r < recordCount; r++)
        {
            sb.Append(r == 0 ? "\n        " : ",\n        ");
            AppendRecord(sb, r, "        ", nested: true);
        }
        sb.Append("\n    ],\n    \"redoHistory\": [\n        ");
        AppendRecord(sb, 3, "        ", nested: true);
        sb.Append("\n    ],\n    \"settings\": {\n        \"autoSave\": true,\n        \"keyBindings\": ");
        sb.Append(emptyRootValues ? "[]" : "[\n            {\n                \"action\": \"Save\"\n            }\n        ]");
        sb.Append(",\n        \"gridMm\": 10\n    },\n    \"lightsOn\": true\n}");
        return sb.ToString();
    }

    private static void AppendRecord(StringBuilder sb, int r, string indent, bool nested)
    {
        string inner = indent + "    ";
        sb.Append("{\n").Append(inner).Append("\"type\": \"move\",\n");
        sb.Append(inner).Append("\"description\": \"Move E").Append(r).Append("\",\n");
        sb.Append(inner).Append("\"posBefore\": [\n").Append(inner).Append("    1.5,\n").Append(inner).Append("    0.25\n").Append(inner).Append("],\n");
        sb.Append(inner).Append("\"children\": [");
        if (nested && r % 10 == 3)
        {
            sb.Append('\n').Append(inner).Append("    ");
            AppendRecord(sb, r + 1, inner + "    ", nested: false);
            sb.Append('\n').Append(inner);
        }
        sb.Append("],\n");
        sb.Append(inner).Append("\"convertState\": [");
        if (r % 7 == 2)
        {
            for (int k = 0; k < 2; k++)
            {
                sb.Append(k == 0 ? "\n" : ",\n").Append(inner).Append("    ");
                AppendElement(sb, r + k, inner + "    ");
            }
            sb.Append('\n').Append(inner);
        }
        sb.Append("]\n").Append(indent).Append('}');
    }

    private static void AppendElement(StringBuilder sb, int index, string indent)
    {
        string inner = indent + "    ";
        int family = index % 10;
        sb.Append("{\n").Append(inner).Append("\"name\": \"E").Append(index).Append("\",\n");
        sb.Append(inner).Append("\"levelId\": ").Append(index % 2 == 0 ? "\"\"" : "\"L1\"").Append(",\n");
        for (int f = 0; f < 190; f++)
            sb.Append(inner).Append("\"field").Append(f).Append("\": ").Append(f % 3 == 0 ? "[\n" + inner + "    1,\n" + inner + "    2\n" + inner + "]" : "12.5").Append(",\n");
        for (int k = 0; k < FamilyFlags.Length; k++)
            sb.Append(inner).Append('"').Append(FamilyFlags[k]).Append("\": ").Append(family == k ? "true" : "false").Append(",\n");
        for (int k = 0; k < FamilyDetails.Length; k++)
            sb.Append(inner).Append('"').Append(FamilyDetails[k]).Append("\": ").Append(k % 4 == 0 ? "\"\"" : "0").Append(",\n");
        sb.Append(inner).Append("\"movable\": true\n").Append(indent).Append('}');
    }
}
