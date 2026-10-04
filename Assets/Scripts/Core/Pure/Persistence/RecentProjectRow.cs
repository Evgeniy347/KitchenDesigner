using System;

namespace KitchenDesigner.Core
{
    public readonly struct RecentProjectRow
    {
        public const string MissingVersionLabel = "—";

        public readonly string Path;
        public readonly bool FileExists;
        public readonly string VersionLabel;
        public readonly bool VersionMismatch;
        public readonly bool FileNewerThanApp;
        public readonly string CreatedLabel;
        public readonly string ModifiedLabel;

        private RecentProjectRow(string path, bool fileExists, string versionLabel, bool versionMismatch,
            bool fileNewerThanApp, string createdLabel, string modifiedLabel)
        {
            Path = path;
            FileExists = fileExists;
            VersionLabel = versionLabel;
            VersionMismatch = versionMismatch;
            FileNewerThanApp = fileNewerThanApp;
            CreatedLabel = createdLabel;
            ModifiedLabel = modifiedLabel;
        }

        public static RecentProjectRow Describe(string path, bool fileExists, string? storedAppVersion,
            string currentAppVersion, string? createdAtUtc, string? fallbackCreatedAtUtc, string? modifiedAtUtc)
        {
            if (!fileExists)
                return new RecentProjectRow(path, false, MissingVersionLabel, true, false,
                    MissingVersionLabel, MissingVersionLabel);

            bool hasVersion = !string.IsNullOrEmpty(storedAppVersion);
            string versionLabel = hasVersion ? storedAppVersion! : MissingVersionLabel;
            bool versionMismatch = !hasVersion ||
                !string.Equals(storedAppVersion, currentAppVersion, StringComparison.Ordinal);
            bool newer = ProjectVersionNotice.FileIsNewerThanApp(storedAppVersion, currentAppVersion);

            string createdSource = !string.IsNullOrEmpty(createdAtUtc) ? createdAtUtc! : fallbackCreatedAtUtc ?? "";
            string createdLabel = FormatDate(createdSource);
            string modifiedLabel = FormatDate(modifiedAtUtc ?? "");

            return new RecentProjectRow(path, true, versionLabel, versionMismatch, newer, createdLabel,
                modifiedLabel);
        }

        private static string FormatDate(string isoUtc)
        {
            if (string.IsNullOrEmpty(isoUtc)) return MissingVersionLabel;
            if (!DateTime.TryParse(isoUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal
                    | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
                return MissingVersionLabel;
            return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
