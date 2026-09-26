using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.Construction;
using KitchenDesigner.Core.MCP;

/// <summary>#17 (test-results/review-construction.md): <see cref="WallLayerElement.HostWallName"/>
/// carries <c>[NotUndoable]</c> because the AUTOMATIC snap on spawn must not fight undo. But
/// <c>edit_elements wall_layer_host_wall_name</c> is a deliberate, explicit MCP edit — it changes
/// host, pose and dimensions through <c>SnapToNamedWall</c> and must still be one ordinary undo
/// step, or the next wall move re-seats the layer on the wrong wall with no way back.</summary>
public class WallLayerMcpUndoTests : McpTestFixture
{
    [SetUp]
    public void ExtraSetUp() => CommandStack.Clear();

    [TearDown]
    public void ExtraTearDown() => CommandStack.Clear();

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private Wall MakeWall(string name, Vector3Int dims, Vector3 position)
    {
        var go = ElementFactory.CreateWall(dims, name, position);
        _spawned.Add(go);
        return go.GetComponent<Wall>()!;
    }

    private InsulationElement MakeInsulation(string name, string hostWallName, Vector3 nearPosition)
    {
        var go = ElementRoot.NewCube(name, "Слой стены", nearPosition);
        _spawned.Add(go);
        var layer = go.AddComponent<InsulationElement>();
        layer.PartName = go.name;
        layer.Movable = true;
        layer.DimensionsMM = new Vector3Int(100, 100, WallLayerDefaults.InsulationThicknessMm);
        ElementRoot.Publish(go, layer);
        layer.SnapToNamedWall(hostWallName);
        return layer;
    }

    [Test]
    public void EditElements_RehostViaMcp_IsUndoable()
    {
        MakeWall("Wall_A", new Vector3Int(3000, 2500, 100), new Vector3(0f, 1.25f, 0f));
        MakeWall("Wall_B", new Vector3Int(3000, 2500, 100), new Vector3(5f, 1.25f, 0f));
        var layer = MakeInsulation("Ins_Rehost", "Wall_A", new Vector3(0f, 1.25f, 0.5f));

        string hostBefore = layer.HostWallName;
        var posBefore = layer.transform.position;
        Assert.AreEqual("Wall_A", hostBefore, "проверка сама себе: слой обязан был сесть на Wall_A");

        var resp = _handler!.Handle(MakeReq("edit_elements",
            new { ops = new[] { new { name = "Ins_Rehost", wall_layer_host_wall_name = "Wall_B" } } }));
        Assert.AreEqual("result", resp.type, resp.type == "error" ? ErrorMessage(resp) : "");
        Assert.AreEqual("Wall_B", layer.HostWallName,
            "проверка сама себе: перепривязка через MCP обязана была случиться");

        CommandStack.Undo();

        Assert.AreEqual(hostBefore, layer.HostWallName,
            "MCP-перепривязка обязана быть обычным шагом отмены: Undo не вернул старую стену");
        Assert.AreEqual(posBefore.x, layer.transform.position.x, 0.001f,
            "и позицию — слой обязан вернуться на старую стену, а не остаться висеть на новой");
        Assert.AreEqual(posBefore.z, layer.transform.position.z, 0.001f);
    }
}
