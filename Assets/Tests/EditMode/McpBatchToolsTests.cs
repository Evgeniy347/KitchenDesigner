using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using Newtonsoft.Json.Linq;

/// <summary>
/// Тесты новой MCP-поверхности: батч-инструменты (get_elements, batch_edit,
/// clone_element), высокоуровневое размещение (align_element, distribute_evenly,
/// get_free_space), epsilon-семантика faceGaps и сериализация McpJson.
/// </summary>
public class McpBatchToolsTests
{
    private McpCommandHandler? _handler;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in _spawned)
            if (go != null) Object.DestroyImmediate(go);
        _spawned.Clear();
        foreach (var el in PartRegistry.GetAll())
            if (el != null) Object.DestroyImmediate(el.gameObject);
        PartRegistry.Clear();
        CommandStack.Clear();
    }

    private McpRequest MakeReq(string method, object data)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        return new McpRequest { id = "test", method = method, Params = JObject.Parse(json) };
    }

    private KitchenElement MakeElement(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        _spawned.Add(go);
        return e;
    }

    private static JObject Data(McpResponse resp) => JObject.FromObject(resp.data!);

    // ── McpJson: округление и отбрасывание null ─────────────────────────

    [Test]
    public void McpJson_RoundsFloatNoise_To4Decimals()
    {
        var json = McpJson.Serialize(new { posY = -0.009000001f, aabbMinY = -0.0180000011f, z = -3.62000012f });
        StringAssert.Contains("-0.009", json);
        StringAssert.Contains("-0.018", json);
        StringAssert.Contains("-3.62", json);
        StringAssert.DoesNotContain("0000001", json);
    }

    [Test]
    public void McpJson_DropsNullFields()
    {
        var info = new ElementInfo { name = "A" }; // drawer/table/faceGaps = null
        var json = McpJson.Serialize(info);
        StringAssert.DoesNotContain("\"drawer\"", json);
        StringAssert.DoesNotContain("\"table\"", json);
        StringAssert.DoesNotContain("\"faceGaps\"", json);
        StringAssert.DoesNotContain("\"faceNormalX\"", json);
    }

    // ── get_elements ─────────────────────────────────────────────────────

    [Test]
    public void GetElements_ByNames_ReturnsOnlyRequested_AndMissing()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "A", "Nope" } }));

        Assert.AreEqual("result", resp.type);
        var d = Data(resp);
        Assert.AreEqual(1, d["count"]!.Value<int>());
        Assert.AreEqual("A", d["elements"]![0]!["name"]!.Value<string>());
        Assert.AreEqual("Nope", d["missing"]![0]!.Value<string>());
    }

    [Test]
    public void GetElements_WildcardFilter_MatchesPrefix()
    {
        MakeElement("B4_side_L", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B4_side_R", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));
        MakeElement("Other", new Vector3Int(500, 400, 18), new Vector3(4f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_elements", new { filter = "B4_*" }));

        var d = Data(resp);
        Assert.AreEqual(2, d["count"]!.Value<int>());
    }

    [Test]
    public void GetElements_Summary_ReturnsCompactShape()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), new Vector3(1f, 2f, 3f));

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "A" }, summary = true }));

        var el = Data(resp)["elements"]![0]!;
        Assert.AreEqual("A", el["name"]!.Value<string>());
        Assert.AreEqual(500, el["dimX"]!.Value<int>());
        Assert.AreEqual(1f, el["posX"]!.Value<float>(), 1e-3f);
        Assert.IsNotNull(el["locked"]);
        Assert.IsNull(el["aabbMinX"], "summary не несёт AABB");
    }

    // ── get_violations {names} ───────────────────────────────────────────

    [Test]
    public void GetViolations_NamesFilter_LimitsReport()
    {
        MakeElement("A", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("B", new Vector3Int(1000, 1000, 1000), Vector3.zero);
        MakeElement("C", new Vector3Int(1000, 1000, 1000), new Vector3(0f, 0f, 0.2f));

        var resp = _handler!.Handle(MakeReq("get_violations", new { names = new[] { "A" } }));

        var d = Data(resp);
        Assert.AreEqual(1, d["count"]!.Value<int>());
        Assert.AreEqual("A", d["violations"]![0]!["name"]!.Value<string>());
    }

    // ── batch_edit ───────────────────────────────────────────────────────

    [Test]
    public void BatchEdit_AppliesSeveralOps_InOneCall()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var b = MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("batch_edit", new
        {
            ops = new object[]
            {
                new { name = "A", x = 1.0f, rot_y = 90f },
                new { name = "B", width = 600 }
            }
        }));

        Assert.AreEqual("result", resp.type, "batch failed: " + resp.data);
        Assert.AreEqual(1.0f, a.transform.position.x, 1e-4f);
        Assert.AreEqual(90f, a.transform.eulerAngles.y, 0.01f);
        Assert.AreEqual(600, b.DimensionsMM.x);
        var d = Data(resp);
        Assert.IsTrue(d["applied"]!.Value<bool>());
        Assert.AreEqual(2, (d["results"] as JArray)!.Count);
    }

    [Test]
    public void BatchEdit_IsAtomic_NothingAppliedOnAnyError()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("batch_edit", new
        {
            ops = new object[]
            {
                new { name = "A", x = 1.0f },
                new { name = "Missing", x = 2.0f }
            }
        }));

        Assert.AreEqual("error", resp.type);
        Assert.AreEqual(0f, a.transform.position.x, 1e-6f, "первый op не должен примениться");
    }

    [Test]
    public void BatchEdit_IsSingleUndoStep()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var b = MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        _handler!.Handle(MakeReq("batch_edit", new
        {
            ops = new object[]
            {
                new { name = "A", x = 1.0f },
                new { name = "B", x = 3.0f }
            }
        }));

        var undoResp = _handler!.Handle(MakeReq("undo", new { }));
        Assert.AreEqual("result", undoResp.type);
        Assert.AreEqual(0f, a.transform.position.x, 1e-4f, "undo откатывает ВЕСЬ батч");
        Assert.AreEqual(2f, b.transform.position.x, 1e-4f, "undo откатывает ВЕСЬ батч");
    }

    [Test]
    public void BatchEdit_DryRun_ReportsViolations_ButChangesNothing()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("batch_edit", new
        {
            dry_run = true,
            ops = new object[] { new { name = "A", x = 2.0f } } // прямо в B
        }));

        Assert.AreEqual("result", resp.type);
        var d = Data(resp);
        Assert.IsFalse(d["applied"]!.Value<bool>());
        var viol = d["results"]![0]!["violations"] as JArray;
        Assert.Greater(viol!.Count, 0, "dry-run должен сообщить о пересечении");
        Assert.AreEqual(0f, a.transform.position.x, 1e-6f, "dry-run не меняет сцену");
    }

    [Test]
    public void BatchEdit_RejectsLocked_UnlessOpUnlocks()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        a.Movable = false;

        var rejected = _handler!.Handle(MakeReq("batch_edit", new
        {
            ops = new object[] { new { name = "A", x = 1.0f } }
        }));
        Assert.AreEqual("error", rejected.type);
        StringAssert.Contains("LOCKED", Data(rejected)["message"]!.Value<string>());

        var unlocked = _handler!.Handle(MakeReq("batch_edit", new
        {
            ops = new object[] { new { name = "A", x = 1.0f, locked = false } }
        }));
        Assert.AreEqual("result", unlocked.type);
        Assert.AreEqual(1.0f, a.transform.position.x, 1e-4f);
        Assert.IsTrue(a.Movable, "locked:false снимает блокировку");
    }

    // ── clone_element ────────────────────────────────────────────────────

    [Test]
    public void CloneElement_CreatesCopies_WithOffsetsAndSuffixes()
    {
        MakeElement("Shelf", new Vector3Int(600, 18, 400), new Vector3(0f, 1f, 0f));

        var resp = _handler!.Handle(MakeReq("clone_element", new
        {
            name = "Shelf", count = 2, offset_y = 0.3f
        }));

        Assert.AreEqual("result", resp.type, "clone failed: " + resp.data);
        var d = Data(resp);
        Assert.AreEqual(2, (d["created"] as JArray)!.Count);

        var c2 = PartRegistry.GetAll().Find(e => e.PartName == "Shelf_2");
        var c3 = PartRegistry.GetAll().Find(e => e.PartName == "Shelf_3");
        Assert.IsNotNull(c2);
        Assert.IsNotNull(c3);
        Assert.AreEqual(1.3f, c2!.transform.position.y, 1e-4f);
        Assert.AreEqual(1.6f, c3!.transform.position.y, 1e-4f);
        Assert.AreEqual(new Vector3Int(600, 18, 400), c2.DimensionsMM);
    }

    [Test]
    public void CloneElement_IsSingleUndoStep()
    {
        MakeElement("Shelf", new Vector3Int(600, 18, 400), Vector3.zero);
        _handler!.Handle(MakeReq("clone_element", new { name = "Shelf", count = 3, offset_x = 0.5f }));
        Assert.AreEqual(4, PartRegistry.GetAll().Count);

        _handler!.Handle(MakeReq("undo", new { }));
        Assert.AreEqual(1, PartRegistry.GetAll().Count, "один undo убирает все клоны");
    }

    // ── align_element ────────────────────────────────────────────────────

    [Test]
    public void AlignElement_LeftToRight_MakesFlushContact()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("align_element", new
        {
            name = "A", face = "left", target = "B", target_face = "right"
        }));

        Assert.AreEqual("result", resp.type, "align failed: " + resp.data);
        // B.maxX = 2 + 0.25 = 2.25; A.minX должен встать туда → центр A = 2.5.
        Assert.AreEqual(2.5f, a.transform.position.x, 1e-4f);
    }

    [Test]
    public void AlignElement_WithGap_LeavesGapMm()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        _handler!.Handle(MakeReq("align_element", new
        {
            name = "A", face = "left", target = "B", target_face = "right", gap_mm = 100f
        }));

        Assert.AreEqual(2.6f, a.transform.position.x, 1e-4f, "центр = 2.25 + 0.1 (зазор) + 0.25 (полширины)");
    }

    [Test]
    public void AlignElement_Errors_WhenFacesOnDifferentAxes()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("align_element", new
        {
            name = "A", face = "left", target = "B", target_face = "top"
        }));

        Assert.AreEqual("error", resp.type);
    }

    // ── distribute_evenly ────────────────────────────────────────────────

    [Test]
    public void DistributeEvenly_CentersMiddleElements()
    {
        MakeElement("A", new Vector3Int(600, 18, 400), new Vector3(0f, 0f, 0f));
        var c = MakeElement("C", new Vector3Int(600, 18, 400), new Vector3(0f, 0.2f, 0f));
        MakeElement("B", new Vector3Int(600, 18, 400), new Vector3(0f, 1.0f, 0f));

        var resp = _handler!.Handle(MakeReq("distribute_evenly", new
        {
            names = new[] { "A", "B", "C" }, axis = "y"
        }));

        Assert.AreEqual("result", resp.type, "distribute failed: " + resp.data);
        Assert.AreEqual(0.5f, c.transform.position.y, 1e-4f, "средний элемент — в середину");
    }

    // ── get_free_space ───────────────────────────────────────────────────

    [Test]
    public void GetFreeSpace_ReturnsBoxBetweenTwoBoards()
    {
        MakeElement("L", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("R", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_free_space", new { between = new[] { "L", "R" } }));

        var d = Data(resp);
        Assert.IsTrue(d["free"]!.Value<bool>());
        Assert.AreEqual("x", d["separationAxis"]!.Value<string>());
        Assert.AreEqual(1500, d["sizeMmX"]!.Value<int>());
        Assert.AreEqual(400, d["sizeMmY"]!.Value<int>());
        Assert.AreEqual(18, d["sizeMmZ"]!.Value<int>());
        Assert.AreEqual(1.0f, d["centerX"]!.Value<float>(), 1e-3f);
    }

    [Test]
    public void GetFreeSpace_ReportsBlockers()
    {
        MakeElement("L", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("R", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));
        MakeElement("Mid", new Vector3Int(500, 400, 18), new Vector3(1f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_free_space", new { between = new[] { "L", "R" } }));

        var blockers = Data(resp)["blockers"] as JArray;
        Assert.IsNotNull(blockers);
        Assert.AreEqual("Mid", blockers![0]!["name"]!.Value<string>());
    }

    [Test]
    public void GetFreeSpace_NotFree_WhenOverlapping()
    {
        MakeElement("L", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("R", new Vector3Int(500, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("get_free_space", new { between = new[] { "L", "R" } }));

        Assert.IsFalse(Data(resp)["free"]!.Value<bool>());
    }

    // ── faceGaps: epsilon и фильтр соседей ───────────────────────────────

    [Test]
    public void ElementGaps_FlushContact_IsTouching_NotOverlap()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(0.5f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_element_gaps", new { name = "A" }));

        var gaps = (ElementGapsResult)resp.data!;
        var x = gaps.gaps.Find(g => g.axis == "x");
        Assert.IsNotNull(x);
        Assert.AreEqual("B", x!.neighbor);
        Assert.IsTrue(x.touching, "вплотную = touching");
        Assert.IsFalse(x.isOverlap, "float-шум контакта не считается пересечением");
        Assert.AreEqual(0f, x.gapMM, 1e-3f);
    }

    [Test]
    public void ElementGaps_SkipsNeighboursWithoutFacingProjection()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        // D далеко по X: «напротив» только по оси X; по Y и Z его проекции
        // по перпендикулярным осям не пересекаются → в соседях по Y/Z его нет.
        MakeElement("D", new Vector3Int(500, 400, 18), new Vector3(5f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_element_gaps", new { name = "A" }));

        var gaps = (ElementGapsResult)resp.data!;
        Assert.AreEqual(1, gaps.gaps.Count, "должна остаться только ось X");
        Assert.AreEqual("x", gaps.gaps[0].axis);
        Assert.AreEqual("D", gaps.gaps[0].neighbor);
        Assert.AreEqual(4500f, gaps.gaps[0].gapMM, 1f);
    }

    // ── Конверт мутаций и блокировка ─────────────────────────────────────

    [Test]
    public void MutationEnvelope_ContainsElementLockedField()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("move_element", new { name = "A", x = 1f }));

        var d = Data(resp);
        Assert.IsFalse(d["element"]!["locked"]!.Value<bool>());
        Assert.IsNotNull(d["sceneViolationCount"]);
        Assert.IsNotNull(d["violations"]);
    }

    [Test]
    public void LockedMove_ErrorMessage_ExplainsUnlock()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        a.Movable = false;

        var resp = _handler!.Handle(MakeReq("move_element", new { name = "A", x = 1f }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("set_element_lock", Data(resp)["message"]!.Value<string>());
        StringAssert.Contains("LOCKED", Data(resp)["message"]!.Value<string>());
    }
}
