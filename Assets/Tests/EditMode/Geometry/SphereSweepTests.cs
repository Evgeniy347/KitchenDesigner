using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>`SphereSweep` replaces a per-element O(elements) scan (`EdgeBanding.Coverage`'s
/// former broad phase) with a single O(n log n + k) sort-and-sweep along X. It reports a
/// SUPERSET of the true touching pairs on purpose — the caller's own exact 3D check filters the
/// rest — so the one property that must never break is completeness: every pair whose spheres
/// truly touch (within `slack`) MUST come out of the sweep. This file checks that against a
/// brute-force O(n^2) reference, not against the exact geometric threshold: the sweep expands
/// EACH sphere's own radius by `slack` (not half each), so its cut-off is intentionally a
/// little wider than the true touch distance — that slack must always be able to say more, not
/// less.</summary>
public class SphereSweepTests
{
    private static bool BruteForceTouches(Vector3 ca, float ra, Vector3 cb, float rb, float slack) =>
        (ca - cb).sqrMagnitude <= (ra + rb + slack) * (ra + rb + slack);

    private static HashSet<(int, int)> BruteForcePairs(
        IReadOnlyList<(Vector3 center, float radius)> spheres, float slack)
    {
        var pairs = new HashSet<(int, int)>();
        for (int i = 0; i < spheres.Count; i++)
            for (int j = i + 1; j < spheres.Count; j++)
                if (BruteForceTouches(spheres[i].center, spheres[i].radius,
                        spheres[j].center, spheres[j].radius, slack))
                    pairs.Add((i, j));
        return pairs;
    }

    [Test]
    public void AddOverlappingPairs_NeverMissesAPairTheBruteForceCheckFinds()
    {
        const float slack = 0.0005f;
        var spheres = new (Vector3 center, float radius)[]
        {
            (new Vector3(0, 0, 0), 0.4f),
            (new Vector3(0.79f, 0, 0), 0.4f),
            (new Vector3(3f, 0, 0), 0.2f),
            (new Vector3(0, 1.5f, 0), 0.3f),
            (new Vector3(0.5999f, 1.5f, 0), 0.3f),
            (new Vector3(0, 0, 3f), 0.3f),
            (new Vector3(0, 0.05f, 3f), 0.3f),
            (new Vector3(50f, 50f, 50f), 0.1f),
        };

        var expected = BruteForcePairs(spheres, slack);

        var found = new HashSet<(int, int)>();
        SphereSweep.AddOverlappingPairs(spheres.Length, i => spheres[i], slack,
            (a, b) => found.Add(a < b ? (a, b) : (b, a)));

        foreach (var pair in expected)
            Assert.IsTrue(found.Contains(pair),
                $"пара {pair} касается (с учётом slack={slack}), но развёртка её не вернула — "
                + "это пропавшее покрытие кромки или пропавший контакт, а не лишняя работа");
    }

    [Test]
    public void AddOverlappingPairs_OnEmptyOrSingleElementScene_ReportsNothing()
    {
        int calls = 0;
        SphereSweep.AddOverlappingPairs(0, i => (Vector3.zero, 1f), 0f, (a, b) => calls++);
        SphereSweep.AddOverlappingPairs(1, i => (Vector3.zero, 1f), 0f, (a, b) => calls++);

        Assert.AreEqual(0, calls, "меньше двух сфер — пар не бывает по определению");
    }

    [Test]
    public void AddOverlappingPairs_OnManyFarApartSpheres_ReportsNoPairs()
    {
        const int count = 500;
        var spheres = new (Vector3 center, float radius)[count];
        for (int i = 0; i < count; i++)
            spheres[i] = (new Vector3(i * 10f, 0, 0), 0.4f);

        int calls = 0;
        SphereSweep.AddOverlappingPairs(count, i => spheres[i], 0.001f, (a, b) => calls++);

        Assert.AreEqual(0, calls, "10 м между центрами радиуса 0,4 м — ни одна пара не касается");
    }
}
