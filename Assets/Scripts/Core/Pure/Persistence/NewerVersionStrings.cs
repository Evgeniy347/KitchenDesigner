namespace KitchenDesigner.Core
{
    public static class NewerVersionStrings
    {
        public static string Title => Loc.T("dialog.newerVersion.title");

        public static string OpenButton => Loc.T("dialog.newerVersion.open");

        public static string CancelButton => Loc.T("common.cancel");

        public static string Message(string fileVersion, string appVersion) =>
            Loc.F("dialog.newerVersion.message", fileVersion, appVersion);
    }
}
