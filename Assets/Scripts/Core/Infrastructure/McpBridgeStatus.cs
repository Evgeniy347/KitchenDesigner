using System;

namespace KitchenDesigner.Core
{
    public static class McpBridgeStatus
    {
        public const int DefaultPort = 9337;

        public const string Path = "/mcp";

        public const string PortVariable = "UNITY_MCP_PORT";

        public const string PortArgument = "-mcpPort";

        public static int? TestPort { get; set; }

        public static bool Running { get; private set; }

        private static int? _reportedPort;

        public static int Port => _reportedPort ?? ResolvePort();

        public static string Url => "http://127.0.0.1:" + Port + Path;

        public static void Report(int port, bool running)
        {
            _reportedPort = port;
            Running = running;
        }

        public static void Forget()
        {
            _reportedPort = null;
            Running = false;
        }

        public static int ResolvePort(int fallbackPort = DefaultPort)
        {
            if (TestPort.HasValue)
                return TestPort.Value;

            var env = Environment.GetEnvironmentVariable(PortVariable);
            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var envPort) && envPort > 0)
                return envPort;

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(PortArgument, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[i + 1], out var argPort) && argPort > 0)
                {
                    return argPort;
                }
            }

            return fallbackPort;
        }
    }
}
