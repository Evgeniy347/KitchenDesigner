using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;
using Newtonsoft.Json.Linq;

/// <summary>
/// Тесты новой MCP-поверхности: батч-инструменты (get_elements, edit_elements,
/// clone_elements), высокоуровневое размещение (align_elements, distribute_evenly,
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
        var info = new ElementInfo { name = "A" };
        var json = McpJson.Serialize(info);
        StringAssert.DoesNotContain("\"drawer\"", json);
        StringAssert.DoesNotContain("\"table\"", json);
        StringAssert.DoesNotContain("\"faceGaps\"", json);
        StringAssert.DoesNotContain("\"faceNormalX\"", json);
    }

    [Test]
    public void McpJson_UnknownParameterField_IsRejected_AndTheAnswerNamesIt()
    {
        var typo = JObject.Parse("{\"names\":[\"A\"],\"nmaes\":[\"B\"]}");

        var ex = Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
            () => typo.ToObjectStrict<ParamsNames>(),
            "лишнее поле в параметрах — это опечатка агента или отставший клиент; "
            + "молча проглотить его значит выполнить НЕ ту команду, о которой просили");

        StringAssert.Contains("nmaes", ex!.Message,
            "в ответе должно стоять имя незнакомого поля: без него агент видит только "
            + "«contract violation» и не знает, что именно исправить");
        StringAssert.Contains("MCP parameter contract violation", ex.Message);
    }

    [Test]
    public void McpJson_KnownFieldsOnly_DeserializeWithoutComplaint()
    {
        var ok = JObject.Parse("{\"names\":[\"A\",\"B\"]}");

        var parsed = ok.ToObjectStrict<ParamsNames>();

        CollectionAssert.AreEqual(new[] { "A", "B" }, parsed.names,
            "положительный контроль к строгой проверке: без него тест выше зеленел бы "
            + "и на разборе, который отвергает вообще всё");
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

    [Test]
    public void GetViolations_AlsoCarriesWarnings_WhichTheSceneNeverHighlights()
    {
        MakeElement("A", new Vector3Int(600, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(600, 400, 18), new Vector3(0.601f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_violations", new { }));

        var issues = (JArray)Data(resp)["issues"]!;
        var warnings = issues.Where(i => i["level"]!.Value<string>() == "warning").ToList();
        CollectionAssert.IsNotEmpty(warnings,
            "зазор 1 мм — это GAP-01, предупреждение уровня warning. На сцене такие "
            + "НЕ подсвечиваются: агент видит их только здесь, и если ответ несёт одни "
            + "коллизии, половина окна «Ошибки» для него не существует");
        Assert.AreEqual("GAP-01", warnings[0]!["code"]!.Value<string>(),
            "предупреждение приходит с тем же стабильным кодом, что и в окне «Ошибки» — "
            + "тот же источник SceneAnalyzer, а не отдельный разбор для MCP");
    }

    // ── edit_elements ────────────────────────────────────────────────────

    [Test]
    public void EditElements_AppliesSeveralOps_InOneCall()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        var b = MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[]
            {
                new { name = "A", x = 1.0f, rot_y = 90f },
                new { name = "B", width = 600 }
            }
        }));

        Assert.AreEqual("result", resp.type, "edit failed: " + resp.data);
        Assert.AreEqual(1.0f, a.transform.position.x, 1e-4f);
        Assert.AreEqual(90f, a.transform.eulerAngles.y, 0.01f);
        Assert.AreEqual(600, b.DimensionsMM.x);
        var d = Data(resp);
        Assert.IsTrue(d["ok"]!.Value<bool>());
        Assert.AreEqual(2, (d["results"] as JArray)!.Count);
    }

    [Test]
    public void EditElements_OmittedAxis_KeepsItsValue_InsteadOfBeingReadAsZero()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), new Vector3(1.5f, 0.9f, 2.25f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", x = 3.0f } }
        }));

        Assert.AreEqual("result", resp.type, "edit failed: " + resp.data);
        Assert.AreEqual(3.0f, a.transform.position.x, 1e-4f);
        Assert.AreEqual(0.9f, a.transform.position.y, 1e-4f,
            "пропущенная ось — «оставь как есть», а НЕ 0: иначе правка одной координаты "
            + "роняет деталь на пол");
        Assert.AreEqual(2.25f, a.transform.position.z, 1e-4f, "то же по Z");
        Assert.AreEqual(new Vector3Int(500, 400, 18), a.DimensionsMM,
            "размеры не указывали — они и не менялись");
    }

    [Test]
    public void EditElements_IsAtomic_NothingAppliedOnAnyError()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
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
    public void EditElements_DryRun_ReportsViolations_ButChangesNothing()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            dry_run = true,
            ops = new object[] { new { name = "A", x = 2.0f } }
        }));

        Assert.AreEqual("result", resp.type);
        var d = Data(resp);
        Assert.IsTrue(d["dryRun"]!.Value<bool>());
        Assert.IsFalse(d["applied"]!.Value<bool>());
        var viol = d["results"]![0]!["violations"] as JArray;
        Assert.Greater(viol!.Count, 0, "dry-run должен сообщить о пересечении");
        Assert.AreEqual(0f, a.transform.position.x, 1e-6f, "dry-run не меняет сцену");
    }

    [Test]
    public void EditElements_RejectsLocked_UnlessOpUnlocks()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        a.Movable = false;

        var rejected = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", x = 1.0f } }
        }));
        Assert.AreEqual("error", rejected.type);
        StringAssert.Contains("LOCKED", Data(rejected)["message"]!.Value<string>());

        var unlocked = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", x = 1.0f, locked = false } }
        }));
        Assert.AreEqual("result", unlocked.type);
        Assert.AreEqual(1.0f, a.transform.position.x, 1e-4f);
        Assert.IsTrue(a.Movable, "locked:false снимает блокировку");
    }

    // ── clone_elements ───────────────────────────────────────────────────

    [Test]
    public void CloneElements_CreatesCopies_WithOffsetsAndSuffixes()
    {
        MakeElement("Shelf", new Vector3Int(600, 18, 400), new Vector3(0f, 1f, 0f));

        var resp = _handler!.Handle(MakeReq("clone_elements", new
        {
            ops = new object[]
            {
                new { name = "Shelf", count = 2, offset_y = 0.3f }
            }
        }));

        Assert.AreEqual("result", resp.type, "clone failed: " + resp.data);
        var d = Data(resp);
        Assert.AreEqual(2, (d["created"] as JArray)!.Count);

        // Суффикс уникальности идёт с «_1» (ElementNaming), а не с «_2».
        var c2 = PartRegistry.GetAll().Find(e => e.PartName == "Shelf_1");
        var c3 = PartRegistry.GetAll().Find(e => e.PartName == "Shelf_2");
        Assert.IsNotNull(c2);
        Assert.IsNotNull(c3);
        Assert.AreEqual(1.3f, c2!.transform.position.y, 1e-4f);
        Assert.AreEqual(1.6f, c3!.transform.position.y, 1e-4f);
        Assert.AreEqual(new Vector3Int(600, 18, 400), c2.DimensionsMM);
    }

    // ── align_elements ───────────────────────────────────────────────────

    [Test]
    public void AlignElements_LeftToRight_MakesFlushContact()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("align_elements", new
        {
            ops = new object[]
            {
                new { name = "A", face = "left", target = "B", target_face = "right" }
            }
        }));

        Assert.AreEqual("result", resp.type, "align failed: " + resp.data);
        Assert.AreEqual(2.5f, a.transform.position.x, 1e-4f);
    }

    [Test]
    public void AlignElements_WithGap_LeavesGapMm()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        _handler!.Handle(MakeReq("align_elements", new
        {
            ops = new object[]
            {
                new { name = "A", face = "left", target = "B", target_face = "right", gap_mm = 100f }
            }
        }));

        Assert.AreEqual(2.6f, a.transform.position.x, 1e-4f, "центр = 2.25 + 0.1 (зазор) + 0.25 (пол ширирины)");
    }

    [Test]
    public void AlignElements_Errors_WhenFacesOnDifferentAxes()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("B", new Vector3Int(500, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("align_elements", new
        {
            ops = new object[]
            {
                new { name = "A", face = "left", target = "B", target_face = "top" }
            }
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

        var resp = _handler!.Handle(MakeReq("get_element_gaps", new { names = new[] { "A" } }));

        var d = Data(resp);
        var gapsArray = d["results"]![0]!["gaps"] as JArray;
        JObject? xGap = null;
        foreach (JObject g in gapsArray!)
            if (g["axis"]!.Value<string>() == "x") { xGap = g; break; }
        Assert.IsNotNull(xGap);
        Assert.AreEqual("B", xGap!["neighbor"]!.Value<string>());
        Assert.IsTrue(xGap!["touching"]!.Value<bool>(), "вплотную = touching");
        Assert.IsFalse(xGap!["isOverlap"]!.Value<bool>(), "float-шум контакта не считается пересечением");
        Assert.AreEqual(0f, xGap!["gapMM"]!.Value<float>(), 1e-3f);
    }

    [Test]
    public void ElementGaps_SkipsNeighboursWithoutFacingProjection()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeElement("D", new Vector3Int(500, 400, 18), new Vector3(5f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("get_element_gaps", new { names = new[] { "A" } }));

        var d = Data(resp);
        var gapsArray = d["results"]![0]!["gaps"] as JArray;
        Assert.AreEqual(1, gapsArray!.Count, "должна остаться только ось X");
        Assert.AreEqual("x", gapsArray[0]!["axis"]!.Value<string>());
        Assert.AreEqual("D", gapsArray[0]!["neighbor"]!.Value<string>());
        Assert.AreEqual(4500f, gapsArray[0]!["gapMM"]!.Value<float>(), 1f);
    }

    // ── Конверт мутаций и блокировка ─────────────────────────────────────

    [Test]
    public void MutationEnvelope_ContainsElementLockedField()
    {
        MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", x = 1f } }
        }));

        var d = Data(resp);
        Assert.IsFalse(d["results"]![0]!["locked"]!.Value<bool>());
        Assert.IsNotNull(d["sceneViolationCount"]);
    }

    [Test]
    public void LockedMove_ErrorMessage_ExplainsUnlock()
    {
        var a = MakeElement("A", new Vector3Int(500, 400, 18), Vector3.zero);
        a.Movable = false;

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "A", x = 1f } }
        }));

        Assert.AreEqual("error", resp.type);
        StringAssert.Contains("edit_elements", Data(resp)["message"]!.Value<string>());
        StringAssert.Contains("LOCKED", Data(resp)["message"]!.Value<string>());
    }
}
