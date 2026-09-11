namespace KitchenDesigner.Core
{
    public static class ProjectVersionNotice
    {
        public static bool FileIsNewerThanApp(string? fileVersion, string? appVersion) =>
            !string.IsNullOrEmpty(fileVersion)
            && !string.IsNullOrEmpty(appVersion)
            && Update.VersionUtil.Compare(fileVersion!, appVersion!) > 0;
    }
}
