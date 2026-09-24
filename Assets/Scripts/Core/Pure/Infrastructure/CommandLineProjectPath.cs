namespace KitchenDesigner.Core
{
    public static class CommandLineProjectPath
    {
        public static string? Parse(string[]? args)
        {
            if (args == null) return null;

            foreach (var arg in args)
            {
                if (string.IsNullOrEmpty(arg)) continue;
                if (arg[0] == '-') continue;
                if (ProjectFileExtension.IsSupported(arg)) return arg;
            }
            return null;
        }
    }
}
