using System;

namespace KitchenDesigner.Core.MCP
{
    public static class McpRequestGate
    {
        public const string Path = "/mcp";

        public const int MaxBodyBytes = 4 * 1024 * 1024;

        public static (int status, string? refusal) Inspect(
            string? method, string? path, string? host, string? origin,
            long contentLength, int port, string? authorization = null, string? expectedToken = null)
        {
            if (!IsOurPath(path))
                return (404, "Not found");

            if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                return (405, "Only POST is supported");

            if (!IsLoopbackAuthority(host, null))
                return (403, "Host is not loopback");

            if (origin != null && !IsLoopbackAuthority(OriginAuthority(origin), port))
                return (403, "Origin is not allowed");

            if (contentLength > MaxBodyBytes)
                return (413, "Body is larger than " + MaxBodyBytes + " bytes");

            if (expectedToken != null && authorization != "Bearer " + expectedToken)
                return (401, "Bad token");

            return (0, null);
        }

        private static bool IsOurPath(string? path)
        {
            if (path == null) return false;
            var trimmed = path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal)
                ? path.Substring(0, path.Length - 1)
                : path;
            return string.Equals(trimmed, Path, StringComparison.OrdinalIgnoreCase);
        }

        private static string? OriginAuthority(string origin)
        {
            const string scheme = "http://";
            if (!origin.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return null;
            return origin.Substring(scheme.Length);
        }

        private static bool IsLoopbackAuthority(string? authority, int? requiredPort)
        {
            if (string.IsNullOrEmpty(authority)) return false;

            var hostPart = authority;
            var portPart = string.Empty;

            if (hostPart!.StartsWith("[", StringComparison.Ordinal))
            {
                var close = hostPart.IndexOf(']');
                if (close < 0) return false;
                portPart = hostPart.Substring(close + 1);
                hostPart = hostPart.Substring(0, close + 1);
            }
            else
            {
                var colon = hostPart.IndexOf(':');
                if (colon >= 0)
                {
                    portPart = hostPart.Substring(colon);
                    hostPart = hostPart.Substring(0, colon);
                }
            }

            if (!string.Equals(hostPart, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(hostPart, "localhost", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(hostPart, "[::1]", StringComparison.OrdinalIgnoreCase))
                return false;

            if (requiredPort == null) return true;
            return portPart == ":" + requiredPort.Value;
        }
    }
}
