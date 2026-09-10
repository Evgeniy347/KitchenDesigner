using System;
using System.Globalization;

namespace KitchenDesigner.Core
{
    public static class McpPortArgument
    {
        public const string Name = "-mcpPort";

        public const int MinPort = 1;

        public const int MaxPort = 65535;

        public readonly struct Result
        {
            public int Port { get; }

            public string? Warning { get; }

            public Result(int port, string? warning)
            {
                Port = port;
                Warning = warning;
            }
        }

        public static Result Parse(string[]? args, int fallbackPort)
        {
            if (args == null) return new Result(fallbackPort, null);

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var raw = args[i + 1];

                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return new Result(fallbackPort,
                        Name + " ожидает номер порта, получено \"" + raw + "\" — использован порт по "
                        + "умолчанию " + fallbackPort);
                }

                if (parsed < MinPort || parsed > MaxPort)
                {
                    return new Result(fallbackPort,
                        Name + " " + parsed + " вне допустимого диапазона " + MinPort + ".." + MaxPort
                        + " — использован порт по умолчанию " + fallbackPort);
                }

                return new Result(parsed, null);
            }

            return new Result(fallbackPort, null);
        }
    }
}
