using System;

namespace KitchenDesigner.Core.Update
{
    public static class InstallerFileName
    {
        public const string Prefix = "KitchenDesigner-Setup-";
        public const string Suffix = "-x64.exe";
        public const string PartSuffix = ".part";

        private const int MaxVersionParts = 3;
        private const int MaxDigitsPerPart = 9;

        public static string For(string version) => Prefix + version + Suffix;

        public static string PartFor(string version) => For(version) + PartSuffix;

        public static bool IsValidVersion(string? version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            var parts = version!.Split('.');
            if (parts.Length > MaxVersionParts) return false;
            foreach (var part in parts)
            {
                if (part.Length == 0 || part.Length > MaxDigitsPerPart) return false;
                foreach (var ch in part)
                    if (ch < '0' || ch > '9') return false;
            }
            return true;
        }

        public static bool TryParseInstaller(string? name, out string version)
        {
            version = string.Empty;
            if (name == null) return false;
            if (!name.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            if (!name.EndsWith(Suffix, StringComparison.Ordinal)) return false;
            int length = name.Length - Prefix.Length - Suffix.Length;
            if (length <= 0) return false;
            var candidate = name.Substring(Prefix.Length, length);
            if (!IsValidVersion(candidate)) return false;
            version = candidate;
            return true;
        }

        public static bool TryParsePart(string? name, out string version)
        {
            version = string.Empty;
            if (name == null) return false;
            if (!name.EndsWith(PartSuffix, StringComparison.Ordinal)) return false;
            return TryParseInstaller(name.Substring(0, name.Length - PartSuffix.Length), out version);
        }

        public static bool IsOurs(string? name) =>
            TryParseInstaller(name, out _) || TryParsePart(name, out _);
    }
}
