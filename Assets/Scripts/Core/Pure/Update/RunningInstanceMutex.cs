using System;

namespace KitchenDesigner.Core.Update
{
    public static class RunningInstanceMutex
    {
        public const string Name = "KitchenDesigner.RunningInstance";
        public const string ArgumentName = "-mutex";

        public static string Resolve(string[]? args)
        {
            if (args == null) return Name;

            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], ArgumentName, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return args[i + 1];

            return Name;
        }
    }
}
