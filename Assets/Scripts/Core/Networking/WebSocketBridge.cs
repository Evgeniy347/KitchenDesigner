using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

#if UNITY_WEBGL
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
#endif

namespace KitchenDesigner.Core.MCP
{
    public class WebSocketBridge : MonoBehaviour
    {
        [SerializeField] private string _serverUrl = "ws://localhost:5000/api/mcp/ws";
        [SerializeField] private bool _autoConnect = true;

        private McpCommandHandler? _handler;
        private string? _accessKey;
        private volatile bool _running;

#if UNITY_WEBGL
        [DllImport("__Internal")]
        private static extern void WebSocketConnect(string url, string gameObjectName);
        [DllImport("__Internal")]
        private static extern void WebSocketSend(string message);
        [DllImport("__Internal")]
        private static extern void WebSocketClose();
        [DllImport("__Internal")]
        private static extern void ShowLockTakenAlert();
#else
        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();
        private Thread? _receiveThread;
#endif

        public bool IsConnected => _running;

        private void Awake()
        {
            _handler = new McpCommandHandler();
        }

        private void Start()
        {
            if (_autoConnect)
                Connect(_serverUrl, "");
        }

        private void Update()
        {
#if !UNITY_WEBGL
            while (_mainThreadActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Debug.LogError($"[MCP-WS] Handler error: {ex.Message}"); }
            }
#endif
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void SetAutoConnect(bool auto)
        {
            _autoConnect = auto;
        }

        public void Connect(string url, string accessKey)
        {
            if (_running) return;
            _accessKey = accessKey;

            var fullUrl = url;
            if (!string.IsNullOrEmpty(accessKey))
                fullUrl += (fullUrl.Contains('?') ? "&" : "?") + "key=" + Uri.EscapeDataString(accessKey);

#if UNITY_WEBGL
            Debug.Log($"[MCP-WS] Connecting to {fullUrl}");
            WebSocketConnect(fullUrl, gameObject.name);
#else
            _cts = new CancellationTokenSource();
            _receiveThread = new Thread(() => ReceiveLoop(fullUrl, _cts.Token))
                { IsBackground = true, Name = "MCP-WS" };
            _receiveThread.Start();
#endif
        }

        public void Disconnect()
        {
            _running = false;
#if UNITY_WEBGL
            try { WebSocketClose(); } catch (EntryPointNotFoundException) { }
#else
            _cts?.Cancel();
            try { _ws?.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(500); }
            catch { }
            _ws?.Dispose();
            _ws = null;
            _receiveThread?.Join(2000);
#endif
        }

        public void SendResponse(string json)
        {
#if UNITY_WEBGL
            WebSocketSend(json);
#else
            if (_ws?.State == WebSocketState.Open && _running)
            {
                var data = Encoding.UTF8.GetBytes(json);
                var ws = _ws;
                var cts = new CancellationTokenSource(3000);
                _ = ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cts.Token)
                    .ContinueWith(t =>
                    {
                        cts.Dispose();
                        if (t.IsFaulted)
                            Debug.LogError($"[MCP-WS] Send error: {t.Exception?.InnerException?.Message}");
                    }, TaskScheduler.Default);
            }
#endif
        }

        public void OnWebSocketOpen()
        {
            _running = true;
            Debug.Log("[MCP-WS] WebGL WebSocket opened");
        }

        public void OnWebSocketMessage(string json)
        {
            ProcessMessage(json);
        }

        internal const string LockTakenPush = "\"lock_taken\"";

        internal static bool IsServerPushedNotification(string json) => json.Contains(LockTakenPush);

        private void AnnounceProjectLockTakenByAnotherTab()
        {
            Debug.LogWarning("[MCP-WS] Project opened in another tab — shutting down.");
#if UNITY_WEBGL
            ShowLockTakenAlert();
#else
            UnityEngine.Debug.LogError("[MCP-WS] Project lock lost — opened in another tab.");
#endif
        }

        private void ProcessMessage(string json)
        {
            if (IsServerPushedNotification(json))
            {
                AnnounceProjectLockTakenByAnotherTab();
                return;
            }

            McpRequest? request = null;
            try { request = JsonConvert.DeserializeObject<McpRequest>(json); }
            catch (Exception ex)
            {
                SendResponse(JsonConvert.SerializeObject(
                    McpResponse.Error("unknown", -32700, $"Parse error: {ex.Message}")));
                return;
            }

            if (request == null || string.IsNullOrEmpty(request.method))
            {
                SendResponse(JsonConvert.SerializeObject(
                    McpResponse.Error(request?.id ?? "unknown", -32600, "Invalid request")));
                return;
            }

            var result = _handler!.Handle(request);
            var responseJson = JsonConvert.SerializeObject(result);
            SendResponse(responseJson);
        }

#if !UNITY_WEBGL
        private void ReceiveLoop(string url, CancellationToken ct)
        {
            try
            {
                _ws = new ClientWebSocket();
                var connectTask = _ws.ConnectAsync(new Uri(url), ct);
                connectTask.Wait(10000, ct);
                _running = true;
                Debug.Log($"[MCP-WS] Connected to {url}");

                var buffer = new byte[65536];
                var messageBuffer = new StringBuilder();

                while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    var result = _ws.ReceiveAsync(segment, ct).GetAwaiter().GetResult();

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[MCP-WS] Server closed connection");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            var json = messageBuffer.ToString().Trim();
                            messageBuffer.Clear();
                            if (!string.IsNullOrEmpty(json))
                            {
                                _mainThreadActions.Enqueue(() =>
                                {
                                    try { ProcessMessage(json); }
                                    catch (Exception ex) { Debug.LogError($"[MCP-WS] Process error: {ex.Message}"); }
                                });
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP-WS] Connection error: {ex.Message}");
            }
            finally
            {
                _running = false;
                try { _ws?.Dispose(); } catch { }
                _ws = null;
                Debug.Log("[MCP-WS] Disconnected");
            }
        }
#endif
    }
}
