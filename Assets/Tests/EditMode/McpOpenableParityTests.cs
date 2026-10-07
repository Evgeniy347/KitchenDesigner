using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Всё, что в панели открывается кнопкой (<see cref="IOpenable"/>), агент открывает
/// полем is_open. До этой проверки список «кому можно» был написан руками рядом с
/// самим интерфейсом и разошёлся с ним дважды: диван и ящик были IOpenable, а поле
/// у них отклонялось. Тип, ставший IOpenable завтра, попадает под проверку сам.</summary>
public class McpOpenableParityTests : McpTestFixture
{
    private McpResponse Open(string name, bool open)
        => _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name, is_open = open } },
        }));

    [Test]
    public void McpUiOpenableParity_EveryIOpenableAcceptsIsOpen_AndReallyOpensAndCloses()
    {
        var failures = new List<string>();
        int openables = 0;
        foreach (var (type, _) in EveryElementType.Makers)
        {
            EveryElementType.ClearScene();
            var element = EveryElementType.Spawn(type, "OpenProbe");
            _spawned.Add(element.gameObject);
            if (!PartRegistry.GetAll().Contains(element)) PartRegistry.Register(element);
            if (!(element is IOpenable openable)) continue;
            openables++;

            var opened = Open(element.PartName, true);
            if (opened.type != "result")
                failures.Add($"{type.Name}: is_open=true refused: "
                    + JObject.FromObject(opened.data!)["message"]);
            else if (!openable.IsOpen)
                failures.Add($"{type.Name}: is_open=true accepted but IsOpen is still false");

            var closed = Open(element.PartName, false);
            if (closed.type != "result")
                failures.Add($"{type.Name}: is_open=false refused");
            else if (openable.IsOpen)
                failures.Add($"{type.Name}: is_open=false accepted but IsOpen is still true");
        }

        Assert.Greater(openables, 0, "сторож ослеп: ни одного IOpenable среди образцов");
        Assert.IsEmpty(failures,
            "правило проекта: что открывается в панели, открывается и по проводу. " + string.Join("; ", failures));
        EveryElementType.ClearScene();
    }

    [Test]
    public void IsOpen_OnAnElementThatIsNotOpenable_IsStillRefused()
    {
        MakeElement("BoardNotOpenable", new UnityEngine.Vector3Int(600, 400, 18));

        var resp = Open("BoardNotOpenable", true);

        Assert.AreEqual("error", resp.type,
            "доска ничего не открывает: молчаливый успех «открыли» был бы ложью");
        Assert.That(JObject.FromObject(resp.data!)["message"]!.ToString(), Does.Contain("is_open"),
            "и отказ называет поле");
    }

    [Test]
    public void IsOpenDescription_NamesTheSofaBedRule()
    {
        var field = typeof(KitchenDesigner.Core.MCP.Contract.EditOp).GetField("is_open");
        var description = ((KitchenDesigner.Core.MCP.Contract.McpParamAttribute)System.Attribute
            .GetCustomAttribute(field!, typeof(KitchenDesigner.Core.MCP.Contract.McpParamAttribute))!)
            .Description;

        Assert.That(description, Does.Contain("Sofa").IgnoreCase, "диван назван в описании параметра");
        Assert.That(description, Does.Contain("BED"), "true у дивана — кровать, и это сказано словом");
        Assert.That(description, Does.Contain("folded"), "false — сложен");
    }
}
