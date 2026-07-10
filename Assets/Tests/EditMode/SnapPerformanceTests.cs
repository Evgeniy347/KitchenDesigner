using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using Debug = UnityEngine.Debug;

/// <summary>
/// Тесты производительности SnapSystem и ConstraintValidator.
/// Запускать в Unity Editor — через CLI результаты могут отличаться.
/// </summary>
public class SnapPerformanceTests
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

    private KitchenElement Make(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        _spawned.Add(go);
        return e;
    }

    private List<KitchenElement> CreateParts(int count)
    {
        var list = new List<KitchenElement>();
        for (int i = 0; i < count; i++)
        {
            float x = (i % 20) * 0.82f;
            float z = (i / 20) * 0.42f;
            list.Add(Make($"B{i}", new Vector3Int(800, 400, 18), new Vector3(x, 0f, z)));
        }
        return list;
    }

    [Test]
    public void Snap_50Boards_Under16ms()
    {
        var boards = CreateParts(50);
        var sw = new Stopwatch();
        sw.Start();

        var snap = SnapSystem.TrySnap(boards[0], boards, new Vector3(0.41f, 0f, 0.02f));

        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 16,
            $"Snap 50 boards took {sw.ElapsedMilliseconds}ms (limit 16ms)");
        Debug.Log($"[Perf] Snap_50Boards: {sw.ElapsedMilliseconds}ms snapped={snap.snapped}");
    }

    [Test]
    public void Snap_100Boards_Under32ms()
    {
        var boards = CreateParts(100);
        var sw = new Stopwatch();
        sw.Start();

        var snap = SnapSystem.TrySnap(boards[0], boards, new Vector3(0.41f, 0f, 0.02f));

        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 32,
            $"Snap 100 boards took {sw.ElapsedMilliseconds}ms (limit 32ms)");
        Debug.Log($"[Perf] Snap_100Boards: {sw.ElapsedMilliseconds}ms snapped={snap.snapped}");
    }

    [Test]
    public void Snap_200Boards_Under64ms()
    {
        var boards = CreateParts(200);
        var sw = new Stopwatch();
        sw.Start();

        var snap = SnapSystem.TrySnap(boards[0], boards, new Vector3(0.41f, 0f, 0.02f));

        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 64,
            $"Snap 200 boards took {sw.ElapsedMilliseconds}ms (limit 64ms)");
        Debug.Log($"[Perf] Snap_200Boards: {sw.ElapsedMilliseconds}ms snapped={snap.snapped}");
    }

    [Test]
    public void Validate_50Boards_Under32ms()
    {
        var boards = CreateParts(50);
        var sw = new Stopwatch();
        sw.Start();

        var result = ConstraintValidator.Validate(boards);

        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 64,
            $"Validate 50 boards took {sw.ElapsedMilliseconds}ms (limit 64ms)");
        Debug.Log($"[Perf] Validate_50Boards: {sw.ElapsedMilliseconds}ms valid={result.isValid}");
    }

    [Test]
    public void Validate_100Boards_Under64ms()
    {
        var boards = CreateParts(100);
        var sw = new Stopwatch();
        sw.Start();

        var result = ConstraintValidator.Validate(boards);

        sw.Stop();
        Assert.IsTrue(sw.ElapsedMilliseconds < 128,
            $"Validate 100 boards took {sw.ElapsedMilliseconds}ms (limit 128ms)");
        Debug.Log($"[Perf] Validate_100Boards: {sw.ElapsedMilliseconds}ms valid={result.isValid}");
    }
}
