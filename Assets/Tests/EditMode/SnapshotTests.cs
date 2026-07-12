using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;

/// <summary>
/// Golden-master snapshot tests: каждый тест генерирует JSON сцены/элемента
/// и сравнивает с эталонным файлом в Snapshots/.
///
/// Первый запуск: снапшот-файл создаётся, тест FAIL → разработчик проверяет
/// содержимое, коммитит файл, перезапускает → PASS.
/// При изменении кода сериализации тест ловит любую разницу в JSON.
///
/// Чтобы обновить ВСЕ снапшоты разом: запустить UpdateAllSnapshots (Explicit).
/// Чтобы сбросить один снапшот: удалить файл из Snapshots/ и перезапустить тест.
/// </summary>
public class SnapshotTests
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();

        foreach (var e in Object.FindObjectsByType<KitchenElement>())
            if (e != null) Object.DestroyImmediate(e.gameObject);

        PartRegistry.Clear();
        GroupManager.Clear();
        CommandStack.Clear();
        ElementFactory.ClearPools();
        MaterialManager.ClearCache();
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private KitchenElement Add(GameObject go)
    {
        _spawned.Add(go);
        var el = go.GetComponent<KitchenElement>();
        if (el != null) PartRegistry.Register(el);
        return el;
    }

    private string CaptureJson(IEnumerable<KitchenElement> elements)
    {
        var data = SaveLoadManager.CaptureScene(elements);
        return SaveLoadManager.Serialize(data);
    }

    // ── Board (KitchenElement) snapshots ─────────────────────────────────

    [Test]
    public void Snapshot_Board_Default()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "DefaultBoard", Vector3.zero);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "board_default");
    }

    [Test]
    public void Snapshot_Board_CustomProperties()
    {
        var go = ElementFactory.CreatePart(new Vector3Int(600, 600, 18), "CustomBoard",
            new Vector3(1.5f, 2.5f, -3.0f));
        var el = Add(go);
        go.transform.rotation = Quaternion.Euler(0, 45, 0);
        el.Movable = false;
        el.Transparent = true;
        el.MaterialId = "oak";

        var json = CaptureJson(new[] { el });
        Snapshot.Match(json, "board_custom_properties");
    }

    [Test]
    public void Snapshot_Board_Multiple()
    {
        var elements = new List<KitchenElement>();
        for (int i = 0; i < 3; i++)
        {
            var go = ElementFactory.CreatePart(
                new Vector3Int(400 + i * 200, 400, 18),
                $"Board_{i}",
                new Vector3(i * 0.5f, 0.2f, 0));
            elements.Add(Add(go));
        }
        var json = CaptureJson(elements);
        Snapshot.Match(json, "board_multiple");
    }

    // ── FacadeElement snapshots ──────────────────────────────────────────

    [Test]
    public void Snapshot_Facade_Default()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "DefaultFacade", Vector3.zero,
            gapLeft: 2, gapRight: 2, gapTop: 2, gapBottom: 2);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "facade_default");
    }

    [Test]
    public void Snapshot_Facade_CustomGaps()
    {
        var go = ElementFactory.CreateFacade(
            new Vector3Int(600, 716, 18), "CustomFacade",
            new Vector3(1.0f, 0.4f, -2.0f),
            gapLeft: 3, gapRight: 5, gapTop: 2, gapBottom: 2);
        var facade = go.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.DrawerOut;
        facade.MaterialId = "wenge";
        Add(go);

        var json = CaptureJson(new[] { facade });
        Snapshot.Match(json, "facade_custom");
    }

    [Test]
    public void Snapshot_Facade_All18Modes()
    {
        var elements = new List<KitchenElement>();
        var allModes = new[]
        {
            DoorMode.HingeFrontLeft, DoorMode.HingeFrontRight, DoorMode.HingeFrontTop, DoorMode.HingeFrontBottom,
            DoorMode.HingeBackLeft, DoorMode.HingeBackRight, DoorMode.HingeBackTop, DoorMode.HingeBackBottom,
            DoorMode.HingeEdgeTopLeft, DoorMode.HingeEdgeTopRight, DoorMode.HingeEdgeBottomLeft, DoorMode.HingeEdgeBottomRight,
            DoorMode.DrawerOut, DoorMode.DrawerIn, DoorMode.DrawerRight, DoorMode.DrawerLeft, DoorMode.DrawerUp, DoorMode.DrawerDown,
        };

        for (int i = 0; i < allModes.Length; i++)
        {
            var go = ElementFactory.CreateFacade(
                new Vector3Int(450, 700, 18), $"Mode_{i}", new Vector3(i * 0.1f, 0, 0));
            var facade = go.GetComponent<FacadeElement>();
            facade.Mode = allModes[i];
            Add(go);
            elements.Add(facade);
        }

        var json = CaptureJson(elements);
        Snapshot.Match(json, "facade_all_18_modes");
    }

    // ── AssembledFacadeElement snapshots ─────────────────────────────────

    [Test]
    public void Snapshot_Assembled_Blind()
    {
        var go = ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 800, 18), "BlindAssembled",
            new Vector3(0.3f, 0.4f, -2.0f), AssembledFill.Blind);
        var assembled = go.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 3;
        assembled.Mode = DoorMode.HingeFrontRight;
        assembled.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { assembled });
        Snapshot.Match(json, "assembled_blind");
    }

    [Test]
    public void Snapshot_Assembled_Glass()
    {
        var go = ElementFactory.CreateAssembledFacade(
            new Vector3Int(450, 600, 18), "GlassAssembled",
            new Vector3(0.1f, 0.3f, -1.5f), AssembledFill.Glass);
        var assembled = go.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 0;
        assembled.Mode = DoorMode.HingeFrontLeft;
        Add(go);

        var json = CaptureJson(new[] { assembled });
        Snapshot.Match(json, "assembled_glass");
    }

    [Test]
    public void Snapshot_Assembled_Open()
    {
        var go = ElementFactory.CreateAssembledFacade(
            new Vector3Int(500, 800, 18), "OpenAssembled",
            Vector3.zero, AssembledFill.Open);
        var assembled = go.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 2;
        Add(go);

        var json = CaptureJson(new[] { assembled });
        Snapshot.Match(json, "assembled_open");
    }

    // ── RadialShelfElement snapshots ─────────────────────────────────────

    [Test]
    public void Snapshot_RadialShelf_Default()
    {
        var go = ElementFactory.CreateRadialShelf(300, 18, "DefaultRadial", Vector3.zero);
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "radial_default");
    }

    [Test]
    public void Snapshot_RadialShelf_Custom()
    {
        var go = ElementFactory.CreateRadialShelf(450, 18, "BigRadial",
            new Vector3(0.5f, 0.01f, -1.0f));
        var shelf = go.GetComponent<RadialShelfElement>();
        go.transform.rotation = Quaternion.Euler(0, 90, 0);
        shelf.MaterialId = "oak";
        Add(go);

        var json = CaptureJson(new[] { shelf });
        Snapshot.Match(json, "radial_custom");
    }

    // ── Wall snapshots ───────────────────────────────────────────────────

    [Test]
    public void Snapshot_Wall_Default()
    {
        var go = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 3000), "DefaultWall",
            new Vector3(-1.6f, 1.35f, 0));
        Add(go);
        var json = CaptureJson(new[] { go.GetComponent<KitchenElement>() });
        Snapshot.Match(json, "wall_default");
    }

    [Test]
    public void Snapshot_Wall_CustomMaterial()
    {
        var go = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 2000), "ConcreteWall",
            new Vector3(0, 1.35f, -3.5f));
        var el = Add(go);
        el.MaterialId = "concrete";

        var json = CaptureJson(new[] { el });
        Snapshot.Match(json, "wall_custom");
    }

    // ── Full scene snapshot ──────────────────────────────────────────────

    [Test]
    public void Snapshot_FullScene_AllTypes()
    {
        var elements = new List<KitchenElement>();

        // Board
        var boardGo = ElementFactory.CreatePart(
            new Vector3Int(800, 400, 18), "Board_A",
            new Vector3(0.5f, 0.2f, 1.0f));
        elements.Add(Add(boardGo));

        // Facade
        var facadeGo = ElementFactory.CreateFacade(
            new Vector3Int(450, 700, 18), "Facade_A",
            new Vector3(0.5f, 0.35f, -1.0f),
            gapLeft: 3, gapRight: 3, gapTop: 2, gapBottom: 2);
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.HingeFrontRight;
        elements.Add(facade);

        // Assembled
        var assembledGo = ElementFactory.CreateAssembledFacade(
            new Vector3Int(600, 800, 18), "Assembled_A",
            new Vector3(-0.3f, 0.4f, -2.0f), AssembledFill.Glass);
        var assembled = assembledGo.GetComponent<AssembledFacadeElement>();
        assembled.GrooveCount = 2;
        assembled.MaterialId = "oak";
        elements.Add(assembled);

        // Radial shelf
        var radialGo = ElementFactory.CreateRadialShelf(350, 18, "Radial_A",
            new Vector3(0.2f, 0.01f, -1.5f));
        elements.Add(Add(radialGo));

        // Wall
        var wallGo = ElementFactory.CreateWall(
            new Vector3Int(100, 2700, 3000), "Wall_A",
            new Vector3(-1.6f, 1.35f, 0));
        elements.Add(Add(wallGo));

        var json = CaptureJson(elements);
        Snapshot.Match(json, "fullscene_all_types");
    }

    // ── KitchenSettings snapshots ────────────────────────────────────────

    [Test]
    public void Snapshot_Settings_Default()
    {
        var gs = KitchenSettings.Instance;
        gs.GridStep = 1; gs.GridEnabled = true; gs.SnapEnabled = true;
        gs.SnapThreshold = 50f; gs.BlockOnViolation = true;
        gs.AutoSave = false; gs.AutoSaveInterval = 60;
        gs.SpatialGrid = false; gs.WindowedMode = true;
        gs.EdgeOutline = false; gs.WallsEnabled = true; gs.LowerNearWalls = false;
        gs.Save();

        var json = gs.GetSettingsJson();
        Snapshot.Match(json, "settings_default");
    }

    [Test]
    public void Snapshot_Settings_Custom()
    {
        var gs = KitchenSettings.Instance;
        gs.GridStep = 32; gs.GridEnabled = false; gs.SnapEnabled = false;
        gs.SnapThreshold = 80f; gs.BlockOnViolation = false;
        gs.AutoSave = true; gs.AutoSaveInterval = 120;
        gs.SpatialGrid = true; gs.WindowedMode = false;
        gs.EdgeOutline = true; gs.WallsEnabled = false; gs.LowerNearWalls = true;
        gs.Save();

        var json = gs.GetSettingsJson();
        Snapshot.Match(json, "settings_custom");
    }

    // ── Full ProjectData snapshot (elements + groups + camera + baseplate) ─

    [Test]
    public void Snapshot_FullProjectData()
    {
        var elements = new List<KitchenElement>();

        // Board + Facade
        var boardGo = ElementFactory.CreatePart(new Vector3Int(800, 400, 18), "Board_1",
            new Vector3(0.5f, 0.2f, 0.0f));
        elements.Add(boardGo.GetComponent<KitchenElement>());
        Add(boardGo);

        var facadeGo = ElementFactory.CreateFacade(new Vector3Int(450, 700, 18), "Facade_1",
            new Vector3(0.0f, 0.35f, -1.0f), 3, 3, 2, 2);
        var facade = facadeGo.GetComponent<FacadeElement>();
        facade.Mode = DoorMode.HingeFrontLeft;
        elements.Add(facade);
        Add(facadeGo);

        // Groups — link board and facade
        var group = GroupManager.Link(new List<KitchenElement> {
            boardGo.GetComponent<KitchenElement>(), facade });

        // BasePlate
        var bp = BasePlate.Create();
        bp.Element.DimensionsMM = new Vector3Int(3500, 18, 4000);
        bp.transform.position = new Vector3(0, -0.009f, 0);
        _spawned.Add(bp.gameObject);

        // Capture scene
        var data = SaveLoadManager.CaptureScene(elements);

        // Inject camera state (not available in EditMode)
        data.camera = new CameraState
        {
            valid = true,
            targetX = 0.5f, targetY = 0.3f, targetZ = -1.0f,
            angleX = 35f, angleY = 45f, distance = 6.0f
        };

        var json = SaveLoadManager.Serialize(data);
        Snapshot.Match(json, "full_project_data");
    }

    // ── Мета-тест: проверка что снапшоты для всего выше созданы ──────────

    [Test]
    public void AllSnapshots_Exist()
    {
        var names = new[]
        {
            "board_default", "board_custom_properties", "board_multiple",
            "facade_default", "facade_custom", "facade_all_18_modes",
            "assembled_blind", "assembled_glass", "assembled_open",
            "radial_default", "radial_custom",
            "wall_default", "wall_custom",
            "fullscene_all_types",
            "settings_default", "settings_custom",
            "full_project_data",
            "undo_composite_two_moves", "undo_resize_command",
            "undo_deep_chain_50_trimmed", "undo_deep_siblings_50_preserved",
        };

        var missing = new System.Text.StringBuilder();
        foreach (var name in names)
        {
            if (!Snapshot.Exists(name))
                missing.AppendLine($"  - {name}.verified.json");
        }

        if (missing.Length > 0)
            Assert.Fail(
                $"Missing verified snapshots:\n{missing}\n" +
                "Run SnapshotTests (this fixture) once — it will create *.candidate.json files.\n" +
                "Then run AcceptAllCandidates (Explicit) to rename them to *.verified.json.");
    }

    // ── Accept candidates (Explicit — только по запросу) ─────────────────

    [Test]
    [Explicit]
    public void AcceptAllCandidates()
    {
        var dir = Snapshot.SnapshotDir;
        var candidates = System.IO.Directory.GetFiles(dir, "*.candidate.json");
        if (candidates.Length == 0)
            Assert.Ignore("No candidate files to accept.");

        foreach (var candidate in candidates)
        {
            var verified = candidate.Replace(".candidate.json", ".verified.json");
            System.IO.File.Copy(candidate, verified, overwrite: true);
            System.IO.File.Delete(candidate);
            TestContext.WriteLine($"  Accepted: {System.IO.Path.GetFileName(verified)}");
        }

        Assert.Pass($"Accepted {candidates.Length} snapshot(s). Commit the new *.verified.json files.");
    }
}
