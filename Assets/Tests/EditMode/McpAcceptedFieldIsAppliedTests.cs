using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Вопрос «можно ли это поле» и действие «применить его» когда-то были двумя
/// лестницами по типам над одной моделью (`drawer_system` стоял в одной и пропал из
/// другой). Теперь обе спрашивают один интерфейс, а этот сторож держит их вместе по
/// ПОВЕДЕНИЮ: если правила приняли поле для элемента, правка обязана что-то изменить в
/// слепке свойств. Принято, но не применено — именно тот молчаливый успех, которого
/// не должно быть.</summary>
public class McpAcceptedFieldIsAppliedTests : McpTestFixture
{
    private static readonly (string field, object op)[] Fields =
    {
        ("corner_radius", new { corner_radius = 7 }),
        ("seat_height", new { seat_height = 333 }),
        ("edge_radius", new { edge_radius = 7 }),
    };

    [Test]
    public void EveryFieldTheRulesAccept_ChangesSomethingOnThatElement()
    {
        var failures = new List<string>();
        var accepted = new Dictionary<string, int>();
        foreach (var (type, _) in EveryElementType.Makers)
        {
            foreach (var (field, fields) in Fields)
            {
                EveryElementType.ClearScene();
                var element = EveryElementType.Spawn(type, "AcceptProbe");
                _spawned.Add(element.gameObject);
                if (!PartRegistry.GetAll().Contains(element)) PartRegistry.Register(element);

                var op = JObject.FromObject(fields);
                op["name"] = element.PartName;
                var rejected = EditFieldRules.Reject(op.ToObject<EditOp>()!, element, _ => null);
                if (rejected.Contains(field)) continue;

                accepted[field] = accepted.TryGetValue(field, out int n) ? n + 1 : 1;
                var before = UndoableProperties.Capture(element);
                var resp = _handler!.Handle(MakeReq("edit_elements", new { ops = new object[] { op } }));
                if (resp.type != "result") { failures.Add($"{type.Name}.{field}: accepted by the rules, refused by the handler"); continue; }
                if (UndoableProperties.Changed(before, UndoableProperties.Capture(element)).Count == 0)
                    failures.Add($"{type.Name}.{field}: accepted, but nothing changed");
            }
        }

        foreach (var (field, _) in Fields)
            Assert.Greater(accepted.TryGetValue(field, out int count) ? count : 0, 0,
                "сторож ослеп: ни один тип не принял поле " + field);
        Assert.IsEmpty(failures, string.Join("; ", failures));
        EveryElementType.ClearScene();
    }
}
