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
        public readonly string CreatedLabel;
        public readonly string ModifiedLabel;

        private RecentProjectRow(string path, bool fileExists, string versionLabel, bool versionMismatch,
            string createdLabel, string modifiedLabel)
        {
            Path = path;
            FileExists = fileExists;
            VersionLabel = versionLabel;
            VersionMismatch = versionMismatch;
            CreatedLabel = createdLabel;
            ModifiedLabel = modifiedLabel;
        }

        public static RecentProjectRow Describe(string path, bool fileExists, string? storedAppVersion,
            string currentAppVersion, string? createdAtUtc, string? fallbackCreatedAtUtc, string? modifiedAtUtc)
        {
            if (!fileExists)
                return new RecentProjectRow(path, false, MissingVersionLabel, true,
                    MissingVersionLabel, MissingVersionLabel);

            bool hasVersion = !string.IsNullOrEmpty(storedAppVersion);
            string versionLabel = hasVersion ? storedAppVersion! : MissingVersionLabel;
            bool versionMismatch = !hasVersion ||
                !string.Equals(storedAppVersion, currentAppVersion, StringComparison.Ordinal);

            string createdSource = !string.IsNullOrEmpty(createdAtUtc) ? createdAtUtc! : fallbackCreatedAtUtc ?? "";
            string createdLabel = FormatDate(createdSource);
            string modifiedLabel = FormatDate(modifiedAtUtc ?? "");

            return new RecentProjectRow(path, true, versionLabel, versionMismatch, createdLabel, modifiedLabel);
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
