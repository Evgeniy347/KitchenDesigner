using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;

/// <summary>cycle_drawer_animation повторяет клавишу E: крутит всё, у чего есть состояние
/// «открыто». Раньше инструмент проверял тип лестницей «ящик или диван», и каждый новый
/// IOpenable оставался за дверью, хотя в приложении по E он уже открывался.</summary>
public class McpCycleOpenableTests : McpTestFixture
{
    [Test]
    public void CycleAnimation_AcceptsEveryIOpenable_NotJustDrawersAndSofas()
    {
        var failures = new List<string>();
        foreach (var (type, _) in EveryElementType.Makers)
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "CycleProbe");
            _spawned.Add(element.gameObject);
            if (!PartRegistry.GetAll().Contains(element)) PartRegistry.Register(element);
            if (!(element is IOpenable openable)) continue;

            var resp = _handler!.Handle(MakeReq("cycle_drawer_animation", new { names = new[] { element.PartName } }));
            if (resp.type != "result")
                failures.Add($"{type.Name}: cycle refused: " + JObject.FromObject(resp.data!)["message"]);
            else if (!openable.IsOpen)
                failures.Add($"{type.Name}: cycled once from closed but IsOpen is still false");
        }

        Assert.IsEmpty(failures,
            "E в приложении крутит любой IOpenable; инструмент агента не должен знать меньше: "
            + string.Join("; ", failures));
        EveryElementType.ClearScene();
    }
}
