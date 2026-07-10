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
}
