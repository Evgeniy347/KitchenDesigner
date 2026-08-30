using System.Collections.Generic;
using NUnit.Framework;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Разбор строки пазов для MCP: "through:top, blind:left".
/// Ошибки должны быть описательными — инструмент вызывается вслепую.</summary>
public class GrooveMcpTests
{
    private static bool Parse(string spec, out List<GrooveSpec> result, out string error)
        => McpSpecCodec.TryParseGrooves(spec, out result, out error);

    [Test]
    public void EmptyString_ClearsAllGrooves()
    {
        Assert.IsTrue(Parse("", out var r, out _));
        Assert.AreEqual(0, r.Count);
    }

    [Test]
    public void ParsesKindAndSide()
    {
        Assert.IsTrue(Parse("through:top, blind:left", out var r, out var err), err);
        Assert.AreEqual(2, r.Count);
        Assert.AreEqual(new GrooveSpec(GrooveKind.Through, GrooveSide.Top), r[0]);
        Assert.AreEqual(new GrooveSpec(GrooveKind.Blind, GrooveSide.Left), r[1]);
    }

    [Test]
    public void IsCaseAndSpaceInsensitive()
    {
        Assert.IsTrue(Parse("  THROUGH : Bottom ", out var r, out var err), err);
        Assert.AreEqual(new GrooveSpec(GrooveKind.Through, GrooveSide.Bottom), r[0]);
    }

    [Test]
    public void RejectsUnknownKind_WithHelpfulMessage()
    {
        Assert.IsFalse(Parse("deep:top", out _, out var err));
        StringAssert.Contains("deep", err);
        StringAssert.Contains("through|blind", err);
    }

    [Test]
    public void RejectsUnknownSide_WithHelpfulMessage()
    {
        Assert.IsFalse(Parse("through:middle", out _, out var err));
        StringAssert.Contains("middle", err);
        StringAssert.Contains("top|bottom|left|right", err);
    }

    [Test]
    public void RejectsMalformedPair()
    {
        Assert.IsFalse(Parse("through-top", out _, out var err));
        StringAssert.Contains("kind:side", err);
    }

    [Test]
    public void RejectsDuplicate_BecauseOffsetIsFixed()
    {
        Assert.IsFalse(Parse("through:top, through:top", out _, out var err));
        StringAssert.Contains("duplicate", err);
    }

    [Test]
    public void FormatGrooves_RoundTripsThroughParser()
    {
        var go = new UnityEngine.GameObject("board");
        try
        {
            var el = go.AddComponent<KitchenElement>();
            el.DimensionsMM = new UnityEngine.Vector3Int(600, 400, 18);
            el.AddGroove(new GrooveSpec(GrooveKind.Through, GrooveSide.Top));
            el.AddGroove(new GrooveSpec(GrooveKind.Blind, GrooveSide.Right));

            string formatted = McpSpecCodec.FormatGrooves(el);
            Assert.AreEqual("through:top, blind:right", formatted);

            Assert.IsTrue(Parse(formatted, out var reparsed, out var err), err);
            CollectionAssert.AreEqual(el.Grooves, reparsed);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
