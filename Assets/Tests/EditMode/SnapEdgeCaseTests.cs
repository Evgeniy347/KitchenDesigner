using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>Граничные условия SnapSystem.</summary>
public class SnapEdgeCaseTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _prevVerbose;

    [SetUp]
    public void Setup()
    {
        KitchenSettings.Instance.GridStep = 1;
        KitchenSettings.Instance.GridEnabled = true;
        KitchenSettings.Instance.SnapEnabled = true;
        KitchenSettings.Instance.SnapThreshold = 50f;
        _prevVerbose = SnapSystem.VerboseLog;
        SnapSystem.VerboseLog = false;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        SnapSystem.VerboseLog = _prevVerbose;
    }

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = rot ?? Quaternion.identity;
        var e = go.AddComponent<KitchenElement>();
        e.BoardName = name;
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    [Test]
    public void SnapDisabled_InSettings_ReturnsNotSnapped()
    {
        KitchenSettings.Instance.SnapEnabled = false;
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.82f, 0f, 0f));
        Assert.IsFalse(r.snapped);
    }

    [Test]
    public void ThresholdZero_NoSnap()
    {
        KitchenSettings.Instance.SnapThreshold = 0f;
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.80001f, 0f, 0f));
        Assert.IsFalse(r.snapped, "при пороге 0 даже касание не снэпается");
    }

    [Test]
    public void ThresholdVeryLarge_SnapsEverything()
    {
        KitchenSettings.Instance.SnapThreshold = 1000f;
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(1.0f, 0f, 0f));
        Assert.IsTrue(r.snapped, "при большом пороге далёкие доски снэпаются");
    }

    [Test]
    public void MultipleTargets_ChoosesClosest()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(0.8f, 0f, 0f));
        var c = Make("C", new Vector3Int(400, 400, 18), Vector3.zero);

        var r = SnapSystem.TrySnap(c, new List<KitchenElement> { a, b },
            new Vector3(0.82f, 0f, 0.02f));
        Assert.IsTrue(r.snapped);
        Assert.AreEqual("B", r.targetName, "C ближе к B (0.02м), чем к A (0.82м)");
    }

    [Test]
    public void MovingAwayFromTarget_NoSnap()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(2.0f, 0f, 0f));
        Assert.IsFalse(r.snapped, "далеко от цели — нет снэпа");
    }

    [Test]
    public void ParallelFaces_NoOverlap_NoSnap()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        // Сдвиг по Y так, что грани не перекрываются (b выше)
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0f, 0.7f, 0.02f));
        Assert.IsFalse(r.snapped, "грани параллельны, но не перекрываются");
    }

    [Test]
    public void PerpendicularFaces_NoSnap()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero,
            Quaternion.AngleAxis(90f, Vector3.up));
        // Нормали перпендикулярны — нет параллельных граней
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.82f, 0f, 0f));
        Assert.IsFalse(r.snapped, "перпендикулярные нормали — нет снэпа");
    }

    [Test]
    public void OppositeNormals_NoSnap()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(800, 400, 18), Vector3.zero);
        // Обе грани смотрят в одну сторону (нормали параллельны, dot ≈ +1):
        // A передняя грань (0,0,1), B передняя грань (0,0,1).
        // Такие грани не могут контактировать — нет снэпа.
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0f, 0f, 0.05f));
        Assert.IsFalse(r.snapped, "параллельные нормали одного направления — нет снэпа");
    }

    [Test]
    public void FloatingPointPrecision_1e9_NoFalsePositive()
    {
        var a = Make("A", new Vector3Int(800, 400, 18), new Vector3(1e9f, 0f, 0f));
        var b = Make("B", new Vector3Int(800, 400, 18), new Vector3(1e9f + 0.82f, 0f, 0f));
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(1e9f + 0.82f, 0f, 0f));
        // На больших координатах не должно быть ложных срабатываний
        Assert.IsFalse(r.snapped || float.IsNaN(r.position.x),
            "на больших координатах нет ложных срабатываний");
    }

    [Test]
    public void VeryLargeBoard_SnapsCorrectly()
    {
        var a = Make("A", new Vector3Int(5000, 5000, 18), Vector3.zero);
        var b = Make("B", new Vector3Int(5000, 5000, 18), Vector3.zero);
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(5.02f, 0f, 0f));
        Assert.IsTrue(r.snapped, "большая доска 5м должна снэпаться");
        Assert.AreEqual(5.0f, r.position.x, 0.001f);
    }

    [Test]
    public void VerySmallBoard_SnapsCorrectly()
    {
        var a = Make("A", new Vector3Int(1, 1, 1), Vector3.zero);
        var b = Make("B", new Vector3Int(1, 1, 1), Vector3.zero);
        var r = SnapSystem.TrySnap(b, new List<KitchenElement> { a },
            new Vector3(0.003f, 0f, 0f));
        Assert.IsTrue(r.snapped, "минимальная доска 1мм должна снэпаться");
        Assert.AreEqual(0.001f, r.position.x, 0.001f);
    }
}
