using System;

namespace KitchenDesigner.Core
{
    public static class McpSaveDirectoryArgument
    {
        public const string Name = "-mcpSaveDir";

        public readonly struct Result
        {
            public string? Directory { get; }

            public string? Warning { get; }

            public Result(string? directory, string? warning)
            {
                Directory = directory;
                Warning = warning;
            }
        }

        public static Result Parse(string[]? args)
        {
            if (args == null) return new Result(null, null);

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var raw = args[i + 1];

                if (string.IsNullOrWhiteSpace(raw))
                    return new Result(null,
                        Name + " ожидает путь к каталогу, получена пустая строка — MCP-сохранение отключено");

                return new Result(raw, null);
            }

            return new Result(null, null);
        }
    }
}
