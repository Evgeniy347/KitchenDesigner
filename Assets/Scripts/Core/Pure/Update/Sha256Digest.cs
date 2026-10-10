using System;
using System.Text;

namespace KitchenDesigner.Core.Update
{
    public static class Sha256Digest
    {
        public const int HexLength = 64;
        private const string GitHubPrefix = "sha256:";

        public static string FromGitHubDigest(string? digest)
        {
            if (digest == null) return string.Empty;
            var trimmed = digest.Trim();
            if (!trimmed.StartsWith(GitHubPrefix, StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return Normalize(trimmed.Substring(GitHubPrefix.Length));
        }

        public static string Normalize(string? hex)
        {
            if (hex == null) return string.Empty;
            var trimmed = hex.Trim();
            if (trimmed.Length != HexLength) return string.Empty;
            var lower = new StringBuilder(HexLength);
            foreach (var ch in trimmed)
            {
                if (ch >= '0' && ch <= '9') lower.Append(ch);
                else if (ch >= 'a' && ch <= 'f') lower.Append(ch);
                else if (ch >= 'A' && ch <= 'F') lower.Append((char)(ch + ('a' - 'A')));
                else return string.Empty;
            }
            return lower.ToString();
        }

        public static string ToHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var hex = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                hex.Append(digits[b >> 4]);
                hex.Append(digits[b & 0xF]);
            }
            return hex.ToString();
        }
    }
}
