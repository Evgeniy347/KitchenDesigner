using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>The `ValidationElement` overload of `CandidatePairsInNestedLoopOrder` delegates to
/// a raw `(min, max)`-per-index overload, added so `EdgeBanding`'s sphere broad-phase can share
/// the SAME grid instead of scanning the whole scene per element. This file exercises the raw
/// overload directly, against a brute-force AABB-overlap-with-slack reference written here —
/// the grid must never MISS a pair the brute force finds (a miss silently drops edge coverage
/// or a validation contact); extra candidates are harmless, the caller's own exact check filters
/// them.</summary>
public class ValidationBroadPhaseBoundsOverloadTests
{
    [TearDown]
    public void TearDown() => ValidationBroadPhase.Clear();

    private static bool BruteForceOverlap(Vector3 minA, Vector3 maxA, Vector3 minB, Vector3 maxB,
        float slack) =>
        minA.x <= maxB.x + slack && maxA.x >= minB.x - slack
        && minA.y <= maxB.y + slack && maxA.y >= minB.y - slack
        && minA.z <= maxB.z + slack && maxA.z >= minB.z - slack;

    private static HashSet<(int lo, int hi)> BruteForcePairs(
        IReadOnlyList<(Vector3 min, Vector3 max)> boxes, float slack)
    {
        var pairs = new HashSet<(int, int)>();
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
                if (BruteForceOverlap(boxes[i].min, boxes[i].max, boxes[j].min, boxes[j].max, slack))
                    pairs.Add((i, j));
        return pairs;
    }

    [Test]
    public void CandidatePairs_OnRawBounds_MatchTheBruteForceOverlapTest()
    {
        const float slack = 0.0005f;
        var boxes = new (Vector3 min, Vector3 max)[]
        {
            (new Vector3(0, 0, 0), new Vector3(0.8f, 0.02f, 0.4f)),
            (new Vector3(0.8f, 0, 0), new Vector3(1.6f, 0.02f, 0.4f)),
            (new Vector3(3f, 0, 0), new Vector3(3.4f, 0.02f, 0.4f)),
            (new Vector3(0, 0.5f, 0), new Vector3(0.4f, 0.9f, 0.4f)),
            (new Vector3(0.3999f, 0.5f, 0), new Vector3(0.8f, 0.9f, 0.4f)),
            (new Vector3(50f, 50f, 50f), new Vector3(50.2f, 50.2f, 50.2f)),
        };

        var expected = BruteForcePairs(boxes, slack);

        ValidationBroadPhase.Clear();
        var found = new List<(int lo, int hi)>(ValidationBroadPhase.CandidatePairsInNestedLoopOrder(
            boxes.Length, i => boxes[i], slack));
        ValidationBroadPhase.Clear();

        var foundSet = new HashSet<(int, int)>(found);
        foreach (var pair in expected)
            Assert.IsTrue(foundSet.Contains(pair),
                $"пара {pair} перекрывается (с учётом slack={slack}), но сетка её не вернула — "
                + "это пропавший контакт или пропавшее покрытие кромки, а не лишняя работа");
    }

    [Test]
    public void CandidatePairs_OnRawBounds_ComeOutSortedByLoThenHi()
    {
        var boxes = new (Vector3 min, Vector3 max)[]
        {
            (new Vector3(2, 0, 0), new Vector3(2.5f, 1, 1)),
            (new Vector3(0, 0, 0), new Vector3(0.5f, 1, 1)),
            (new Vector3(1, 0, 0), new Vector3(1.5f, 1, 1)),
        };

        ValidationBroadPhase.Clear();
        var pairs = new List<(int lo, int hi)>(ValidationBroadPhase.CandidatePairsInNestedLoopOrder(
            boxes.Length, i => boxes[i], 5f));
        ValidationBroadPhase.Clear();

        Assert.Greater(pairs.Count, 1, "сцена собрана неправильно: нечего сортировать");
        for (int k = 1; k < pairs.Count; k++)
            Assert.IsTrue(pairs[k - 1].lo < pairs[k].lo
                          || (pairs[k - 1].lo == pairs[k].lo && pairs[k - 1].hi < pairs[k].hi),
                "порядок пар обязан быть детерминированным для вызывающего кода, который "
                + "строит из них список соседей один раз на снимок сцены");
    }
}
