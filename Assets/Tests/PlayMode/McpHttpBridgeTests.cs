using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KitchenDesigner.Core;
using KitchenDesigner.Core.MCP;

/// <summary>Единственный тест, который поднимает НАСТОЯЩИЙ сокет. Всё остальное в этом
/// слое (роутер, гейт, схема) проверяется без сети, а здесь проверяется именно шов:
/// HttpListener в Unity, разбор тела, прыжок на главный поток и обратно.
///
/// Порт берётся свободный: 9337 на машине разработчика почти всегда занят запущенным
/// приложением, и жёсткое число превратило бы тест в «иногда красный».
///
/// Тест живёт в PlayMode, а не в EditMode, по необходимости: в EditMode Unity не зовёт
/// ни Awake, ни Update. Без Awake мост остался бы с _port из инициализатора поля (9337)
/// и полез бы на порт запущенного приложения; без Update очередь на главный поток не
/// осушается, и tools/call ждал бы тридцать секунд, чтобы упасть по таймауту. Звать их
/// через SendMessage нельзя — на не запущенном компоненте Unity роняет
/// «Assertion failed on expression: ShouldRunBehaviour()». Заводить публичный метод
/// «осуши очередь» ради теста значило бы проверять не тот путь, которым ходит
/// приложение.</summary>
public class McpHttpBridgeTests
{
    private GameObject? _host;
    private McpHttpBridge? _bridge;
    private int _port;

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private void StartBridgeOn(int port)
    {
        McpBridgeStatus.TestPort = port;
        _port = port;
        _host = new GameObject("McpHttpBridgeTests");
        _bridge = _host.AddComponent<McpHttpBridge>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        _host = null;
        _bridge = null;
        McpBridgeStatus.TestPort = null;
        McpBridgeStatus.Forget();
    }

    private IEnumerator PostAndPump(string body, Action<HttpStatusCode, string> onDone)
    {
        HttpStatusCode? status = null;
        var text = string.Empty;
        Exception? failure = null;

        var caller = new Thread(() =>
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"http://127.0.0.1:{_port}/mcp");
                request.Method = "POST";
                request.ContentType = "application/json";
                var payload = Encoding.UTF8.GetBytes(body);
                request.ContentLength = payload.Length;
                using (var stream = request.GetRequestStream()) stream.Write(payload, 0, payload.Length);
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()!, Encoding.UTF8))
                {
                    status = response.StatusCode;
                    text = reader.ReadToEnd();
                }
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse http)
            {
                status = http.StatusCode;
                using (var reader = new StreamReader(http.GetResponseStream()!, Encoding.UTF8))
                    text = reader.ReadToEnd();
            }
            catch (Exception ex) { failure = ex; }
        });
        caller.IsBackground = true;
        caller.Start();

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (caller.IsAlive && DateTime.UtcNow < deadline)
            yield return null;

        if (failure != null) throw failure;
        Assert.IsNotNull(status, "ответа за 15 секунд не пришло: мост не поднялся или очередь "
            + "главного потока не осушается");
        onDone(status!.Value, text);
    }

    [UnityTest]
    public IEnumerator Post_Initialize_ThenToolsCall_Ping_RoundTripsOverRealHttp()
    {
        StartBridgeOn(FreePort());
        yield return null;
        Assert.IsTrue(_bridge!.IsRunning,
            "HttpListener в Unity-Mono — управляемая реализация: ни http.sys, ни netsh urlacl, "
            + "ни прав администратора. Если это упало, установка per-user без UAC остаётся "
            + "без MCP вовсе");
        Assert.IsTrue(McpBridgeStatus.Running, "вкладка настроек читает состояние отсюда");

        var initialize = string.Empty;
        yield return PostAndPump(
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}",
            (status, text) => { Assert.AreEqual(HttpStatusCode.OK, status); initialize = text; });
        StringAssert.Contains("\"name\":\"unity-kitchen\"", initialize,
            "клиент читает имя сервера из serverInfo сразу после initialize");
        StringAssert.Contains("\"id\":1,", initialize,
            "id обязан вернуться числом: строка \"1\" не сопоставится с запросом");

        var called = string.Empty;
        yield return PostAndPump(
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\","
            + "\"params\":{\"name\":\"ping\",\"arguments\":{}}}",
            (status, text) => { Assert.AreEqual(HttpStatusCode.OK, status); called = text; });

        StringAssert.Contains("status", called,
            "инструмент исполняется на главном потоке Unity: этот ответ доказывает, что "
            + "прыжок из потока слушателя и обратно работает");
        StringAssert.Contains("ok", called);
        StringAssert.DoesNotContain("isError", called,
            "ping обязан ответить успехом, а не текстом ошибки в обёртке");
    }

    [UnityTest]
    public IEnumerator Get_Is405_AndAForeignOriginIs403()
    {
        StartBridgeOn(FreePort());
        yield return null;

        var get = (HttpWebRequest)WebRequest.Create($"http://127.0.0.1:{_port}/mcp");
        get.Method = "GET";
        Assert.AreEqual(HttpStatusCode.MethodNotAllowed, StatusOf(get),
            "SSE-поток мы не открываем; спецификация разрешает 405");

        var foreign = (HttpWebRequest)WebRequest.Create($"http://127.0.0.1:{_port}/mcp");
        foreign.Method = "POST";
        foreign.Headers["Origin"] = "https://evil.example";
        foreign.ContentLength = 2;
        using (var stream = foreign.GetRequestStream()) stream.Write(Encoding.UTF8.GetBytes("{}"), 0, 2);
        Assert.AreEqual(HttpStatusCode.Forbidden, StatusOf(foreign),
            "страница в браузере не имеет права править открытый проект");

        yield return null;
    }

    [UnityTest]
    public IEnumerator PortAlreadyTaken_ReportsStoppedInsteadOfThrowing()
    {
        var port = FreePort();
        var squatter = new HttpListener();
        squatter.Prefixes.Add($"http://127.0.0.1:{port}/");
        squatter.Start();

        try
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Cannot listen on port"));
            StartBridgeOn(port);
            yield return null;

            Assert.IsFalse(_bridge!.IsRunning,
                "второй экземпляр приложения не должен падать — он просто остаётся без моста");
            Assert.IsFalse(McpBridgeStatus.Running,
                "вкладка обязана показать «мост остановлен», а не соврать про рабочий порт");
        }
        finally
        {
            squatter.Close();
        }

        yield return null;
    }

    private static HttpStatusCode StatusOf(HttpWebRequest request)
    {
        try
        {
            using (var response = (HttpWebResponse)request.GetResponse())
                return response.StatusCode;
        }
        catch (WebException ex) when (ex.Response is HttpWebResponse http)
        {
            var code = http.StatusCode;
            http.Close();
            return code;
        }
    }

    /// <summary>Выключение обязано быть ОГРАНИЧЕННЫМ по времени. Приложение
    /// закрывается на глазах у человека, и мост, который «когда-нибудь» отпустит
    /// поток слушателя, читается как зависший конструктор: окно пропало, процесс
    /// висит. Здесь измеряется само выключение — оно не имеет права занимать
    /// дольше отведённого ожидания, даже если слушатель занят.</summary>
    [UnityTest]
    public IEnumerator StopBridge_ReturnsWithinItsShutdownBudget()
    {
        StartBridgeOn(FreePort());
        yield return null;
        Assume.That(_bridge!.IsRunning, Is.True, "мост не поднялся — мерить нечего");

        var started = DateTime.UtcNow;
        _bridge.StopBridge();
        var spent = (DateTime.UtcNow - started).TotalMilliseconds;

        Assert.IsFalse(_bridge.IsRunning, "после остановки мост считает себя выключенным");
        Assert.Less(spent, McpHttpBridge.ShutdownWaitMs * 2,
            $"остановка заняла {spent:F0} мс при бюджете {McpHttpBridge.ShutdownWaitMs} мс — "
            + "именно так выглядит «просто запустил и виснет» при закрытии окна");
    }

    /// <summary>Порт обязан освободиться СРАЗУ, а не «когда поток догорит». Если
    /// слушатель пережил остановку, следующий запуск молча упадёт на «адрес занят»,
    /// и мост будет числиться выключенным, продолжая держать сокет.</summary>
    [UnityTest]
    public IEnumerator StopBridge_ReleasesThePort_SoTheBridgeCanStartAgain()
    {
        int port = FreePort();
        StartBridgeOn(port);
        yield return null;
        Assume.That(_bridge!.IsRunning, Is.True);

        _bridge.StopBridge();

        var again = new HttpListener();
        again.Prefixes.Add($"http://127.0.0.1:{port}/");
        Assert.DoesNotThrow(() => again.Start(),
            "порт остался занят мёртвым слушателем — повторный запуск приложения не поднимет мост");
        again.Close();
    }
}
