
using System;
using NUnit.Framework;
using KitchenDesigner.Core;

public class McpBridgeStatusTests
{
    private int? _previousTestPort;

    [SetUp]
    public void SetUp()
    {
        _previousTestPort = McpBridgeStatus.TestPort;
        McpBridgeStatus.TestPort = null;
    }

    [TearDown]
    public void TearDown()
    {
        McpBridgeStatus.TestPort = _previousTestPort;
    }

    [Test]
    public void ResolvePort_ReturnsDefault_WhenNoOverride()
    {
        Assert.AreEqual(McpBridgeStatus.DefaultPort, McpBridgeStatus.ResolvePort());
    }

    [Test]
    public void ResolvePort_ReturnsTestPort_WhenStaticOverrideSet()
    {
        McpBridgeStatus.TestPort = 19337;
        Assert.AreEqual(19337, McpBridgeStatus.ResolvePort());
    }

    [Test]
    public void ResolvePort_TestPortTakesPrecedenceOverFallback()
    {
        McpBridgeStatus.TestPort = 12345;
        Assert.AreEqual(12345, McpBridgeStatus.ResolvePort(9999));
    }

    [Test]
    public void ResolvePort_ReturnsFallback_WhenNoOverrides()
    {
        Assert.AreEqual(5555, McpBridgeStatus.ResolvePort(5555));
    }

    [Test]
    public void ResolvePort_ReadsTheEnvironmentVariable_WhenNoTestOverride()
    {
        var previous = Environment.GetEnvironmentVariable(PortVariable);
        try
        {
            Environment.SetEnvironmentVariable(PortVariable, "18081");
            Assert.AreEqual(18081, McpBridgeStatus.ResolvePort(5555),
                "UNITY_MCP_PORT перебивает значение по умолчанию: этим переменным "
                + "окружения мост запускают рядом с уже занятым портом");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PortVariable, previous);
        }
    }

    [Test]
    public void ResolvePort_TestOverrideBeatsTheEnvironmentVariable()
    {
        var previous = Environment.GetEnvironmentVariable(PortVariable);
        try
        {
            Environment.SetEnvironmentVariable(PortVariable, "18081");
            McpBridgeStatus.TestPort = 17000;

            Assert.AreEqual(17000, McpBridgeStatus.ResolvePort(5555),
                "приоритет: TestPort выше UNITY_MCP_PORT. Иначе переменная окружения "
                + "машины, на которой идёт прогон, молча уводит тест на чужой порт");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PortVariable, previous);
        }
    }

    [Test]
    public void Url_UsesTheResolvedPort_AndTheMcpPath()
    {
        McpBridgeStatus.Report(9500, true);
        try
        {
            Assert.AreEqual("http://127.0.0.1:9500/mcp", McpBridgeStatus.Url,
                "эту строку вкладка настроек кладёт пользователю в буфер, и он отдаёт её "
                + "агенту дословно. Слой UI не имеет права ссылаться на MCP "
                + "(LayerDependencyDirectionTests, UI->MCP = 0), поэтому URL собирается "
                + "здесь, в Infrastructure, и ровно один раз");
            StringAssert.DoesNotContain("localhost", McpBridgeStatus.Url,
                "в префикс слушателя localhost не добавлен: на части машин он резолвится "
                + "в ::1, и Mono открыл бы второй сокет. Адрес для агента обязан быть тем, "
                + "который мы действительно слушаем");
        }
        finally
        {
            McpBridgeStatus.Forget();
        }
    }

    [Test]
    public void TheLegacyTcpPort_IsNotTheHttpPort()
    {
        Assert.AreNotEqual(McpBridgeStatus.DefaultPort, McpBridgeStatus.LegacyTcpPort,
            "оба моста поднимаются одним Bootstrap; совпадение портов означает, что второй "
            + "молча не стартует, и виноватым выглядит тот, кто запустился первым");
    }

    private const string PortVariable = McpBridgeStatus.PortVariable;
}

