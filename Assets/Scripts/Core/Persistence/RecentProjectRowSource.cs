using System.IO;

namespace KitchenDesigner.Core
{
    public static class RecentProjectRowSource
    {
        public static RecentProjectRow For(string path)
        {
            bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);
            if (!exists)
                return RecentProjectRow.Describe(path, false, null, BuildInfo.Version, null, null, null);

            string storedVersion = ProjectFileVersion.Of(path);
            string createdAtUtc = ProjectFileCreatedAt.Of(path);
            string fallbackCreated = ProjectFileCreatedAt.FallbackFromFileSystemUtc(path);
            string modified = SafeModifiedUtc(path);

            return RecentProjectRow.Describe(path, true, storedVersion, BuildInfo.Version,
                createdAtUtc, fallbackCreated, modified);
        }

        private static string SafeModifiedUtc(string path)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path).ToString("o");
            }
            catch (IOException)
            {
                return "";
            }
            catch (System.UnauthorizedAccessException)
            {
                return "";
            }
        }
    }
}
