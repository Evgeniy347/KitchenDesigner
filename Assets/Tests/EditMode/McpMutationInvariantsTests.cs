using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Инварианты, общие для всех адресных мутаций MCP: результат ложится
/// на целый миллиметр, прикреплённые детали едут за родителем без ресайза, а
/// ETag не дёргается от суб-миллиметрового дрейфа.</summary>
public class McpMutationInvariantsTests
{
    private McpCommandHandler? _handler;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    [SetUp]
    public void Setup()
    {
        _handler = new McpCommandHandler();
        PartRegistry.Clear();
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
    }

    private McpRequest MakeReq(string method, object data) => new McpRequest
    {
        id = "test",
        method = method,
        Params = JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data))
    };

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private KitchenElement MakeBoard(string name, Vector3Int dims, Vector3 pos)
    {
        var go = new GameObject(name);
        _spawned.Add(go);
        go.transform.position = pos;
        var e = go.AddComponent<KitchenElement>();
        e.PartName = name;
        e.DimensionsMM = dims;
        PartRegistry.Register(e);
        return e;
    }

    private static float MinFaceMm(KitchenElement el, int axis)
    {
        var verts = el.GetVertices();
        float min = float.MaxValue;
        foreach (var v in verts) min = Mathf.Min(min, v[axis]);
        return min / AppConstants.MM_TO_UNITS;
    }

    [Test]
    public void AlignElements_FractionalGap_StillLandsTheFaceOnAWholeMillimetre()
    {
        var a = MakeBoard("A", new Vector3Int(500, 400, 18), Vector3.zero);
        MakeBoard("B", new Vector3Int(501, 400, 18), new Vector3(2f, 0f, 0f));

        var resp = _handler!.Handle(MakeReq("align_elements", new
        {
            ops = new object[]
            {
                new { name = "A", face = "left", target = "B", target_face = "right", gap_mm = 3.4f }
            }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        float faceMm = MinFaceMm(a, 0);
        Assert.AreEqual(Mathf.Round(faceMm), faceMm, 1e-2f,
            "gap_mm — float, и полугабарит цели бывает нечётным; грань выравниваемой " +
            "детали всё равно обязана встать на целый миллиметр, иначе раскрой получит дробь");
    }

    [Test]
    public void DistributeEvenly_SpanThatDoesNotDivideEvenly_StillLandsFacesOnWholeMillimetres()
    {
        MakeBoard("A", new Vector3Int(600, 18, 400), Vector3.zero);
        var middle = MakeBoard("C", new Vector3Int(600, 18, 400), new Vector3(0f, 0.2f, 0f));
        MakeBoard("B", new Vector3Int(600, 18, 400), new Vector3(0f, 1.001f, 0f));

        var resp = _handler!.Handle(MakeReq("distribute_evenly", new
        {
            names = new[] { "A", "B", "C" }, axis = "y"
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        float faceMm = MinFaceMm(middle, 1);
        Assert.AreEqual(Mathf.Round(faceMm), faceMm, 1e-2f,
            "пролёт 1001 мм на три детали даёт ровно 0,5 мм на шаг — грань всё равно " +
            "ставится на целый миллиметр");
    }

    [Test]
    public void EditElements_MovingAHost_CarriesTheAttachedPartAlong()
    {
        var host = MakeBoard("Host", new Vector3Int(600, 400, 18), Vector3.zero);
        var child = MakeBoard("Child", new Vector3Int(100, 100, 18), new Vector3(0f, 0.3f, 0f));

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Child", attached_to_name = "Host" } }
        }));
        var hostBefore = host.transform.position;
        var childBefore = child.transform.position;
        var resp = _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Host", anchor_x_mm = 1000f } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        var hostDelta = host.transform.position - hostBefore;
        Assert.AreNotEqual(0f, hostDelta.x,
            "родитель обязан сдвинуться, иначе проверка ниже сравнивает два нуля");
        Assert.AreEqual(hostDelta.x, child.transform.position.x - childBefore.x, 1e-4f,
            "прикреплённая деталь едет за родителем НА ТОТ ЖЕ вектор — иначе связь "
            + "распадается при первом же переносе. Сравниваем СДВИГИ, а не координаты: "
            + "anchor_x_mm задаёт минимальный угол родителя, и его центр стоит на "
            + "пол-габарита дальше, тогда как ребёнок повторяет именно сдвиг");
    }

    [Test]
    public void EditElements_ResizingAHost_LeavesTheAttachedPartsSizeAlone()
    {
        MakeBoard("Host", new Vector3Int(600, 400, 18), Vector3.zero);
        var child = MakeBoard("Child", new Vector3Int(100, 100, 18), new Vector3(0f, 0.3f, 0f));
        var childDimsBefore = child.DimensionsMM;

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Child", attached_to_name = "Host" } }
        }));
        var resp = _handler.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "Host", width = 1200 } }
        }));

        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        Assert.AreEqual(childDimsBefore, child.DimensionsMM,
            "за родителем передаётся ТОЛЬКО перенос и поворот: ресайз ребёнку не транслируется, " +
            "иначе растянутая полка растянула бы и приклеенную к ней накладку");
    }

    [Test]
    public void GetAllElements_SubMillimetreDrift_DoesNotChangeTheEtag()
    {
        var board = MakeBoard("A", new Vector3Int(500, 400, 18), Vector3.zero);
        string? before = _handler!.Handle(MakeReq("get_all_elements", new { })).etag;

        board.transform.position += new Vector3(0.00004f, 0f, 0f);
        string? after = _handler.Handle(MakeReq("get_all_elements", new { })).etag;

        Assert.AreEqual(before, after,
            "ETag считается по McpJson с округлением до 0,1 мм: дрейф координат на " +
            "сотые доли миллиметра не должен инвалидировать кэш клиента");
    }

    [Test]
    public void GetAllElements_RealMove_DoesChangeTheEtag()
    {
        var board = MakeBoard("A", new Vector3Int(500, 400, 18), Vector3.zero);
        string? before = _handler!.Handle(MakeReq("get_all_elements", new { })).etag;

        board.transform.position += new Vector3(0.1f, 0f, 0f);
        string? after = _handler.Handle(MakeReq("get_all_elements", new { })).etag;

        Assert.AreNotEqual(before, after, "настоящий перенос обязан менять ETag");
    }
}
