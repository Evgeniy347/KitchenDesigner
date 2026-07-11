#if !UNITY_WEBGL

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KitchenDesigner.Core
{
    public sealed class TcpCommandBridge : ICommandBridge
    {
        public bool IsRunning { get; private set; }
        public event Action<string> OnCommandReceived;

        private TcpListener _listener;
        private Thread _thread;
        private readonly int _port;

        public TcpCommandBridge(int port = 9337)
        {
            _port = port;
        }

        public void Start()
        {
            if (IsRunning) return;
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            IsRunning = true;
            _thread = new Thread(ListenLoop) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            IsRunning = false;
            _listener?.Stop();
            _thread?.Join(1000);
        }

        private void ListenLoop()
        {
            while (IsRunning)
            {
                try
                {
                    using var client = _listener.AcceptTcpClient();
                    using var stream = client.GetStream();
                    var buffer = new byte[65536];
                    int read = stream.Read(buffer, 0, buffer.Length);
                    var json = Encoding.UTF8.GetString(buffer, 0, read);
                    OnCommandReceived?.Invoke(json);
                }
                catch { }
            }
        }

        public void SendResponse(string json) { }
    }
}

#endif // !UNITY_WEBGL
