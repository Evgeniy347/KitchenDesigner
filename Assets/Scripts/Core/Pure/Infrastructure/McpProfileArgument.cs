using System;

namespace KitchenDesigner.Core
{
    public static class McpProfileArgument
    {
        public const string Name = "-mcpProfile";

        public const string FullWord = "full";

        public const string SimpleWord = "simple";

        public readonly struct Result
        {
            public McpToolProfile Profile { get; }

            public string? Warning { get; }

            public Result(McpToolProfile profile, string? warning)
            {
                Profile = profile;
                Warning = warning;
            }
        }

        public static Result Parse(string[]? args)
        {
            if (args == null) return new Result(McpToolProfile.Full, null);

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                var word = (args[i + 1] ?? string.Empty).Trim();

                if (string.Equals(word, SimpleWord, StringComparison.OrdinalIgnoreCase))
                    return new Result(McpToolProfile.Simple, null);

                if (string.Equals(word, FullWord, StringComparison.OrdinalIgnoreCase))
                    return new Result(McpToolProfile.Full, null);

                return new Result(McpToolProfile.Full,
                    Name + " expects " + FullWord + " or " + SimpleWord + ", got \"" + word
                    + "\" - the full tool list is used");
            }

            return new Result(McpToolProfile.Full, null);
        }
    }
}
