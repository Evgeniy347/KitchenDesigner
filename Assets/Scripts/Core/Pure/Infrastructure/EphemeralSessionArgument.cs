using System;

namespace KitchenDesigner.Core
{
    public static class EphemeralSessionArgument
    {
        public const string Name = "-ephemeralSession";

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
