using System;
using UnityEngine;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    public static class McpProfileStatus
    {
        public const string ProfileArgument = McpProfileArgument.Name;

        public static McpToolProfile? TestProfile { get; set; }

        private static McpToolProfile _resolved = McpToolProfile.Full;
        private static bool _resolvedOnce;

        public static McpToolProfile Current
        {
            get
            {
                if (TestProfile.HasValue) return TestProfile.Value;
                if (!_resolvedOnce) Resolve();
                return _resolved;
            }
        }

        public static void ResetForTests()
        {
            TestProfile = null;
            _resolved = McpToolProfile.Full;
            _resolvedOnce = false;
        }

        public static void Resolve()
        {
            var result = McpProfileArgument.Parse(Environment.GetCommandLineArgs());
            _resolved = result.Profile;
            _resolvedOnce = true;

            if (result.Warning != null)
                Debug.LogWarning("[MCP] " + result.Warning);
        }

        public static void LogStartupStatus()
        {
            Resolve();
            int listed = McpToolProfiles.Select(McpToolRegistry.Tools, _resolved).Count;
            Debug.Log("[MCP] Tool profile: " + _resolved.ToString().ToLowerInvariant()
                + " (" + listed + " of " + McpToolRegistry.Tools.Count + " tools listed; the rest stay callable by name)");
        }
    }
}
