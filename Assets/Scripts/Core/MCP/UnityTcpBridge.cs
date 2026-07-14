#if !UNITY_WEBGL

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

namespace KitchenDesigner.Core.MCP
{
    public class UnityTcpBridge : MonoBehaviour
    {
        public const int DefaultPort = 9337;

        [SerializeField] private int _port = DefaultPort;
        [SerializeField] private bool _autoStart = true;

        private TcpListener? _listener;
        private Thread? _serverThread;
        private volatile bool _running;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private McpCommandHandler? _handler;

        /// <summary>
        /// Static override used by tests to force a non-default port.
        /// Takes precedence over environment variables and command-line args.
        /// </summary>
        public static int? TestPort { get; set; }

        public int Port => _port;
        public bool IsRunning => _running;

        /// <summary>
        /// Resolves the effective MCP port from (highest to lowest priority):
        /// <see cref="TestPort"/>, UNITY_MCP_PORT environment variable,
        /// -mcpPort command-line argument, or <paramref name="fallbackPort"/>.
        /// </summary>
        public static int ResolvePort(int fallbackPort = DefaultPort)
        {
            if (TestPort.HasValue)
                return TestPort.Value;

            var env = System.Environment.GetEnvironmentVariable("UNITY_MCP_PORT");
            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var envPort) && envPort > 0)
                return envPort;

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("-mcpPort", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[i + 1], out var argPort) && argPort > 0)
                {
                    return argPort;
                }
            }

            return fallbackPort;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _handler = new McpCommandHandler();
            _port = ResolvePort(_port);
        }

        private void Start()
        {
            if (_autoStart)
                StartBridge();
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Debug.LogError($"[MCP] Handler error: {ex.Message}"); }
            }
        }

        private void OnDestroy() => StopBridge();

        public void StartBridge()
        {
            if (_running) return;
            _running = true;
            _serverThread = new Thread(ServerLoop) { IsBackground = true, Name = "MCP-TCP" };
            _serverThread.Start();
            Debug.Log($"[MCP] Bridge started on port {_port}");
        }

        public void StopBridge()
        {
            _running = false;
            lock (_clientsLock)
            {
                foreach (var c in _clients)
                {
                    try { c.Close(); } catch { }
                }
                _clients.Clear();
            }
            if (_listener != null)
            {
                try { _listener.Stop(); } catch { }
                _listener = null;
            }
            Debug.Log("[MCP] Bridge stopped");
        }

        private void ServerLoop()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, _port);
                _listener.Start();

                while (_running)
                {
                    if (!_listener.Pending()) { Thread.Sleep(50); continue; }
                    var client = _listener.AcceptTcpClient();
                    lock (_clientsLock) _clients.Add(client);
                    var thread = new Thread(() => HandleClient(client))
                    { IsBackground = true, Name = "MCP-Client" };
                    thread.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Server error: {ex.Message}");
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                var buffer = new byte[65536];
                var leftover = "";

                while (_running && client.Connected)
                {
                    if (!stream.DataAvailable) { Thread.Sleep(10); continue; }
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    var chunk = leftover + Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var lines = chunk.Split('\n');
                    leftover = lines[lines.Length - 1];

                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        ProcessLine(line, client);
                    }
                }
            }
            catch (IOException) { }
            catch (Exception ex) { Debug.LogError($"[MCP] Client error: {ex.Message}"); }
            finally
            {
                lock (_clientsLock) _clients.Remove(client);
                try { client.Close(); } catch { }
            }
        }

        private void ProcessLine(string line, TcpClient client)
        {
            McpRequest? request = null;
            try { request = JsonConvert.DeserializeObject<McpRequest>(line); }
            catch (Exception ex)
            {
                SendJson(client, JsonConvert.SerializeObject(
                    McpResponse.Error("unknown", -32700, $"Parse error: {ex.Message}")));
                return;
            }

            if (request == null || string.IsNullOrEmpty(request.method))
            {
                SendJson(client, JsonConvert.SerializeObject(
                    McpResponse.Error(request?.id ?? "unknown", -32600, "Invalid request")));
                return;
            }

            var capturedRequest = request;
            var capturedClient = client;
            _mainThreadActions.Enqueue(() =>
            {
                var result = _handler!.Handle(capturedRequest);
                var json = JsonConvert.SerializeObject(result);
                SendJson(capturedClient, json);
            });
        }

        private void SendJson(TcpClient client, string json)
        {
            try
            {
                if (!client.Connected) return;
                var data = Encoding.UTF8.GetBytes(json + "\n");
                var stream = client.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch { }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Kitchen Designer/MCP Bridge/Start")]
        private static void EditorStart()
        {
            var bridge = FindAnyObjectByType<UnityTcpBridge>();
            if (bridge == null)
            {
                var go = new GameObject("MCPBridge");
                bridge = go.AddComponent<UnityTcpBridge>();
            }
            bridge.StartBridge();
        }

        [UnityEditor.MenuItem("Kitchen Designer/MCP Bridge/Stop")]
        private static void EditorStop()
        {
            var bridge = FindAnyObjectByType<UnityTcpBridge>();
            if (bridge != null) bridge.StopBridge();
        }

        [UnityEditor.MenuItem("Kitchen Designer/MCP Bridge/Toggle")]
        private static void EditorToggle()
        {
            var bridge = FindAnyObjectByType<UnityTcpBridge>();
            if (bridge == null) { EditorStart(); return; }
            if (bridge.IsRunning) bridge.StopBridge();
            else bridge.StartBridge();
        }
#endif
    }
}

#endif // !UNITY_WEBGL
