
using System;
using NUnit.Framework;
using UnityEngine;
using KitchenDesigner.Core.MCP;

public class UnityTcpBridgeTests
{
    private int? _previousTestPort;

    [SetUp]
    public void SetUp()
    {
        _previousTestPort = UnityTcpBridge.TestPort;
        UnityTcpBridge.TestPort = null;
    }

    [TearDown]
    public void TearDown()
    {
        UnityTcpBridge.TestPort = _previousTestPort;
    }

    [Test]
    public void ResolvePort_ReturnsDefault_WhenNoOverride()
    {
        Assert.AreEqual(UnityTcpBridge.DefaultPort, UnityTcpBridge.ResolvePort());
    }

    [Test]
    public void ResolvePort_ReturnsTestPort_WhenStaticOverrideSet()
    {
        UnityTcpBridge.TestPort = 19337;
        Assert.AreEqual(19337, UnityTcpBridge.ResolvePort());
    }

    [Test]
    public void ResolvePort_TestPortTakesPrecedenceOverFallback()
    {
        UnityTcpBridge.TestPort = 12345;
        Assert.AreEqual(12345, UnityTcpBridge.ResolvePort(9999));
    }

    [Test]
    public void ResolvePort_ReturnsFallback_WhenNoOverrides()
    {
        Assert.AreEqual(5555, UnityTcpBridge.ResolvePort(5555));
    }

    [Test]
    public void ResolvePort_ReadsTheEnvironmentVariable_WhenNoTestOverride()
    {
        var previous = Environment.GetEnvironmentVariable(PortVariable);
        try
        {
            Environment.SetEnvironmentVariable(PortVariable, "18081");
            Assert.AreEqual(18081, UnityTcpBridge.ResolvePort(5555),
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
            UnityTcpBridge.TestPort = 17000;

            Assert.AreEqual(17000, UnityTcpBridge.ResolvePort(5555),
                "приоритет: TestPort выше UNITY_MCP_PORT. Иначе переменная окружения "
                + "машины, на которой идёт прогон, молча уводит тест на чужой порт");
        }
        finally
        {
            Environment.SetEnvironmentVariable(PortVariable, previous);
        }
    }

    private const string PortVariable = "UNITY_MCP_PORT";
}

