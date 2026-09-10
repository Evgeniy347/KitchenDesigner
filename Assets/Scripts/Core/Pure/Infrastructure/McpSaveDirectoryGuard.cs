using System;

namespace KitchenDesigner.Core
{
    public static class McpSaveDirectoryGuard
    {
        public readonly struct Result
        {
            public bool Allowed { get; }

            public string? Reason { get; }

            private Result(bool allowed, string? reason)
            {
                Allowed = allowed;
                Reason = reason;
            }

            public static Result Ok() => new Result(true, null);

            public static Result Refuse(string reason) => new Result(false, reason);
        }

        public static Result Evaluate(string? allowedDirectory, string requestedPath)
        {
            if (string.IsNullOrWhiteSpace(allowedDirectory))
                return Result.Refuse(
                    $"Refused: no MCP save directory is configured. Start the app with "
                    + $"{McpSaveDirectoryArgument.Name} <directory> to allow save_project.");

            if (string.IsNullOrWhiteSpace(requestedPath))
                return Result.Refuse("path required");

            string realAllowed;
            string realRequested;
            try
            {
                realAllowed = RealPathResolver.Resolve(allowedDirectory);
                realRequested = RealPathResolver.Resolve(requestedPath);
            }
            catch (Exception ex)
            {
                return Result.Refuse($"Refused: could not resolve '{requestedPath}': {ex.Message}");
            }

            if (!IsWithin(realAllowed, realRequested))
                return Result.Refuse(
                    $"Refused: '{requestedPath}' resolves outside the allowed MCP save directory "
                    + $"'{allowedDirectory}'.");

            return Result.Ok();
        }

        private static bool IsWithin(string allowedRoot, string candidate)
        {
            if (string.Equals(candidate, allowedRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            var prefix = allowedRoot.EndsWith("\\", StringComparison.Ordinal) ? allowedRoot : allowedRoot + "\\";
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
