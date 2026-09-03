using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public class McpHttpBridge : MonoBehaviour
    {
        [SerializeField] private int _port = McpBridgeStatus.DefaultPort;
        [SerializeField] private bool _autoStart = true;

        private HttpListener? _listener;
        private Thread? _serverThread;
        private volatile bool _running;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private McpCommandHandler? _handler;
        private McpRpcRouter? _router;

        public int Port => _port;
        public bool IsRunning => _running;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _handler = new McpCommandHandler();
            _router = new McpRpcRouter(DispatchOnMainThread, Application.version);
            _port = McpBridgeStatus.ResolvePort(_port);
            McpBridgeStatus.Report(_port, false);
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

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Start();
            }
            catch (Exception ex)
            {
                _listener = null;
                McpBridgeStatus.Report(_port, false);
                Debug.LogError($"[MCP] Cannot listen on port {_port}: {ex.Message}");
                return;
            }

            _running = true;
            _serverThread = new Thread(ServerLoop) { IsBackground = true, Name = "MCP-HTTP" };
            _serverThread.Start();
            McpBridgeStatus.Report(_port, true);
            Debug.Log($"[MCP] Bridge listening on {McpBridgeStatus.Url}");
        }

        public void StopBridge()
        {
            if (!_running && _listener == null) return;

            _running = false;
            if (_listener != null)
            {
                try { _listener.Close(); } catch { }
                _listener = null;
            }
            _serverThread = null;
            McpBridgeStatus.Report(_port, false);
            Debug.Log("[MCP] Bridge stopped");
        }

        private void ServerLoop()
        {
            while (_running)
            {
                HttpListenerContext context;
                try { context = _listener!.GetContext(); }
                catch (Exception) { return; }

                try { Serve(context); }
                catch (Exception ex)
                {
                    Debug.LogError($"[MCP] Request failed: {ex.Message}");
                    try { context.Response.Abort(); } catch { }
                }
            }
        }

        private void Serve(HttpListenerContext context)
        {
            var request = context.Request;
            var (refusedStatus, refusal) = McpRequestGate.Inspect(
                request.HttpMethod,
                request.Url?.AbsolutePath,
                request.Headers["Host"],
                request.Headers["Origin"],
                request.ContentLength64,
                _port);

            if (refusedStatus != 0)
            {
                Respond(context, refusedStatus, refusal, "text/plain; charset=utf-8");
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            var (status, json) = _router!.Handle(body);
            Respond(context, status, json, "application/json; charset=utf-8");
        }

        private static void Respond(HttpListenerContext context, int status, string? body, string contentType)
        {
            context.Response.StatusCode = status;
            if (body == null)
            {
                context.Response.ContentLength64 = 0;
                context.Response.Close();
                return;
            }

            var payload = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = payload.Length;
            context.Response.OutputStream.Write(payload, 0, payload.Length);
            context.Response.Close();
        }

        private McpResponse DispatchOnMainThread(McpRequest request)
        {
            McpResponse? result = null;
            Exception? failure = null;
            var done = new ManualResetEventSlim(false);

            _mainThreadActions.Enqueue(() =>
            {
                try
                {
                    FrameRateManager.KeepAwake(1f);
                    result = _handler!.Handle(request);
                }
                catch (Exception ex) { failure = ex; }
                finally { done.Set(); }
            });

            if (!done.Wait(TimeSpan.FromSeconds(McpToolCall.TimeoutSeconds)))
                throw new TimeoutException(request.method);
            if (failure != null)
                throw failure;
            return result!;
        }
    }
}
