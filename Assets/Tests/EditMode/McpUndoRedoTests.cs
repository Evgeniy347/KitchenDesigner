using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>У слабой модели не было «назад»: откат значил «удалить и поставить заново», а это новая
/// петля колебаний. undo/redo идут поверх CommandStack, и один шаг — это один мутирующий вызов,
/// какой бы инструмент его ни сделал.</summary>
public class McpUndoRedoTests : McpTestFixture
{
    [SetUp]
    public void Setup() => CommandStack.Clear();

    [TearDown]
    public void Teardown() => CommandStack.Clear();

    private bool Exists(string name) => PartRegistry.GetAll().Any(e => e != null && e.PartName == name);

    private McpResponse CreateBoard(string name, float x, float y = 0f, float z = 0f, int width = 600) =>
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[] { new { name, width, height = 400, depth = 18, anchor_x_mm = x, anchor_y_mm = y, anchor_z_mm = z } }
        }));

    private McpResponse Undo(int? steps = null) =>
        _handler!.Handle(MakeReq("undo", steps.HasValue ? (object)new { steps = steps.Value } : new { }));

    private McpResponse Redo(int? steps = null) =>
        _handler!.Handle(MakeReq("redo", steps.HasValue ? (object)new { steps = steps.Value } : new { }));

    [Test]
    public void CreateThenUndo_TheElementIsGone_AndTheViolationsItCausedGoWithIt()
    {
        MakeSupportingFloor();
        MakeElement("Existing", new Vector3Int(1000, 1000, 1000), new Vector3(0f, 0.5f, 0f));
        CommandStack.Clear();
        var created = CreateBoard("Clash", 0f);
        CollectionAssert.AreEquivalent(new[] { "Clash", "Existing" },
            ReplyOf(created)["sceneViolationDelta"]!["added"]!.ToObject<string[]>(), "предусловие: создание сломало сцену");

        var undone = Undo();

        Assert.AreEqual("result", undone.type, ReplyOf(undone).ToString());
        Assert.IsFalse(Exists("Clash"), "после undo созданной детали нет — модели не нужно её удалять самой");
        var reply = ReplyOf(undone);
        Assert.AreEqual(1, reply["doneCount"]!.Value<int>());
        StringAssert.Contains("create_elements", reply["steps"]![0]!.Value<string>(), "шаг назван по вызову, который его сделал");
        CollectionAssert.AreEquivalent(new[] { "Clash", "Existing" }, reply["sceneViolationDelta"]!["removed"]!.ToObject<string[]>(),
            "дельта undo показывает, что откат починил: пересечение исчезло");
        Assert.AreEqual(0, ReplyOf(_handler!.Handle(MakeReq("get_violations", new { })))["count"]!.Value<int>(),
            "и нарушений в сцене действительно нет");
        Assert.AreEqual(1, reply["redoAvailableCount"]!.Value<int>());
    }

    [Test]
    public void Redo_BringsTheUndoneElementBack_WithTheSameViolations()
    {
        MakeSupportingFloor();
        MakeElement("Existing", new Vector3Int(1000, 1000, 1000), new Vector3(0f, 0.5f, 0f));
        CommandStack.Clear();
        CreateBoard("Clash", 0f);
        Undo();

        var redone = Redo();

        Assert.IsTrue(Exists("Clash"), "redo вернул деталь");
        CollectionAssert.AreEquivalent(new[] { "Clash", "Existing" },
            ReplyOf(redone)["sceneViolationDelta"]!["added"]!.ToObject<string[]>());
        Assert.AreEqual(1, ReplyOf(redone)["doneCount"]!.Value<int>());
        Assert.IsNull(ReplyOf(redone)["steps"], "описаний шагов redo не отдаёт: стек хранит их только для отмены");
    }

    [Test]
    public void OneCreateCallWithThreeItems_IsOneStep_AndOneUndoRemovesAllThree()
    {
        _handler!.Handle(MakeReq("create_elements", new
        {
            items = new[]
            {
                new { name = "S1", width = 300, height = 18, depth = 300, anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f },
                new { name = "S2", width = 300, height = 18, depth = 300, anchor_x_mm = 400f, anchor_y_mm = 0f, anchor_z_mm = 0f },
                new { name = "S3", width = 300, height = 18, depth = 300, anchor_x_mm = 800f, anchor_y_mm = 0f, anchor_z_mm = 0f },
            }
        }));
        Assert.AreEqual(1, CommandStack.UndoCount, "батч — один шаг истории");

        Undo();

        Assert.IsFalse(Exists("S1") || Exists("S2") || Exists("S3"), "один undo снимает весь батч");
    }

    [Test]
    public void EditThenUndo_PutsThePartExactlyWhereItWas()
    {
        var part = MakeElement("Mover", new Vector3Int(600, 400, 18), new Vector3(1f, 0.2f, 2f));
        var before = part.transform.position;
        _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "Mover", anchor_x_mm = 5000f, rot_y = 90f } } }));
        Assume.That(part.transform.position, Is.Not.EqualTo(before), "предусловие: правка сдвинула деталь");

        Undo();

        Assert.AreEqual(before.x, part.transform.position.x, 1e-5f);
        Assert.AreEqual(before.y, part.transform.position.y, 1e-5f);
        Assert.AreEqual(before.z, part.transform.position.z, 1e-5f);
        Assert.AreEqual(0f, part.transform.eulerAngles.y, 1e-3f, "и поворот вернулся");
    }

    [Test]
    public void StepsTwo_TakesBackTwoCalls_AndTheReplyListsThemNewestFirst()
    {
        var part = MakeElement("Mover", new Vector3Int(600, 400, 18), Vector3.zero);
        _handler!.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "Mover", anchor_x_mm = 1000f } } }));
        _handler.Handle(MakeReq("edit_elements", new { ops = new[] { new { name = "Mover", anchor_x_mm = 2000f } } }));

        var undone = Undo(2);

        Assert.AreEqual(2, ReplyOf(undone)["doneCount"]!.Value<int>());
        Assert.AreEqual(2, ((JArray)ReplyOf(undone)["steps"]!).Count);
        Assert.AreEqual(0f, part.transform.position.x, 1e-5f, "обе правки откатились");
    }

    [Test]
    public void UndoWithNothingToUndo_IsNotAnError_ItReportsZeroDone()
    {
        var resp = Undo();

        Assert.AreEqual("result", resp.type, "пустая история — не ошибка, а «откатывать нечего»");
        Assert.AreEqual(0, ReplyOf(resp)["doneCount"]!.Value<int>());
        Assert.AreEqual(0, ReplyOf(resp)["undoAvailableCount"]!.Value<int>());
    }

    [Test]
    public void AskingForMoreStepsThanTheHistoryHolds_UndoesWhatThereIs_AndStops()
    {
        CreateBoard("A", 0f);
        CreateBoard("B", 1000f);

        var resp = Undo(McpHistorySteps.Max);

        Assert.AreEqual(2, ReplyOf(resp)["doneCount"]!.Value<int>());
        Assert.IsFalse(Exists("A") || Exists("B"));
    }

    [Test]
    public void ANewMutationAfterUndo_ClearsTheRedoHistory()
    {
        CreateBoard("A", 0f);
        Undo();
        CreateBoard("B", 1000f);

        var redo = Redo();

        Assert.AreEqual(0, ReplyOf(redo)["doneCount"]!.Value<int>(),
            "после новой правки старую ветку вернуть нельзя: redo ничего не делает и честно говорит об этом");
        Assert.IsFalse(Exists("A"));
    }

    [Test]
    public void BothHistoryTools_AreOnTheSurface_AsWritesNotDestructiveCalls()
    {
        foreach (var name in new[] { "undo", "redo" })
        {
            var tool = KitchenDesigner.Core.MCP.Contract.McpToolRegistry.Tools.FirstOrDefault(t => t.Name == name);
            Assert.IsNotNull(tool, $"{name}: инструмента нет в реестре");
            Assert.AreEqual(KitchenDesigner.Core.MCP.Contract.McpToolKind.Write, tool!.Kind,
                $"{name} возвращает состояние, но не стирает данные: пометка Destructive заставила бы агента спрашивать разрешение на откат");
        }
    }
}
