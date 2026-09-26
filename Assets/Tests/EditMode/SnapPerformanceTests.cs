using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>2026-09-26: заменил пять сенсоров в МИЛЛИСЕКУНДАХ (`Assert.IsTrue(sw.ElapsedMilliseconds
/// &lt; N)`, N масштабировался вручную под каждый размер сцены) на счётчики ОПЕРАЦИЙ — по тому же
/// требованию и тем же приёмом, что и `EdgeCoverageBroadPhaseEquivalenceTests`
/// (conventions/PERFORMANCE.md: «сенсор масштаба считает операции, а не миллисекунды»). Класс сам
/// признавался в шаткости: "Запускать в Unity Editor — через CLI результаты могут отличаться" — и
/// действительно, JsonTrimScaleTests (тот же класс дефекта) уже красился под соседской нагрузкой
/// на этой самой машине в этой же сессии.
///
/// TrySnap уже даёт готовый прибор — `SnapCandidateCollector.TakeNeighboursSeen()`, тот же
/// [ThreadStatic]-счётчик, которым тест был бы обязан считать сам, если бы его не было. Validate
/// точно так же берёт `ValidationCore.TakePairsProcessed()` — идиому, которую этот проект уже
/// использует в `IncrementalValidationTests` для ровно той же формы (растёт ли работа с O(n) или
/// с O(n²)).</summary>
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
    public void Snap_NeighboursSeen_GrowsLinearly_NotQuadratically_AsBoardsQuadruple()
    {
        int small = NeighboursSeenFor(50);
        int large = NeighboursSeenFor(200);

        Assert.Greater(small, 0, "сцена из 50 досок обязана дать хоть одного соседа на просмотр");
        Assert.Less(large, small * 8,
            $"сцена выросла в 4 раза (50 досок -> 200), число просмотренных соседей "
            + $"выросло с {small} до {large}. TrySnap обязан оставаться O(n) на список "
            + "кандидатов; рост около 16х означает, что где-то появился вложенный проход "
            + "по сцене на каждого соседа");
    }

    private int NeighboursSeenFor(int count)
    {
        var boards = CreateParts(count);
        SnapCandidateCollector.TakeNeighboursSeen();
        SnapSystem.TrySnap(boards[0], boards, new Vector3(0.41f, 0f, 0.02f));
        int seen = SnapCandidateCollector.TakeNeighboursSeen();

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        return seen;
    }

    [Test]
    public void Validate_PairsProcessed_GrowsLinearly_NotQuadratically_AsBoardsQuadruple()
    {
        int small = PairsProcessedFor(50);
        int large = PairsProcessedFor(200);

        Assert.Greater(small, 0, "сцена из 50 досок обязана дать хоть одну пару для проверки");
        Assert.Less(large, small * 8,
            $"сцена выросла в 4 раза (50 досок -> 200), число обработанных пар "
            + $"выросло с {small} до {large}. Широкая фаза обязана оставаться O(n log n) "
            + "или лучше; рост около 16х означает, что валидация деградировала до "
            + "полного перебора всех пар");
    }

    private int PairsProcessedFor(int count)
    {
        var boards = CreateParts(count);
        ValidationCore.TakePairsProcessed();
        ConstraintValidator.Validate(boards);
        int pairs = ValidationCore.TakePairsProcessed();

        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        return pairs;
    }
}
