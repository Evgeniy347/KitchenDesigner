using System.IO;

namespace KitchenDesigner.Core
{
    public static class RecentProjectRowSource
    {
        internal static string CurrentVersion { get; set; } = BuildInfo.Version;

        public static RecentProjectRow For(string path)
        {
            bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);
            if (!exists)
                return RecentProjectRow.Describe(path, false, null, CurrentVersion, null, null, null);

            string json = ReadOrEmpty(path);
            string storedVersion = ProjectFileVersion.In(json);
            string createdAtUtc = ProjectFileCreatedAt.In(json);
            string fallbackCreated = ProjectFileCreatedAt.FallbackFromFileSystemUtc(path);
            string modified = SafeModifiedUtc(path);

            return RecentProjectRow.Describe(path, true, storedVersion, CurrentVersion,
                createdAtUtc, fallbackCreated, modified);
        }

        private static string ReadOrEmpty(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return "";
            }
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
