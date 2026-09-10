using System;

namespace KitchenDesigner.Core
{
    public static class HideWindowArgument
    {
        public const string Name = "-hideWindow";

        public static bool Parse(string[]? args)
        {
            if (args == null) return false;

            foreach (var arg in args)
                if (string.Equals(arg, Name, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }
    }
}
