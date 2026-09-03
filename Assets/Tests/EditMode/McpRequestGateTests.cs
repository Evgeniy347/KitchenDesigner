using NUnit.Framework;
using KitchenDesigner.Core.MCP;

/// <summary>Всё, что решается ДО разбора тела, и единственное место, где MCP-эндпоинт
/// отвечает кодами 4xx. Сокета здесь нет: гейт получает метод, путь и три заголовка,
/// поэтому его можно прогнать целиком без сети.
///
/// Зачем он вообще. Старый построчный TCP-мост слушал тот же порт и не видел ни Host,
/// ни Origin. Страница в браузере могла сделать fetch с mode:"no-cors" и
/// Content-Type: text/plain — предполётного запроса при этом нет, в сокет приезжает
/// HTTP-текст, мост резал его по \n, на строке запроса и заголовках отвечал -32700 и
/// ПРОДОЛЖАЛ читать, а строку тела исполнял на главном потоке. Проверки Host и Origin
/// ниже — то, чего там не было.</summary>
public class McpRequestGateTests
{
    private const int Port = 9337;

    private static (int status, string? refusal) Inspect(
        string method = "POST", string path = "/mcp", string? host = "127.0.0.1:9337",
        string? origin = null, long length = 10)
        => McpRequestGate.Inspect(method, path, host, origin, length, Port);

    [Test]
    public void APostToTheMcpPath_FromLoopback_Passes()
    {
        Assert.AreEqual(0, Inspect().status,
            "обычный запрос агента обязан проходить — иначе тесты ниже проверяют закрытую дверь");
        Assert.AreEqual(0, Inspect(path: "/mcp/").status,
            "завершающий слэш пишут руками и в конфигах клиентов; отказ по нему выглядит "
            + "как «сервер не работает»");
    }

    [Test]
    public void AnyOtherPath_Is404()
    {
        Assert.AreEqual(404, Inspect(path: "/").status);
        Assert.AreEqual(404, Inspect(path: "/other").status,
            "порт занимает MCP целиком, но отвечать на чужие пути он не обязан");
    }

    [Test]
    public void GetAndDelete_Are405_BecauseThereIsNoSseAndNoSession()
    {
        Assert.AreEqual(405, Inspect(method: "GET").status,
            "GET /mcp — это подписка на SSE-поток; мы его не открываем, и спецификация "
            + "разрешает ответить 405");
        Assert.AreEqual(405, Inspect(method: "DELETE").status,
            "DELETE закрывает сессию, а сессий у нас нет: сервер без состояния");
        Assert.AreEqual(405, Inspect(method: "PUT").status);
    }

    [Test]
    public void AForeignHostHeader_Is403_EvenOnALoopbackSocket()
    {
        Assert.AreEqual(403, Inspect(host: "evil.example").status,
            "запрос с чужим Host пришёл через перепривязку DNS: сокет локальный, "
            + "а страница — чужая");
        Assert.AreEqual(403, Inspect(host: null).status, "Host обязателен");
        Assert.AreEqual(0, Inspect(host: "LOCALHOST:9337").status,
            "регистр в Host значения не имеет");
        Assert.AreEqual(0, Inspect(host: "[::1]:9337").status,
            "IPv6-петля — тот же компьютер");
    }

    [Test]
    public void MissingOrigin_Passes_BecauseCliClientsSendNone()
    {
        Assert.AreEqual(0, Inspect(origin: null).status,
            "curl, Claude Code и Codex Origin не шлют вовсе — требовать его значит "
            + "не пустить ни одного настоящего клиента");
    }

    [Test]
    public void ForeignOrigin_IsRefusedEvenWhenHostIsLocal()
    {
        Assert.AreEqual(403, Inspect(origin: "https://evil.example").status,
            "именно так выглядит запрос со страницы в браузере: сокет наш, Host наш, "
            + "а страница чужая");
        Assert.AreEqual(403, Inspect(origin: "http://127.0.0.1:8080").status,
            "чужой порт на том же компьютере — другой сервер, и его страница не имеет "
            + "права править открытый проект");
        Assert.AreEqual(403, Inspect(origin: "https://127.0.0.1:9337").status,
            "https на нашем порту мы не слушаем: такой Origin подделан");
        Assert.AreEqual(0, Inspect(origin: "http://localhost:9337").status,
            "своя же страница по localhost — свой");
    }

    [Test]
    public void ABodyOverFourMegabytes_Is413()
    {
        Assert.AreEqual(0, Inspect(length: McpRequestGate.MaxBodyBytes).status,
            "ровно предел ещё проходит");
        Assert.AreEqual(413, Inspect(length: McpRequestGate.MaxBodyBytes + 1).status,
            "тело читается в память целиком: без предела любой процесс на машине кладёт "
            + "приложение одним запросом");
    }

    [Test]
    public void WithoutAnExpectedToken_TheAuthorizationHeaderIsNotRead()
    {
        Assert.AreEqual(0, McpRequestGate.Inspect(
            "POST", "/mcp", "127.0.0.1:9337", null, 10, Port, authorization: null).status,
            "авторизации в этом этапе нет: сервер слушает только петлю, и требовать токен "
            + "значит заставить пользователя его куда-то вписывать");

        Assert.AreEqual(401, McpRequestGate.Inspect(
            "POST", "/mcp", "127.0.0.1:9337", null, 10, Port,
            authorization: null, expectedToken: "s3cret").status,
            "точка расширения обязана работать в тот день, когда токен понадобится, "
            + "иначе она — мёртвый параметр");
        Assert.AreEqual(0, McpRequestGate.Inspect(
            "POST", "/mcp", "127.0.0.1:9337", null, 10, Port,
            authorization: "Bearer s3cret", expectedToken: "s3cret").status);
    }

    [Test]
    public void EveryRefusal_SaysWhy()
    {
        foreach (var (status, refusal) in new[]
                 {
                     Inspect(path: "/other"), Inspect(method: "GET"), Inspect(host: "evil.example"),
                     Inspect(origin: "https://evil.example"),
                     Inspect(length: McpRequestGate.MaxBodyBytes + 1)
                 })
        {
            Assert.AreNotEqual(0, status);
            Assert.IsNotNull(refusal,
                "молчаливый 403 неотличим от «приложение закрыто»; читать этот текст будет "
                + "человек, у которого агент не подключился");
        }
    }
}
