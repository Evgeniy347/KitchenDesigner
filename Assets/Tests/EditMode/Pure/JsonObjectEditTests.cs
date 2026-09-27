using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using KitchenDesigner.Core;

/// <summary>`JsonObjectEdit` replaced a chain of `JsonText.RemoveMember` calls — one per
/// removed key, each re-parsing the element's members and rebuilding the whole string. That
/// chain cost 2.3 s and ~1 GB of garbage per save on the user's 412-element project
/// (test-results/perf/perf_20260927_095510.csv: one 4988 ms frame, 995 MB allocated). The
/// edit reads the object once, lets every rule mark keys, and writes the object once.
///
/// The saved text must stay byte-for-byte what the chain produced, so the reference here is
/// the chain itself: for every subset of members — first member, last member, a leading run,
/// everything — marking the subset and writing once equals removing the same keys one by one
/// with `RemoveMember`, in either order.</summary>
public class JsonObjectEditTests
{
    private static readonly string[] Keys = { "a", "b", "c", "d", "e" };

    [Test]
    public void WriteTo_EveryRemovalSubset_EqualsSequentialRemoveMember_InBothOrders()
    {
        string[] layouts =
        {
            BuildPretty(Keys, "    "),
            "{\"a\":1,\"b\":[1,2],\"c\":{\"x\":\"}\"},\"d\":\"\",\"e\":false}",
            "{ \"a\" : 1 , \"b\" : 2 , \"c\" : 3 , \"d\" : 4 , \"e\" : 5 }",
        };

        int checkedCases = 0;
        foreach (string layout in layouts)
        {
            string source = "[" + layout + "]";
            var span = new JsonSpan(1, 1 + layout.Length);
            for (int mask = 1; mask < 1 << Keys.Length; mask++)
            {
                var removed = new List<string>();
                for (int k = 0; k < Keys.Length; k++)
                    if ((mask & (1 << k)) != 0) removed.Add(Keys[k]);

                string viaEdit = JsonObjectEdit.Apply(source, span, edit =>
                {
                    foreach (var key in removed) edit.Remove(key);
                });

                Assert.AreEqual(Sequential(source, span, removed), viaEdit,
                    $"mask {mask} on {layout}: one write must equal RemoveMember key by key");
                var reversed = new List<string>(removed);
                reversed.Reverse();
                Assert.AreEqual(Sequential(source, span, reversed), viaEdit,
                    $"mask {mask} on {layout}: RemoveMember order must not matter");
                checkedCases++;
            }
        }
        Assert.AreEqual(3 * 31, checkedCases);
    }

    [Test]
    public void Apply_NothingMarked_ReturnsTheSameInstance()
    {
        string source = BuildPretty(Keys, "");
        string result = JsonObjectEdit.Apply(source, JsonText.RootObject(source), _ => { });
        Assert.IsTrue(ReferenceEquals(source, result),
            "an untouched element must not be copied — RewriteNamedObject relies on reference equality");
    }

    [Test]
    public void Remove_DuplicateKey_RemovesOnlyTheFirstOccurrence_LikeRemoveMember()
    {
        string source = "{\n    \"a\": 1,\n    \"b\": 2,\n    \"a\": 3\n}";
        var span = JsonText.RootObject(source);
        string viaEdit = JsonObjectEdit.Apply(source, span, edit => edit.Remove("a"));
        Assert.AreEqual(JsonText.RemoveMember(source, span, "a"), viaEdit);
        StringAssert.Contains("\"a\": 3", viaEdit, "the second occurrence survives, as with RemoveMember");
    }

    [Test]
    public void Remove_EscapedKey_MatchesItsUnescapedName_AndTheEarlierOccurrenceWins()
    {
        string escapedFirst = "{\"\\u0061\": 1, \"b\": 2, \"a\": 3}";
        string plainFirst = "{\"a\": 1, \"b\": 2, \"\\u0061\": 3}";
        foreach (string source in new[] { escapedFirst, plainFirst })
        {
            var span = JsonText.RootObject(source);
            Assert.AreEqual(JsonText.RemoveMember(source, span, "a"),
                JsonObjectEdit.Apply(source, span, edit => edit.Remove("a")),
                "RemoveMember unescapes key names and removes the FIRST match; the key index must agree: " + source);
        }
    }

    [Test]
    public void Remove_OnAnObjectOfManyKeys_FindsEveryOne()
    {
        var keys = new string[300];
        for (int i = 0; i < keys.Length; i++) keys[i] = "key" + i;
        string source = BuildPretty(keys, "");
        string result = JsonObjectEdit.Apply(source, JsonText.RootObject(source), edit =>
        {
            for (int i = 0; i < keys.Length; i += 2) edit.Remove(keys[i]);
        });
        for (int i = 0; i < keys.Length; i++)
            Assert.AreEqual(i % 2 == 1, result.Contains("\"" + keys[i] + "\""),
                $"{keys[i]}: the hash index must neither lose a key nor hit a neighbour");
    }

    [Test]
    public void ValueIs_ComparesTheRawValueText_OfTheFirstOccurrence()
    {
        var edit = new JsonObjectEdit();
        Assert.IsTrue(edit.Read("{\"f\": false, \"s\": \"\", \"f\": true}", 0));
        Assert.IsTrue(edit.ValueIs("f", "false"));
        Assert.IsTrue(edit.ValueIs("s", "\"\""));
        Assert.IsFalse(edit.ValueIs("s", "\"x\""));
        Assert.IsFalse(edit.ValueIs("missing", "false"), "an absent key is never equal to anything");
        Assert.IsTrue(edit.Has("s"));
        Assert.IsFalse(edit.Has("missing"));
    }

    private static string Sequential(string source, JsonSpan span, List<string> keys)
    {
        string text = span.Text(source);
        foreach (var key in keys)
            text = JsonText.RemoveMember(text, JsonText.RootObject(text), key);
        return source.Substring(0, span.Start) + text + source.Substring(span.End);
    }

    private static string BuildPretty(string[] keys, string indent)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i < keys.Length; i++)
        {
            sb.Append('\n').Append(indent).Append("    \"").Append(keys[i]).Append("\": ").Append(i);
            if (i < keys.Length - 1) sb.Append(',');
        }
        sb.Append('\n').Append(indent).Append('}');
        return sb.ToString();
    }
}
