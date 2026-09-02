using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>
/// Репро дефекта: convert_elements через MCP обходил единственный гейт конвертации.
///
/// В интерфейсе «во что можно превратить» решает ElementTypeConverter.GroupOf, и
/// столам, стульям, диванам, кроватям, пуфам, опорам, мойкам, полам, лампам,
/// панелям и стенам он конвертацию не предлагает. McpCommandHandler звал
/// ElementConverter.Convert напрямую, а сам Convert отказывал только ящику, окну,
/// двери и трём приборам. В итоге convert_elements {name:"Table1",
/// target:"facade"} УНИЧТОЖАЛ TableElement (DestroyImmediate) и вешал вместо него
/// FacadeElement, отвечая ok:true — потеря пользовательских данных, о которой
/// агенту на том конце никто не сообщал.
///
/// Это ровно та пара из CONVENTIONS.md → «Class responsibility (SRP)»: «можно ли?»
/// и «делаю» ветвились по типу в ДВУХ местах и разъехались. Теперь ветвление одно
/// — ElementConverter.CanConvert, — и спрашивают его оба вызывающих.
///
/// Тесты ходят пользовательским путём: через McpCommandHandler.Handle, а не через
/// ElementConverter.Convert. Проверка на самом Convert была бы зелёной и при
/// полностью потерянной проверке в обработчике.
/// </summary>
public class McpConvertElementsReproTests
{
    private McpCommandHandler? _handler;
    private ProjectLoadStateGuard? _globals;

    [SetUp]
    public void SetUp()
    {
        _globals = ProjectLoadStateGuard.Capture();
        _handler = new McpCommandHandler();
        EveryElementType.ClearScene();
    }

    [TearDown]
    public void TearDown()
    {
        EveryElementType.ClearScene();
        CommandStack.Clear();
        GroupManager.Clear();
        _globals?.Restore();
    }

    private McpRequest Req(string method, object data) => new McpRequest
    {
        id = "test",
        method = method,
        Params = JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(data))
    };

    private static string ErrorMessage(McpResponse resp) =>
        JObject.FromObject(resp.data!)["message"]!.Value<string>()!;

    private McpResponse Ask(string name, string target) =>
        _handler!.Handle(Req("convert_elements", new { ops = new[] { new { name, target } } }));

    /// <summary>Репро в одну строку: стол просят стать фасадом. Раньше он им и
    /// становился — компонент TableElement уничтожался, а ответ был ok.</summary>
    [Test]
    public void ConvertElements_Table_IsRefused_InsteadOfSilentlyDestroyingTheElement()
    {
        var table = EveryElementType.Spawn(typeof(TableElement), "Table1");

        var resp = Ask("Table1", "facade");

        Assert.AreEqual("error", resp.type,
            "конвертация стола интерфейсом не предлагается и обязана быть отклонена ЯВНО: "
            + "тихий ok:true скрывал уничтожение элемента");
        StringAssert.Contains("Table1", ErrorMessage(resp),
            "агент обязан узнать, КАКОЙ элемент отклонён");
        Assert.IsTrue(table != null && table.GetComponent<TableElement>() != null,
            "стол обязан пережить отказ: раньше на этом месте оставался FacadeElement");
    }

    /// <summary>У стены KitchenElement и Wall живут на одном объекте. Convert
    /// уничтожал первый и оставлял второй — стена без элемента.</summary>
    [Test]
    public void ConvertElements_Wall_IsRefused_SoItsWallComponentIsNotLeftOrphaned()
    {
        var go = ElementFactory.CreateWall(new Vector3Int(100, 2700, 3000), "Wall1", Vector3.zero);

        var resp = Ask("Wall1", "facade");

        Assert.AreEqual("error", resp.type, "стена не конвертируется — ни в интерфейсе, ни через MCP");
        Assert.IsNotNull(go.GetComponent<Wall>(), "предусловие: это действительно стена");
        Assert.IsNotNull(go.GetComponent<KitchenElement>(),
            "у стены обязан остаться её KitchenElement: без него Wall висит сиротой");
    }

    /// <summary>Положительный контроль на ТОЙ ЖЕ упряжи. Отказ, который отказывает
    /// всем, «проходит» проверку выше и ломает работающую функцию — а гейт, умеющий
    /// только отказывать, обязан иметь куда упасть (CONVENTIONS.md → «A gate that
    /// can only refuse must have somewhere to fall back to»).</summary>
    [Test]
    public void ConvertElements_PlainBoard_StillBecomesAFacade()
    {
        EveryElementType.Spawn(typeof(KitchenElement), "Board1");

        var resp = Ask("Board1", "facade");

        Assert.AreEqual("result", resp.type,
            resp.type == "error" ? ErrorMessage(resp) : "доска в фасад конвертируется и обязана продолжать");
        var converted = PartRegistry.GetAll().Find(e => e != null && e.PartName == "Board1");
        Assert.IsNotNull(converted, "элемент обязан остаться в сцене под своим именем");
        Assert.IsNotNull(converted!.GetComponent<FacadeElement>(), "доска обязана стать фасадом");
    }

    /// <summary>Парность реестра ПО ПОВЕДЕНИЮ, прогнанная по каждому типу элемента:
    /// то, чего интерфейс не предлагает, путь MCP обязан ОТКЛОНИТЬ, а элемент —
    /// уцелеть. Поимённая проверка стола не помешала бы следующему типу повторить
    /// пропуск, а набор экземпляров сверяется рефлексией по сборке
    /// (EveryElementType), поэтому новый тип сначала уронит сам список.</summary>
    [Test]
    public void ConvertElements_EveryTypeTheUiDoesNotOffer_IsRefusedAndSurvives()
    {
        var wrong = new List<string>();
        int refused = 0;
        int accepted = 0;

        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            var el = EveryElementType.Spawn(type, "Conv" + type.Name);
            bool offered = ElementConverter.CanConvert(el);

            var resp = Ask("Conv" + type.Name, "facade");
            var survivor = PartRegistry.GetAll().Find(e => e != null && e.PartName == "Conv" + type.Name);

            if (offered) { accepted++; continue; }

            refused++;
            if (resp.type != "error")
                wrong.Add($"{type.Name}: ответ «{resp.type}» вместо отказа");
            if (survivor == null || survivor.GetType() != type)
                wrong.Add($"{type.Name}: после отказа в сцене "
                          + (survivor == null ? "ничего нет" : survivor.GetType().Name));
        }

        Assert.Greater(refused, 0, "ни один тип не отклонён — проверка не может провалиться");
        Assert.Greater(accepted, 0,
            "ни один тип не принят — гейт закрыл вообще всё, и отказ ниже ничего не доказывает");
        Assert.IsEmpty(wrong,
            "путь MCP не спросил тот же гейт, что интерфейс: конвертация типа, которого продукт "
            + "конвертировать не умеет, уничтожает элемент и отвечает успехом — "
            + string.Join("; ", wrong));
    }

    /// <summary>Гейт и текст инструмента — двойники, и они расходятся молча
    /// (CONVENTIONS.md → «A capability table has a twin in the contract»). Описание
    /// convert_elements обещает ровно четыре исходных типа; реализация обещала
    /// больше и молчала об этом.</summary>
    [Test]
    public void TheGate_AcceptsExactlyTheFourTypes_TheToolDescriptionPromises()
    {
        var accepted = new List<string>();
        foreach (var type in EveryElementType.Declared())
        {
            EveryElementType.ClearScene();
            if (ElementConverter.CanConvert(EveryElementType.Spawn(type, "Gate" + type.Name)))
                accepted.Add(type.Name);
        }

        CollectionAssert.AreEquivalent(
            new[]
            {
                nameof(KitchenElement), nameof(FacadeElement),
                nameof(AssembledFacadeElement), nameof(RadialShelfElement),
            },
            accepted,
            "описание инструмента говорит «board(part) <-> facade <-> assembled facade <-> radial "
            + "shelf»; реализация обязана принимать ровно это. Расхождение в любую сторону — "
            + "дефект: шире — тихое разрушение, уже — недоступная функция");
    }
}
