using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Сценарный сенсор этапа 2: «три шкафа под окном вплотную к стене» глазами слабой модели.
/// Путь через place: оглядеться, прочитать стену и окно, поставить. Контроль: тот же результат
/// старым путём, где модель сама считает координаты из прочитанных чисел. Считаются ВЫЗОВЫ и БАЙТЫ
/// ответов ровно такими, какими их видит агент (McpToolCall).</summary>
public class McpPlaceScenarioTests : McpTestFixture
{
    private const int CallBudget = 6;
    private const int ByteBudget = 3 * 1024;

    private int _calls;
    private int _bytes;

    [SetUp]
    public void Setup()
    {
        CommandStack.Clear();
        ProjectInstructions.Reset();
        BuildTheRoom();
    }

    [TearDown]
    public void Teardown()
    {
        CommandStack.Clear();
        ProjectInstructions.Reset();
        LevelRegistry.Reset();
    }

    private JObject Wire(string tool, object args, bool counted = true)
    {
        var def = McpToolRegistry.Tools.First(t => t.Name == tool);
        var content = new McpToolCall(_handler!.Handle).Invoke(def, JObject.FromObject(args), "scenario");
        var text = content["content"]![0]!["text"]!.Value<string>()!;
        Assert.IsNull(content["isError"], tool + " отказал: " + text);
        if (counted)
        {
            _calls++;
            _bytes += Encoding.UTF8.GetByteCount(text);
        }
        return JObject.Parse(text);
    }

    private void BuildTheRoom()
    {
        Wire("create_walls", new
        {
            segments = new[] { new { name = "Wall", from_x = 0, from_z = 0, to_x = 4000, to_z = 0, kind = "bearing", height = 2700, thickness_mm = 200 } }
        }, counted: false);
        Wire("create_floor", new
        {
            name = "Floor", top_y_mm = 0, thickness_mm = 100,
            poly = new[] { new { x = 0, z = -100 }, new { x = 4000, z = -100 }, new { x = 4000, z = 3000 }, new { x = 0, z = 3000 } }
        }, counted: false);
        Wire("add_opening", new
        {
            name = "Win1", wall = "Wall", kind = "window", offset_mm = 1500, width = 1200, height = 1200, sill_mm = 900
        }, counted: false);
    }

    private static Vector3 MinMm(string name)
    {
        var element = PartRegistry.GetAll().First(e => e.PartName == name);
        var min = element.GetVertices()[0];
        foreach (var v in element.GetVertices()) min = Vector3.Min(min, v);
        return min * 1000f;
    }

    private static object[] Row(string name, params object[] against) => new object[]
    {
        new { name, width = 600, height = 720, depth = 560, against },
    };

    private void ThreeCabinetsThroughPlace()
    {
        Wire("get_scene_tree", new { });
        Wire("get", new { names = new[] { "Wall", "Win1" } });
        var reply = Wire("place", new
        {
            items = new object[]
            {
                new
                {
                    name = "Cab2", width = 600, height = 720, depth = 560,
                    against = new[] { new { target = "Wall", face = "front" } },
                    align = new[] { new { target = "Win1", axis = "x", at = "center" } },
                },
                new
                {
                    name = "Cab1", width = 600, height = 720, depth = 560,
                    against = new[] { new { target = "Wall", face = "front" }, new { target = "Cab2", face = "left" } },
                },
                new
                {
                    name = "Cab3", width = 600, height = 720, depth = 560,
                    against = new[] { new { target = "Wall", face = "front" }, new { target = "Cab2", face = "right" } },
                },
            }
        });
        CollectionAssert.IsEmpty(reply["sceneViolationDelta"]!["added"]!.ToObject<string[]>(),
            "place не сломал ничего в сцене: " + reply["sceneViolationDelta"]);
    }

    private void ThreeCabinetsTheOldWay()
    {
        Wire("get_scene_tree", new { });
        var read = Wire("get", new { names = new[] { "Wall", "Win1" } });
        var wall = read["elements"]!.First(e => e["name"]!.Value<string>() == "Wall");
        var window = read["elements"]!.First(e => e["name"]!.Value<string>() == "Win1");
        float windowCenterX = window["posMm"]![0]!.Value<float>() + window["footprintMm"]![0]!.Value<float>() / 2f;
        float wallFrontZ = wall["posMm"]![2]!.Value<float>() + wall["footprintMm"]![2]!.Value<float>();
        float centre2 = windowCenterX - 300f;

        object Item(string name, float x) => new
        {
            name, width = 600, height = 720, depth = 560, anchor_x_mm = x, anchor_y_mm = 0f, anchor_z_mm = wallFrontZ,
        };
        var created = Wire("create_elements", new { items = new[] { Item("Cab2", centre2), Item("Cab1", centre2 - 600f), Item("Cab3", centre2 + 600f) } });
        CollectionAssert.IsEmpty(created["sceneViolationDelta"]!["added"]!.ToObject<string[]>(),
            "и старым путём ничего не сломано: " + created["sceneViolationDelta"]);
    }

    [Test]
    public void ThreeCabinetsUnderTheWindow_ViaPlace_FitTheCallAndByteBudget_WithNoNewViolation()
    {
        ThreeCabinetsThroughPlace();

        TestContext.WriteLine($"SCENARIO place: calls={_calls}, bytes={_bytes}");
        Assert.LessOrEqual(_calls, CallBudget, "сценарий не должен требовать больше вызовов");
        Assert.LessOrEqual(_bytes, ByteBudget,
            $"ответы сценария заняли {_bytes} байт при бюджете {ByteBudget}: place обязан экономить контекст слабой модели");
        var cab2 = MinMm("Cab2");
        Assert.AreEqual(100f, cab2.z, 0.2f, "вплотную к комнатной грани стены (толщина 200, ось z=0)");
        Assert.AreEqual(0f, cab2.y, 0.2f, "на полу");
        Assert.AreEqual(cab2.x - 600f, MinMm("Cab1").x, 0.2f);
        Assert.AreEqual(cab2.x + 600f, MinMm("Cab3").x, 0.2f);
    }

    [Test]
    public void TheOldWay_GivesTheSameRow_AsAControl_ForCallsAndBytes()
    {
        ThreeCabinetsThroughPlace();
        var placed = new[] { MinMm("Cab1"), MinMm("Cab2"), MinMm("Cab3") };
        int placeCalls = _calls, placeBytes = _bytes;
        _handler!.Handle(MakeReq("undo", new { }));
        Assume.That(PartRegistry.GetAll().Any(e => e.PartName == "Cab1"), Is.False, "предусловие: undo убрал ряд");
        _calls = 0;
        _bytes = 0;

        ThreeCabinetsTheOldWay();

        TestContext.WriteLine($"SCENARIO control (old path): calls={_calls}, bytes={_bytes}; place: calls={placeCalls}, bytes={placeBytes}");
        var control = new[] { MinMm("Cab1"), MinMm("Cab2"), MinMm("Cab3") };
        for (int i = 0; i < 3; i++)
            Assert.AreEqual(0f, Vector3.Distance(placed[i], control[i]), 0.3f,
                "place ставит туда же, куда приводят ручные расчёты по прочитанным числам: решатель считает то же самое, но не требует от модели арифметики");
        Assert.LessOrEqual(placeCalls, _calls + 1, "place не дороже ручного пути по числу вызовов");
    }
}
