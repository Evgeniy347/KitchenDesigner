using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Each *JsonTrim class walked the WHOLE elements array once PER call to
/// JsonText.RemoveMember, and RemoveMember rebuilds the FULL document with
/// string.Substring + string.Substring on every call — a new copy of the whole project
/// per removed key, on every element. That is the exact shape `RawElementRecords.Apply`
/// had before `a3ca35c0` (O(elements) calls each costing O(document length)), just spread
/// across five sibling classes instead of one. `JsonText.RewriteArrayItems` now walks the
/// array ONCE with a StringBuilder and hands each trim only its own element's substring, so
/// `RemoveMember` there pays for one element, not the document.
///
/// 2026-09-26: replaced the millisecond budget with a character-count one, following
/// `EdgeCoverageBroadPhaseEquivalenceTests` (conventions/PERFORMANCE.md: "a scale test
/// counts operations, not milliseconds") — this test class's own predecessor here went red
/// under shared-machine load (361ms vs a 300ms budget) on unchanged code the very same
/// session this fix was written in. `JsonText.TakeCharsProcessedByRemoveMember()` counts the
/// total length of every string `RemoveMember` was asked to rewrite: the O(n^2) shape this
/// test exists to catch called it on the FULL document (length L) once per element (N),
/// total N*L; the fix calls it on each element's own slice, total ~L regardless of N. That
/// is a machine-independent stand-in for exactly the growth this test guards, not a proxy
/// for it.
///
/// 2026-09-26: `LevelsJsonTrim` and `DuctJsonTrim` joined the chain (test-results/review-persistence.md
/// #1) — the review found they were the two classes NOT covered here, and `LevelsJsonTrim` in
/// particular still walked the array with `RemoveMember(source, item, ...)` directly instead of
/// through `RewriteArrayItems`, so this exact guard could not see its O(n^2) shape.
///
/// 2026-09-27: family trims no longer call RemoveMember per key; they mark keys on one
/// `JsonObjectEdit` read of the element and write it once. The sensor is therefore the sum of
/// both counters: RemoveMember (still used for root members) plus `JsonObjectEdit.TakeCharsRead()`.</summary>
public class JsonTrimScaleTests
{
    private static long TakeCharsProcessed() =>
        JsonText.TakeCharsProcessedByRemoveMember() + JsonObjectEdit.TakeCharsRead();

    private static string RunChainedTrims(int elementCount, out string result)
    {
        string project = BuildProjectWithPlainElements(elementCount);
        TakeCharsProcessed();
        result = LevelsJsonTrim.RemoveWhenEmpty(
            DuctJsonTrim.RemoveWhenNotDuct(
                RoofJsonTrim.RemoveWhenNotRoof(
                    WallLayerJsonTrim.RemoveWhenNotWallLayer(
                        FoundationJsonTrim.RemoveWhenNotFoundation(
                            FloorSlabJsonTrim.RemoveWhenNotFloorSlab(
                                FenceJsonTrim.RemoveWhenNotFence(project)))))));
        return project;
    }

    [Test]
    public void ChainedTrims_OnManyElements_StayLinearAndKeepEveryElement()
    {
        const int elementCount = 800;
        RunChainedTrims(elementCount, out string result);

        var array = JsonText.MemberValue(result, JsonText.RootObject(result), "elements");
        var items = JsonText.ArrayItems(result, array);
        Assert.AreEqual(elementCount, items.Count,
            "trim must not drop or merge elements while rewriting the array");

        var first = items[0].Text(result);
        var last = items[items.Count - 1].Text(result);
        Assert.AreEqual("\"E0\"", MemberOf(first, "name"));
        Assert.AreEqual($"\"E{elementCount - 1}\"", MemberOf(last, "name"));
        Assert.IsFalse(first.Contains("isFence"),
            "a plain element's family flags are still removed");
        Assert.IsFalse(first.Contains("wallLayerHostWallName"));
        Assert.IsFalse(first.Contains("isRoof"));
        Assert.IsFalse(first.Contains("levelId"),
            "LevelsJsonTrim must also run through RewriteArrayItems, not RemoveMember on the full document");
        Assert.IsFalse(first.Contains("isDuct"));
        Assert.IsFalse(first.Contains("ductDiameterMm"));
        Assert.IsFalse(first.Contains("isGrille"));
        Assert.IsFalse(first.Contains("grilleAirflowM3PerHour"));
    }

    [Test]
    public void ChainedTrims_CharsProcessed_GrowsLinearly_NotQuadratically_AsElementsQuadruple()
    {
        RunChainedTrims(200, out _);
        long small = TakeCharsProcessed();

        RunChainedTrims(800, out _);
        long large = TakeCharsProcessed();

        Assert.Greater(small, 0, "200 elements through seven chained trims must read their element slices");
        Assert.Less(large, small * 8,
            $"element count quadrupled (200 -> 800), but RemoveMember's total characters "
            + $"processed grew from {small} to {large}. RewriteArrayItems hands each trim "
            + "only its own element's slice (O(n) total); growth near 16x means RemoveMember "
            + "is seeing the WHOLE document again on every element, the exact O(n^2) shape "
            + "this class exists to catch (this is exactly the shape LevelsJsonTrim had before "
            + "test-results/review-persistence.md #1: it called RemoveMember(source, item, ...) "
            + "with `source` the FULL document, once per element)");
    }

    private static string MemberOf(string json, string key)
    {
        var value = JsonText.MemberValue(json, JsonText.RootObject(json), key);
        return value.Found ? value.Text(json) : "";
    }

    private static string BuildProjectWithPlainElements(int count)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"elements\": [\n");
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(",\n");
            sb.Append("    {\n");
            sb.Append("      \"name\": \"E").Append(i).Append("\",\n");
            sb.Append("      \"levelId\": \"\",\n");
            sb.Append("      \"isFence\": false,\n");
            sb.Append("      \"fencePostSectionMm\": 60,\n");
            sb.Append("      \"isFloorSlab\": false,\n");
            sb.Append("      \"isFoundation\": false,\n");
            sb.Append("      \"isInsulation\": false,\n");
            sb.Append("      \"isVentGap\": false,\n");
            sb.Append("      \"isCladding\": false,\n");
            sb.Append("      \"wallLayerHostWallName\": \"\",\n");
            sb.Append("      \"isRoof\": false,\n");
            sb.Append("      \"roofType\": 1,\n");
            sb.Append("      \"isDuct\": false,\n");
            sb.Append("      \"ductDiameterMm\": 100,\n");
            sb.Append("      \"isGrille\": false,\n");
            sb.Append("      \"grilleAirflowM3PerHour\": 0,\n");
            sb.Append("      \"movable\": true,\n");
            sb.Append("      \"position\": [0.0, 0.0, 0.0]\n");
            sb.Append("    }");
        }
        sb.Append("\n  ]\n}");
        return sb.ToString();
    }
}
