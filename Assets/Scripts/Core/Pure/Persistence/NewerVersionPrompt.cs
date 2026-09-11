namespace KitchenDesigner.Core
{
    public static class NewerVersionPrompt
    {
        public static System.Action<string, System.Action>? Show;

        public static void Confirm(string? fileVersion, string? appVersion, System.Action open)
        {
            if (open == null) return;
            var ask = Show;
            if (ask == null || !ProjectVersionNotice.FileIsNewerThanApp(fileVersion, appVersion))
            {
                open();
                return;
            }
            ask(NewerVersionStrings.Message(fileVersion!, appVersion!), open);
        }
    }
}
