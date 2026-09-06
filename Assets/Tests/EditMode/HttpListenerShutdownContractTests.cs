#nullable disable
using System;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;

/// <summary>
/// McpHttpBridge.StopBridge narrows its shutdown catches to ObjectDisposedException
/// (was a bare `catch { }`). That narrowing is only correct if THIS is really the
/// exception .NET's HttpListener throws once the instance is torn down — pin the
/// contract here so a BCL/runtime change that picks a different exception type shows
/// up as a red test instead of a silently-reopened swallow-everything catch.
/// </summary>
public class HttpListenerShutdownContractTests
{
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Test]
    public void Stop_AfterClose_ThrowsObjectDisposedException()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{FreePort()}/");
        listener.Start();
        listener.Close();

        Assert.Throws<ObjectDisposedException>(() => listener.Stop(),
            "McpHttpBridge.StopBridge catches exactly ObjectDisposedException around "
            + "_listener.Stop(); if the runtime ever throws something else here, that "
            + "narrow catch stops swallowing the expected case and StopBridge starts "
            + "throwing on ordinary shutdown");
    }

    [Test]
    public void Close_CalledTwice_DoesNotThrow()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{FreePort()}/");
        listener.Start();
        listener.Close();

        Assert.DoesNotThrow(() => listener.Close(),
            "a second Close() is documented as a safe no-op; if that ever changes to "
            + "throwing something other than ObjectDisposedException, McpHttpBridge's "
            + "narrowed catch around _listener.Close() would let it escape");
    }
}
