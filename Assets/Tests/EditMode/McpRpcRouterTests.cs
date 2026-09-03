using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using KitchenDesigner.Core.MCP;
using KitchenDesigner.Core.MCP.Contract;

/// <summary>Сам протокол MCP (JSON-RPC 2.0: initialize, ping, tools/list, tools/call)
/// раньше жил в Node — mcp-server/src/index.ts на @modelcontextprotocol/sdk. Приложение
/// говорит по нему само, и весь этот слой перееxал в McpRpcRouter. Тесты держат ровно
/// те решения, которые в Node принимал чужой SDK и которые теперь принимаем мы.
///
/// Диспетчер здесь подставной: роутер не имеет права знать, что там дальше — сцена,
/// главный поток Unity или ничего. Единственное, что он про диспетчер знает, —
/// McpRequest на входе и McpResponse на выходе.</summary>
public class McpRpcRouterTests
{
    private readonly List<McpRequest> _seen = new List<McpRequest>();

    private McpRpcRouter Router(Func<McpRequest, McpResponse>? dispatch = null) =>
        new McpRpcRouter(request =>
        {
            _seen.Add(request);
            return dispatch != null ? dispatch(request) : McpResponse.Result(request.id, new { ok = true });
        }, "9.9.9");

    private JObject Ask(string body, Func<McpRequest, McpResponse>? dispatch = null)
    {
        var (status, json) = Router(dispatch).Handle(body);
        Assert.AreEqual(200, status, "ответ на запрос с id идёт с HTTP 200: " + json);
        Assert.IsNotNull(json);
        return JObject.Parse(json!);
    }

    private static string Rpc(string method, string id = "1", string parameters = "{}") =>
        "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":" + parameters + "}";

    [SetUp]
    public void SetUp() => _seen.Clear();

    [Test]
    public void Initialize_EchoesAKnownVersion_AndFallsBackForAnUnknownOne()
    {
        var known = Ask(Rpc("initialize", "0", "{\"protocolVersion\":\"2025-03-26\"}"));
        Assert.AreEqual("2025-03-26", (string?)known["result"]!["protocolVersion"],
            "версию из списка поддерживаемых возвращаем как есть — иначе клиент считает, "
            + "что мы её не понимаем");

        var future = Ask(Rpc("initialize", "0", "{\"protocolVersion\":\"2099-01-01\"}"));
        Assert.AreEqual(McpRpcRouter.DefaultProtocolVersion, (string?)future["result"]!["protocolVersion"],
            "клиент, вышедший после нас, обязан подключиться: отказ на незнакомой версии "
            + "означает, что обновлённый агент перестал видеть кухню");

        var absent = Ask(Rpc("initialize", "0"));
        Assert.AreEqual(McpRpcRouter.DefaultProtocolVersion, (string?)absent["result"]!["protocolVersion"]);
    }

    [Test]
    public void Initialize_CarriesToolsCapability_ServerInfo_AndInstructions()
    {
        var result = Ask(Rpc("initialize", "0"))["result"]!;

        Assert.IsNotNull(result["capabilities"]!["tools"],
            "без объявленной возможности tools клиент не станет звать tools/list");
        Assert.AreEqual("unity-kitchen", (string?)result["serverInfo"]!["name"],
            "имя сервера — то же, под которым агенту предлагают его прописать");
        Assert.AreEqual("9.9.9", (string?)result["serverInfo"]!["version"],
            "версия приходит снаружи: роутер не знает про UnityEngine.Application");
        StringAssert.Contains("MILLIMETERS", (string?)result["instructions"],
            "instructions — та самая шпаргалка про метры и миллиметры, ради которой агент "
            + "перестаёт путать 600 мм с 600 м");
    }

    [Test]
    public void Notification_Returns202_AndDispatchesNothing()
    {
        var (status, body) = Router().Handle("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");

        Assert.AreEqual(202, status, "у уведомления нет id, значит и ответа на него нет");
        Assert.IsNull(body, "тело ответа на уведомление обязано быть пустым");
        CollectionAssert.IsEmpty(_seen, "уведомление не исполняется");
    }

    [Test]
    public void NotificationNamingATool_StillDispatchesNothing()
    {
        var (status, _) = Router().Handle("{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\","
            + "\"params\":{\"name\":\"delete_elements\",\"arguments\":{}}}");

        Assert.AreEqual(202, status);
        CollectionAssert.IsEmpty(_seen,
            "запрос без id — уведомление, и удалять по нему элементы нельзя, как бы method "
            + "ни выглядел");
    }

    [Test]
    public void Ping_ReturnsEmptyResult()
    {
        var result = Ask(Rpc("ping", "5"))["result"] as JObject;

        Assert.IsNotNull(result);
        CollectionAssert.IsEmpty(result!.Properties().ToList(),
            "ping протокола отвечает пустым объектом и НЕ ходит в Unity — инструмент ping "
            + "из реестра зовут через tools/call");
        CollectionAssert.IsEmpty(_seen);
    }

    [Test]
    public void ToolsList_ListsEveryRegistryTool_InRegistryOrder()
    {
        var tools = (JArray)Ask(Rpc("tools/list"))["result"]!["tools"]!;

        Assert.AreEqual(McpToolRegistry.Tools.Count, tools.Count,
            "tools/list отдаётся прямо из реестра: расхождение означает, что агент не видит "
            + "часть инструментов. Раньше за этим следил mcp-server/scripts/check-parity.mjs");
        CollectionAssert.AreEqual(
            McpToolRegistry.Tools.Select(t => t.Name).ToList(),
            tools.Select(t => (string?)t["name"]).ToList(),
            "порядок — порядок реестра");

        foreach (var tool in tools)
        {
            Assert.IsNotNull((string?)tool["description"], "инструмент без описания бесполезен агенту");
            Assert.AreEqual("object", (string?)tool["inputSchema"]!["type"],
                "клиент отвергает инструмент, у которого inputSchema.type не object: " + tool["name"]);
            Assert.IsNotNull(tool["annotations"]!["readOnlyHint"],
                "по этой подсказке клиент решает, спрашивать ли подтверждение: " + tool["name"]);
        }
    }

    [Test]
    public void ToolsCall_ForwardsRenamedArguments_ToTheDispatcher()
    {
        Ask(Rpc("tools/call", "2",
            "{\"name\":\"get_element_gaps\",\"arguments\":{\"names\":[\"Side_L\"]}}"));

        Assert.AreEqual(1, _seen.Count, "инструмент обязан доехать до диспетчера ровно один раз");
        Assert.AreEqual("get_element_gaps", _seen[0].method,
            "method для Unity — имя инструмента, а не tools/call");
        Assert.AreEqual("Side_L", (string?)_seen[0].Params!["names"]![0],
            "аргументы уезжают в params под теми именами, которых ждёт McpCommandHandler");
    }

    [Test]
    public void ToolsCall_WithoutArguments_SendsAnEmptyParamsObject()
    {
        Ask(Rpc("tools/call", "2", "{\"name\":\"ping\"}"));

        Assert.AreEqual(1, _seen.Count);
        Assert.IsNotNull(_seen[0].Params,
            "отсутствующий arguments — это пустой объект, а не null: обработчики зовут "
            + "ToObjectStrictOrDefault и на null у части инструментов падают");
    }

    [Test]
    public void ToolsCall_WrapsResultAsText_AndErrorAsIsError_NotAsRpcError()
    {
        var ok = Ask(Rpc("tools/call", "2", "{\"name\":\"ping\",\"arguments\":{}}"));
        var content = (JArray)ok["result"]!["content"]!;
        Assert.AreEqual("text", (string?)content[0]["type"]);
        StringAssert.Contains("\"ok\": true", (string?)content[0]["text"],
            "результат инструмента уезжает агенту как отформатированный JSON внутри текста");
        Assert.IsNull(ok["result"]!["isError"], "успех не помечается isError");

        var failed = Ask(Rpc("tools/call", "2", "{\"name\":\"ping\",\"arguments\":{}}"),
            request => McpResponse.Error(request.id, -1, "Element not found: Side_L"));

        Assert.IsNull(failed["error"],
            "ошибка ИНСТРУМЕНТА — не ошибка протокола: JSON-RPC error заставил бы клиент "
            + "считать сервер сломанным и оборвать сессию вместо того, чтобы показать "
            + "модели, что именно не нашлось");
        Assert.AreEqual(true, (bool?)failed["result"]!["isError"]);
        StringAssert.Contains("ERROR: Element not found: Side_L",
            (string?)failed["result"]!["content"]![0]!["text"],
            "текст ошибки Unity обязан доехать до агента дословно");
    }

    [Test]
    public void ToolsCall_TurnsADispatcherTimeout_IntoAdviceAboutTheApp()
    {
        var timedOut = Ask(Rpc("tools/call", "2", "{\"name\":\"get_scene_tree\",\"arguments\":{}}"),
            _ => throw new TimeoutException("queue"));

        var text = (string?)timedOut["result"]!["content"]![0]!["text"];
        Assert.AreEqual(true, (bool?)timedOut["result"]!["isError"]);
        StringAssert.Contains("get_scene_tree", text,
            "в сообщении о таймауте обязано быть имя инструмента — иначе непонятно, что зависло");
        StringAssert.Contains("Kitchen Designer", text,
            "агенту нужно понять, что чинить: приложение закрыто или подвисло");
    }

    [Test]
    public void ToolsCall_Guide_AnswersLocally_WithRawText()
    {
        var result = Ask(Rpc("tools/call", "3",
            "{\"name\":\"guide\",\"arguments\":{\"topic\":\"bulk\"}}"))["result"]!;

        CollectionAssert.IsEmpty(_seen, "шпаргалка не требует ни сцены, ни главного потока");
        Assert.AreEqual(McpGuideTexts.Topics["bulk"], (string?)result["content"]![0]!["text"],
            "текст отдаётся как есть, без обёртки в JSON");

        var fallback = Ask(Rpc("tools/call", "3",
            "{\"name\":\"guide\",\"arguments\":{\"topic\":\"нет такой темы\"}}"))["result"]!;
        Assert.AreEqual(McpGuideTexts.Topics[McpGuideTexts.DefaultTopic],
            (string?)fallback["content"]![0]!["text"],
            "неизвестная тема отдаёт обзорную, а не ошибку: агент угадывает имя темы, "
            + "и отказ здесь стоит ему лишнего круга");
    }

    [Test]
    public void NumericId_ComesBackAsANumber_StringIdAsAString_NullAsNull()
    {
        var numeric = Ask(Rpc("ping", "7"))["id"]!;
        Assert.AreEqual(JTokenType.Integer, numeric.Type,
            "id 7 обязан вернуться числом: McpRequest.id — строка, и наивный эхо-ответ "
            + "превратил бы его в \"7\", после чего клиент не сопоставит ответ с запросом");
        Assert.AreEqual(7, (int)numeric);

        var text = Ask(Rpc("ping", "\"a\""))["id"]!;
        Assert.AreEqual(JTokenType.String, text.Type);
        Assert.AreEqual("a", (string?)text);

        var nothing = Ask(Rpc("ping", "null"))["id"]!;
        Assert.AreEqual(JTokenType.Null, nothing.Type,
            "явный null в id — это запрос, а не уведомление, и null обязан вернуться null");
    }

    [Test]
    public void Batch_AnswersRequestsOnly_InOrder()
    {
        var (status, body) = Router().Handle(
            "[" + Rpc("ping", "1") + ",{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"},"
            + Rpc("ping", "2") + "]");

        Assert.AreEqual(200, status);
        var replies = JArray.Parse(body!);
        Assert.AreEqual(2, replies.Count, "на уведомление внутри пачки ответа нет");
        CollectionAssert.AreEqual(new[] { 1, 2 }, replies.Select(r => (int)r["id"]!).ToList(),
            "порядок ответов — порядок запросов");
    }

    [Test]
    public void BatchOfNotificationsOnly_Returns202_AndAnEmptyBatch_Is400()
    {
        var (notificationsOnly, body) = Router().Handle(
            "[{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}]");
        Assert.AreEqual(202, notificationsOnly, "отвечать нечем — пустой массив вместо 202 "
            + "клиенты читают как сломанный ответ");
        Assert.IsNull(body);

        var (empty, _) = Router().Handle("[]");
        Assert.AreEqual(400, empty, "пустая пачка — это не запрос, а испорченное сообщение");
    }

    [Test]
    public void ParseError_InvalidRequest_MethodNotFound_InvalidParams_UseTheirCodes()
    {
        var (parseStatus, parseBody) = Router().Handle("not json");
        Assert.AreEqual(200, parseStatus,
            "протокольная ошибка внутри успешно доставленного сообщения — это HTTP 200: "
            + "4xx здесь заставляет клиент считать транспорт сломанным");
        var parse = JObject.Parse(parseBody!);
        Assert.AreEqual(-32700, (int?)parse["error"]!["code"]);
        Assert.AreEqual(JTokenType.Null, parse["id"]!.Type, "id разобрать было не из чего");

        Assert.AreEqual(-32600, (int?)Ask("{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"ping\"}")["error"]!["code"],
            "чужая версия JSON-RPC — Invalid request");
        Assert.AreEqual(-32600, (int?)Ask("{\"jsonrpc\":\"2.0\",\"id\":1}")["error"]!["code"],
            "запрос без method — Invalid request");

        Assert.AreEqual(-32601, (int?)Ask(Rpc("resources/list"))["error"]!["code"],
            "ресурсы мы не поддерживаем; клиент обязан узнать это как Method not found, "
            + "а не как молчание");

        Assert.AreEqual(-32602, (int?)Ask(Rpc("tools/call", "1", "{\"name\":\"no_such_tool\"}"))["error"]!["code"]);
        Assert.AreEqual(-32602, (int?)Ask(Rpc("tools/call", "1", "{}"))["error"]!["code"]);
        Assert.AreEqual(-32602,
            (int?)Ask(Rpc("tools/call", "1", "{\"name\":\"ping\",\"arguments\":42}"))["error"]!["code"],
            "arguments не объект — ошибка параметров, а не падение роутера");
        CollectionAssert.IsEmpty(_seen, "ни один из этих запросов не имел права доехать до Unity");
    }

    [Test]
    public void RouterException_BecomesInternalError_NotADeadConnection()
    {
        var body = Ask(Rpc("tools/call", "1", "{\"name\":\"ping\",\"arguments\":{}}"),
            _ => throw new InvalidOperationException("сцена не готова"));

        Assert.AreEqual(true, (bool?)body["result"]!["isError"],
            "исключение диспетчера — это ошибка инструмента: клиент должен увидеть текст, "
            + "а не разорванное соединение");
        StringAssert.Contains("сцена не готова", (string?)body["result"]!["content"]![0]!["text"]);
    }

    /// <summary>Спецификация MCP требует отвечать 400 на незнакомый заголовок
    /// MCP-Protocol-Version. Мы этого не делаем сознательно: сервер без состояния и без
    /// сессий отдаёт одно и то же поведение всем версиям протокола, и ничего появившегося
    /// после 2025-03-26 не использует. Отказ здесь стоил бы подключения каждому клиенту,
    /// который представится версией новее нашего списка, и не купил бы ничего.</summary>
    [Test]
    public void UnknownProtocolVersionHeader_IsIgnored()
    {
        var withHeader = Ask(Rpc("ping", "1"));

        Assert.IsNull(withHeader["error"],
            "роутер вообще не читает заголовков: он получает только тело. Если этот тест "
            + "однажды покраснеет, значит в роутер завели проверку версии — и вместе с ней "
            + "отказ подключаться незнакомому клиенту");
    }
}
