using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Кодек строковых спецификаций MCP: пазы, накладки текстур и список
/// открытых торцов. Здесь живут причины, которые раньше были комментариями
/// над этими методами в McpCommandHandler.</summary>
public class McpSpecCodecTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private static readonly Vector3Int ShelfDims = new Vector3Int(800, 18, 400);

    private KitchenElement CreatePart(string name, Vector3Int dims, Vector3 pos = default)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        var element = go.AddComponent<KitchenElement>();
        element.PartName = name;
        element.DimensionsMM = dims;
        go.transform.position = pos;
        return element;
    }

    [SetUp]
    public void SetUp() => PartRegistry.Clear();

    [TearDown]
    public void TearDown()
    {
        PartRegistry.Clear();
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
    }

    [Test]
    public void FormatBandedEdges_LoneShelf_ListsEveryOpenEnd()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var all = new List<KitchenElement> { shelf };

        string edges = McpSpecCodec.FormatBandedEdgesRecomputedFromScene(shelf, all);

        Assert.AreEqual("L1,L2,W1,W2", edges,
            "одинокая полка ничем не закрыта — кромка нужна на всех четырёх торцах");
    }

    [Test]
    public void FormatBandedEdges_NeighbourMovedAgainstEnd_DropsThatEndWithoutTouchingTheShelf()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        var neighbour = CreatePart("Side", new Vector3Int(18, 720, 400),
            new Vector3(409f * AppConstants.MM_TO_UNITS, 0f, 0f));
        var all = new List<KitchenElement> { shelf, neighbour };

        string withNeighbour = McpSpecCodec.FormatBandedEdgesRecomputedFromScene(shelf, all);
        string alone = McpSpecCodec.FormatBandedEdgesRecomputedFromScene(shelf, new List<KitchenElement> { shelf });

        Assert.AreNotEqual(alone, withNeighbour,
            "наличие кромки не хранится в детали, а вычисляется по текущей сцене: " +
            "стойка, придвинутая к торцу, обязана убрать его из списка");
        StringAssert.DoesNotContain("W1", withNeighbour,
            "торец, упирающийся в соседа, кромкой не закрывается");
        StringAssert.Contains("W2", withNeighbour,
            "остальные торцы соседом не затронуты");
    }

    [Test]
    public void FormatBandedEdges_BandingOff_IsEmptyEvenWithOpenEnds()
    {
        var shelf = CreatePart("Shelf", ShelfDims);
        shelf.EdgeBandingEnabled = false;

        Assert.AreEqual(string.Empty,
            McpSpecCodec.FormatBandedEdgesRecomputedFromScene(shelf, new List<KitchenElement> { shelf }),
            "кромкование выключено — контракт отдаёт пустую строку, а не список торцов");
    }

    [Test]
    public void TextureOverlays_SemicolonSeparatesItems_BecauseCommaIsTakenByTheArea()
    {
        Assert.IsTrue(McpSpecCodec.TryParseTextureOverlays(
            "a:oak; b:white@100,200+800x600", out var parsed, out string error), error);

        Assert.AreEqual(2, parsed.Count,
            "элементы разделяет ';' — запятая внутри области уже занята парой u,v");
        Assert.AreEqual(100, parsed[1].u0MM);
        Assert.AreEqual(200, parsed[1].v0MM);
    }
}
