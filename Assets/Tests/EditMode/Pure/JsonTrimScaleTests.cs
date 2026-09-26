using System.Diagnostics;
using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>Each *JsonTrim class walked the WHOLE elements array once PER call to
/// JsonText.RemoveMember, and RemoveMember rebuilds the FULL document with
/// string.Substring + string.Substring on every call — a new copy of the whole project
/// per removed key, on every element. That is the exact shape `RawElementRecords.Apply`
/// had before `a3ca35c0` (O(elements) calls each costing O(document length)), just spread
/// across five sibling classes instead of one. Measured before the fix: chaining
/// Fence+FloorSlab+Foundation+WallLayer+Roof at 800 synthetic elements took ~700 ms; at 1600
/// it was ~2.3 s. `JsonText.RewriteArrayItems` now
/// walks the array ONCE with a StringBuilder and hands each trim only its own element's
/// substring, so `RemoveMember` there pays for one element, not the document.
///
/// The threshold below is not tuned to the fix's actual speed (a few ms) — it is set an
/// order of magnitude above it and comfortably below where the old O(n^2) code lands, so a
/// slow CI box cannot make this test flaky in either direction: the old code misses it by
/// ~4x even on a machine several times slower than the one it was measured on, and the new
/// code clears it by ~10x with plenty of room to spare.</summary>
public class JsonTrimScaleTests
{
    private const int ElementCount = 800;
    private const int BudgetMs = 300;

    [Test]
    public void ChainedTrims_OnManyElements_StayLinearAndKeepEveryElement()
    {
        string project = BuildProjectWithPlainElements(ElementCount);

        var sw = Stopwatch.StartNew();
        string result = RoofJsonTrim.RemoveWhenNotRoof(
            WallLayerJsonTrim.RemoveWhenNotWallLayer(
                FoundationJsonTrim.RemoveWhenNotFoundation(
                    FloorSlabJsonTrim.RemoveWhenNotFloorSlab(
                        FenceJsonTrim.RemoveWhenNotFence(project)))));
        sw.Stop();

        var array = JsonText.MemberValue(result, JsonText.RootObject(result), "elements");
        var items = JsonText.ArrayItems(result, array);
        Assert.AreEqual(ElementCount, items.Count,
            "trim must not drop or merge elements while rewriting the array");

        var first = items[0].Text(result);
        var last = items[items.Count - 1].Text(result);
        Assert.AreEqual("\"E0\"", MemberOf(first, "name"));
        Assert.AreEqual($"\"E{ElementCount - 1}\"", MemberOf(last, "name"));
        Assert.IsFalse(first.Contains("isFence"),
            "a plain element's family flags are still removed");
        Assert.IsFalse(first.Contains("wallLayerHostWallName"));
        Assert.IsFalse(first.Contains("isRoof"));

        Assert.Less(sw.Elapsed.TotalMilliseconds, BudgetMs,
            $"{ElementCount} elements through five chained trims took {sw.Elapsed.TotalMilliseconds:F0} ms; "
            + "this class of defect made it grow with the SQUARE of the element count "
            + "(RawElementRecords.Apply before a3ca35c0), not with the count itself");
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
            sb.Append("      \"movable\": true,\n");
            sb.Append("      \"position\": [0.0, 0.0, 0.0]\n");
            sb.Append("    }");
        }
        sb.Append("\n  ]\n}");
        return sb.ToString();
    }
}
