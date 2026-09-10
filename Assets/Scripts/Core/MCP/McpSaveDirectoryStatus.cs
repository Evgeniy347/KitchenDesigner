using System;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public static class McpSaveDirectoryStatus
    {
        public const string DirectoryArgument = McpSaveDirectoryArgument.Name;

        public static string? TestDirectory { get; set; }

        private static string? _resolved;
        private static bool _resolvedOnce;

        public static string? AllowedDirectory
        {
            get
            {
                if (TestDirectory != null) return TestDirectory;
                if (!_resolvedOnce) Resolve();
                return _resolved;
            }
        }

        public static void ResetForTests()
        {
            TestDirectory = null;
            _resolved = null;
            _resolvedOnce = false;
        }

        public static void Resolve()
        {
            var result = McpSaveDirectoryArgument.Parse(Environment.GetCommandLineArgs());
            _resolved = result.Directory;
            _resolvedOnce = true;

            if (result.Warning != null)
                Debug.LogWarning("[MCP] " + result.Warning);
        }

        public static void LogStartupStatus()
        {
            Resolve();

            if (string.IsNullOrEmpty(_resolved))
                Debug.LogWarning("[MCP] " + DirectoryArgument + " не задан — save_project будет отказывать. "
                    + "Запустите приложение с " + DirectoryArgument + " <каталог>, чтобы разрешить "
                    + "сохранение проекта через MCP.");
            else
                Debug.Log("[MCP] Каталог, разрешённый для save_project: " + _resolved);
        }
    }
}
