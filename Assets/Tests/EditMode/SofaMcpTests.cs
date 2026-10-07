using NUnit.Framework;
using Newtonsoft.Json.Linq;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Диван-книжка на проводе MCP: фиксированная высота и раскладывание
/// тем же инструментом, что анимирует ящики. Агент должен получить отказ там, где
/// человек не получил бы строку в панели, и тот же этап в ответе, который видна на
/// сцене.</summary>
public class SofaMcpTests : McpTestFixture
{
    private SofaElement CreateSofa(string name, int? width = null, int? height = null,
        int? depth = null)
    {
        var resp = _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new
                {
                    name, type = "sofa", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f,
                    width, height, depth,
                },
            },
        }));
        Assert.AreEqual("result", resp.type, "диван создаётся через create_elements");

        var sofa = PartRegistry.GetAll().Find(e => e.PartName == name) as SofaElement;
        Assert.IsNotNull(sofa, "и появляется в реестре как SofaElement");
        return sofa!;
    }

    private McpResponse Cycle(params string[] names)
        => _handler!.Handle(MakeReq("cycle_drawer_animation", new { names }));

    private McpResponse TryCreateSofa(string name, int? height)
        => _handler!.Handle(MakeReq("create_elements", new
        {
            items = new object[]
            {
                new { name, type = "sofa", anchor_x_mm = 0f, anchor_y_mm = 0f, anchor_z_mm = 0f, height },
            },
        }));

    [Test]
    public void CreateSofa_WithAHeightOtherThanTheFixedOne_IsRefused_LikeEdit_AndNothingIsCreated()
    {
        var resp = TryCreateSofa("SofaTall", 1500);

        Assert.AreEqual("error", resp.type,
            "заказанные 1500 по высоте отклоняются: молчаливое «получите 800» было бы ложью, "
            + "как и в edit_elements, где то же самое число уже отказ");
        var message = JObject.FromObject(resp.data!)["message"]!.ToString();
        Assert.That(message, Does.Contain("height"), "отказ называет поле");
        Assert.That(message, Does.Contain(SofaLayout.OverallHeightMM.ToString()),
            "и называет фиксированное значение, чтобы агенту не пришлось гадать");
        Assert.IsNull(PartRegistry.GetAll().Find(e => e.PartName == "SofaTall"),
            "отказ целиком: диван не создан");
    }

    [Test]
    public void CreateSofa_WithTheFixedHeightItself_IsAccepted()
    {
        var sofa = CreateSofa("SofaExact", 1800, SofaLayout.OverallHeightMM, 1000);

        Assert.AreEqual(new UnityEngine.Vector3Int(1800, SofaLayout.OverallHeightMM, 1000),
            sofa.DimensionsMM,
            "высота, равная фиксированной, ничего не меняет, а длина и глубина берутся как заказаны");
    }

    [Test]
    public void CreateSofa_WithoutAHeight_GetsTheFixedHeight()
    {
        var sofa = CreateSofa("SofaNoHeight", 1800, null, 1000);

        Assert.AreEqual(SofaLayout.OverallHeightMM, sofa.DimensionsMM.y,
            "не названная высота — это фиксированная высота, а не ноль");
    }

    [Test]
    public void CreateItemHeight_Description_NamesTheSofaRule()
    {
        var field = typeof(KitchenDesigner.Core.MCP.Contract.CreateItem).GetField("height");
        var description = ((KitchenDesigner.Core.MCP.Contract.McpParamAttribute)System.Attribute
            .GetCustomAttribute(field!, typeof(KitchenDesigner.Core.MCP.Contract.McpParamAttribute))!)
            .Description;

        Assert.That(description, Does.Contain("sofa").IgnoreCase, "правило названо в описании параметра");
        Assert.That(description, Does.Contain(SofaLayout.OverallHeightMM.ToString()),
            "с числом, а не словами «фиксирована»");
        Assert.That(description, Does.Contain("rejected"),
            "и сказано, что чужое значение отклоняется, а не молча заменяется");
    }

    [Test]
    public void EditSofa_Height_IsRefused_AndNothingChanges()
    {
        var sofa = CreateSofa("SofaRefuse");

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "SofaRefuse", height = 900 } },
        }));

        Assert.AreEqual("error", resp.type,
            "правка высоты дивана отклоняется: молчаливый успех «применили 900» был бы "
            + "ложью, ведь высота всё равно вернулась бы к 800");
        Assert.That(JObject.FromObject(resp.data!)["message"]!.ToString(),
            Does.Contain("height not settable on this element"),
            "и отказ называет причину агенту: высота фиксирована");
        Assert.AreEqual(SofaLayout.OverallHeightMM, sofa.DimensionsMM.y, "высота та же");
    }

    private McpResponse SetOpen(string name, bool open)
        => _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name, is_open = open } },
        }));

    [Test]
    public void EditSofa_IsOpenTrue_GoesStraightToTheBed_WithoutAnIntermediateStage()
    {
        var sofa = CreateSofa("SofaOpenTrue");

        var resp = SetOpen("SofaOpenTrue", true);

        Assert.AreEqual("result", resp.type, "is_open диван принимает: он IOpenable");
        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage,
            "true — это кровать одним вызовом: стадия «выдвинуто» остаётся для cycle_drawer_animation");
    }

    [Test]
    public void EditSofa_IsOpenTrue_OnAnExtendedSofa_MakesItTheBed()
    {
        var sofa = CreateSofa("SofaOpenFromExtended");
        Cycle("SofaOpenFromExtended");
        Assert.AreEqual(SofaStage.Extended, sofa.UnfoldStage, "предусловие: диван выдвинут");

        SetOpen("SofaOpenFromExtended", true);

        Assert.AreEqual(SofaStage.Bed, sofa.UnfoldStage,
            "is_open:true — это всегда кровать, а не «уже открыт, делать нечего»");
    }

    [Test]
    public void EditSofa_IsOpenFalse_FoldsItFromAnyStage()
    {
        var sofa = CreateSofa("SofaOpenFalse");
        SetOpen("SofaOpenFalse", true);

        var resp = SetOpen("SofaOpenFalse", false);

        Assert.AreEqual("result", resp.type, "is_open:false тоже принимается");
        Assert.AreEqual(SofaStage.Folded, sofa.UnfoldStage, "и складывает диван одним вызовом");
    }

    [SetUp]
    public void ClearUndoHistory() => CommandStack.Clear();

    [TearDown]
    public void ClearUndoHistoryAfter() => CommandStack.Clear();

    [Test]
    public void EditSofa_EdgeRadius_IsUndoable_LikeThePanel()
    {
        var sofa = CreateSofa("SofaEdgeUndo");
        int original = sofa.EdgeRadiusMM;

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "SofaEdgeUndo", edge_radius = 25 } },
        }));
        Assert.AreEqual(25, sofa.EdgeRadiusMM, "предусловие: радиус применён");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "правка радиуса по проводу — ровно один шаг отмены, как «Применить» в панели");

        CommandStack.Undo();
        Assert.AreEqual(original, sofa.EdgeRadiusMM, "отмена возвращает прежний радиус");
        CommandStack.Redo();
        Assert.AreEqual(25, sofa.EdgeRadiusMM, "а повтор — заказанный");
    }

    [Test]
    public void EditSofa_ResizeAndTypeFieldsTogether_AreOneUndoStep()
    {
        var sofa = CreateSofa("SofaOneStep");
        var dimsBefore = sofa.DimensionsMM;
        int seatBefore = sofa.SeatHeightMM;

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "SofaOneStep", depth = 1100, seat_height = 420 } },
        }));
        Assert.AreEqual(420, sofa.SeatHeightMM, "предусловие: высота сиденья применена");
        Assert.AreEqual(1100, sofa.DimensionsMM.z, "предусловие: глубина применена");
        Assert.AreEqual(1, CommandStack.UndoCount,
            "габарит и типовое поле одним вызовом — один шаг, а не два: иначе отмена вернёт "
            + "половину правки");

        CommandStack.Undo();
        Assert.AreEqual(dimsBefore, sofa.DimensionsMM, "габарит вернулся");
        Assert.AreEqual(seatBefore, sofa.SeatHeightMM, "и высота сиденья вернулась тем же шагом");
    }

    [Test]
    public void EditSofa_Depth_IsAccepted_AndOnlyTheSeatGrows()
    {
        var sofa = CreateSofa("SofaDepth");

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "SofaDepth", depth = 1200 } },
        }));

        Assert.AreEqual("result", resp.type, "глубину править можно");
        Assert.AreEqual(1200, sofa.DimensionsMM.z, "габарит обновлён");
        Assert.AreEqual(1020, SofaLayout.SeatDepthFor(sofa.DimensionsMM.z),
            "и вся прибавка досталась сиденью: спинка осталась 180");
    }

    [Test]
    public void CycleDrawerAnimation_OnASofa_StepsTheStages_AndReportsThem()
    {
        var sofa = CreateSofa("SofaCycle");
        var stages = new[] { "Extended", "Bed", "Folded" };

        foreach (var expected in stages)
        {
            var resp = Cycle("SofaCycle");

            Assert.AreEqual("result", resp.type, "инструмент принимает диван");
            var entry = JObject.FromObject(resp.data!)["results"]![0]!;
            Assert.AreEqual(expected, entry["unfoldStage"]!.ToString(),
                "каждый вызов делает один этап и сообщает его");
        }

        Assert.AreEqual(SofaStage.Folded, sofa.UnfoldStage, "три вызова замыкают цикл");
    }

    [Test]
    public void CycleDrawerAnimation_WithASofaAndABoard_RefusesBoth_AndCyclesNothing()
    {
        var sofa = CreateSofa("SofaAtomic");
        MakeElement("BoardNotSofa", new UnityEngine.Vector3Int(600, 400, 18));

        var resp = Cycle("SofaAtomic", "BoardNotSofa");

        Assert.AreEqual("error", resp.type, "доска не ящик и не диван — вызов отклонён целиком");
        Assert.AreEqual(SofaStage.Folded, sofa.UnfoldStage,
            "и диван, названный раньше, остался сложенным: сообщение обещает «NOTHING was "
            + "cycled», а прежний цикл проверял и крутил в одном проходе");
    }

    [Test]
    public void GetElements_ForASofa_ReportsTheStageTheSeatDepthAndTheBackrest()
    {
        CreateSofa("SofaInfo");
        Cycle("SofaInfo");

        var resp = _handler!.Handle(MakeReq("get_elements", new { names = new[] { "SofaInfo" } }));

        var info = JObject.FromObject(resp.data!)["elements"]![0]!["sofa"]!;
        Assert.AreEqual("Extended", info["unfoldStage"]!.ToString(), "этап виден агенту");
        Assert.IsTrue(info["isOpen"]!.Value<bool>(), "и признак «открыт»");
        Assert.AreEqual(720, info["seatDepthMM"]!.Value<int>(), "глубина сиденья");
        Assert.AreEqual(180, info["backDepthMM"]!.Value<int>(), "толщина спинки");
        Assert.AreEqual(100, info["backrestBottomMM"]!.Value<int>(), "низ спинки над полом");
        Assert.AreEqual(700, info["backrestHeightMM"]!.Value<int>(), "высота спинки");
    }

    [Test]
    public void EditSofa_EdgeRadius_IsApplied_ClampedAndReported()
    {
        var sofa = CreateSofa("SofaEdge");

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "SofaEdge", edge_radius = 25 } },
        }));
        Assert.AreEqual("result", resp.type, "edge_radius принимается");
        Assert.AreEqual(25, sofa.EdgeRadiusMM, "радиус применён");

        _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "SofaEdge", edge_radius = 9999 } },
        }));
        Assert.AreEqual(SofaLayout.MaxEdgeRadiusMM(sofa.DimensionsMM, sofa.SeatHeightMM),
            sofa.EdgeRadiusMM, "и зажат геометрией");

        var info = JObject.FromObject(_handler!.Handle(
            MakeReq("get_elements", new { names = new[] { "SofaEdge" } })).data!)
            ["elements"]![0]!["sofa"]!;
        Assert.AreEqual(sofa.EdgeRadiusMM, info["edgeRadiusMM"]!.Value<int>(),
            "get_elements сообщает принятое значение, а не заказанное");
    }

    [Test]
    public void EditBoard_EdgeRadius_IsRefused_BecauseOnlyASofaHasIt()
    {
        MakeElement("BoardEdge", new UnityEngine.Vector3Int(600, 400, 18));

        var resp = _handler!.Handle(MakeReq("edit_elements", new
        {
            ops = new object[] { new { name = "BoardEdge", edge_radius = 10 } },
        }));

        Assert.AreEqual("error", resp.type,
            "edge_radius у доски — молчаливый успех был бы ложью: параметр есть только у дивана");
    }
}
