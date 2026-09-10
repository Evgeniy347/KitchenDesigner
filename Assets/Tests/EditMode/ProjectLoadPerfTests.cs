using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using Debug = UnityEngine.Debug;

/// <summary>
/// Замер, а не регрессионный тест: печатает разбивку времени восстановления
/// сцены из замороженной копии проекта. Нужен, чтобы отвечать числами на
/// «сцена долго открывается», а не догадками.
///
/// Сцена берётся из ЗАМОРОЖЕННОЙ копии по той же причине, что и в
/// <see cref="ValidationInvariantTests"/>: docs/example.save.json живой, его
/// переписывает автосохранение десктопа, и два замера подряд мерили бы разные
/// сцены.
/// </summary>
[Explicit("замер, не тест: печатает разбивку времени загрузки проекта")]
public class ProjectLoadPerfTests
{
    private const string SaveFileName = "Fixtures/validation-scene.save.json";

    private string _json = "";
    private ProjectLoadStateGuard? _guard;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var fullPath = Path.Combine(Application.dataPath, "Tests/EditMode", SaveFileName);
        Assert.IsTrue(File.Exists(fullPath), $"Save file not found: {fullPath}");
        _json = File.ReadAllText(fullPath);
        Assert.IsNotEmpty(_json);
    }

    [SetUp]
    public void SetUp()
    {
        var s = KitchenSettings.Instance;
        Assert.IsNotNull(s);
        _guard = ProjectLoadStateGuard.Capture();
        s.AutoSave = false;
        s.SpatialGrid = false;
        s.NormalView.edgeOutline = false;
        ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        ClearScene();
        _guard?.Restore();
        FaceCache.Clear();
    }

    private void ClearScene()
    {
        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);
        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();

        ProjectInstructions.Reset();
        ProjectRooms.Reset();
        ProjectFloorplans.Reset();
    }

    private void Restore()
    {
        var data = SaveLoadManager.Deserialize(_json);
        Assert.IsNotNull(data);
        var objs = SaveLoadManager.RestoreScene(data!);
        Assert.IsNotEmpty(objs);
    }

    [Test, Category("Perf")]
    public void Report_RestoreBreakdown()
    {
        Restore();
        ClearScene();

        var hlGo = new GameObject("ElementHighlighter");
        var hl = hlGo.AddComponent<ElementHighlighter>();
        ElementHighlighter.Instance = hl;
        int refreshesBefore = hl.RefreshCount;

        var deserialize = Stopwatch.StartNew();
        var data = SaveLoadManager.Deserialize(_json);
        deserialize.Stop();
        Assert.IsNotNull(data);

        var whole = Stopwatch.StartNew();
        SaveLoadManager.RestoreScene(data!);
        whole.Stop();

        int refreshes = hl.RefreshCount - refreshesBefore;
        var all = PartRegistry.GetAll();
        int count = all.Count;

        ClearScene();
        ElementHighlighter.Instance = hl;
        int perElementBefore = hl.RefreshCount;
        var perElement = Stopwatch.StartNew();
        foreach (var ed in data!.elements)
            ElementRestorers.Restore(ElementFactory.Instance, ed);
        perElement.Stop();
        int perElementRefreshes = hl.RefreshCount - perElementBefore;

        ClearScene();
        ElementHighlighter.Instance = hl;
        var spawnOnly = Stopwatch.StartNew();
        using (HighlightBatch.Open())
            foreach (var ed in data.elements)
                ElementRestorers.Restore(ElementFactory.Instance, ed);
        spawnOnly.Stop();

        ClearScene();
        ElementHighlighter.Instance = hl;
        KitchenElement.SuppressVisualRebuild = true;
        var spawnNoMesh = Stopwatch.StartNew();
        using (HighlightBatch.Open())
            foreach (var ed in data.elements)
                ElementRestorers.Restore(ElementFactory.Instance, ed);
        spawnNoMesh.Stop();
        KitchenElement.SuppressVisualRebuild = false;

        all = PartRegistry.GetAll();

        var oneValidate = Stopwatch.StartNew();
        ConstraintValidator.Validate(all);
        oneValidate.Stop();

        var oneSync = Stopwatch.StartNew();
        EdgeSubstrate.SyncScene(all);
        oneSync.Stop();

        var oneRefresh = Stopwatch.StartNew();
        hl.RefreshHighlights();
        oneRefresh.Stop();

        Object.DestroyImmediate(hlGo);
        ElementHighlighter.Instance = null;

        Debug.Log(
            $"[LoadPerf] элементов {count}, undo {data.undoHistory?.Length ?? 0}\n"
            + $"[LoadPerf] Deserialize                 {deserialize.ElapsedMilliseconds} мс\n"
            + $"[LoadPerf] RestoreScene (с заслонкой)  {whole.ElapsedMilliseconds} мс, "
            + $"{refreshes} обновлений подсветки\n"
            + $"[LoadPerf] тот же цикл без заслонки    {perElement.ElapsedMilliseconds} мс, "
            + $"{perElementRefreshes} обновлений подсветки\n"
            + $"[LoadPerf] только цикл создания        {spawnOnly.ElapsedMilliseconds} мс\n"
            + $"[LoadPerf] цикл создания без мешей     {spawnNoMesh.ElapsedMilliseconds} мс\n"
            + $"[LoadPerf] один Validate               {oneValidate.Elapsed.TotalMilliseconds:F1} мс\n"
            + $"[LoadPerf] один EdgeSubstrate.SyncScene {oneSync.Elapsed.TotalMilliseconds:F1} мс\n"
            + $"[LoadPerf] один RefreshHighlights      {oneRefresh.Elapsed.TotalMilliseconds:F1} мс");

        Assert.Greater(count, 0, "сцена не восстановилась — мерить нечего");
    }
}
