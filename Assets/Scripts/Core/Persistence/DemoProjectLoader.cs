using System;
using System.IO;
using UnityEngine;

namespace KitchenDesigner.Core
{
    internal static class DemoProjectLoader
    {
        internal const string DemoFolderName = "Demo";
        internal const string DemoFileName = "demo.json";

        internal static string DemoPath
        {
            get
            {
                string? installDir = Path.GetDirectoryName(Application.dataPath);
                return string.IsNullOrEmpty(installDir)
                    ? string.Empty
                    : Path.Combine(installDir, DemoFolderName, DemoFileName);
            }
        }

        internal static bool IsDemoPath(string? path) =>
            !string.IsNullOrEmpty(path) && DemoMode.SamePath(path!, DemoPath);

        internal static bool OpenDemoOrLastSession(ISaveLoadManager saveLoad)
        {
            bool firstRunRecorded = FirstRunMarker.Recorded;
            var args = Environment.GetCommandLineArgs();
            FirstRunMarker.Record(args);

            if (TryOpenFromCommandLine(saveLoad, args)) return true;

            string demo = DemoPath;
            bool wanted = DemoMode.ShouldOpenDemo(
                firstRunRecorded, saveLoad.HasLastPath, !string.IsNullOrEmpty(demo) && File.Exists(demo));

            if (wanted && OpenDemo(saveLoad, demo)) return true;
            return saveLoad.LoadLastSession();
        }

        internal static bool TryOpenFromCommandLine(ISaveLoadManager saveLoad, string[] args)
        {
            string? path = CommandLineProjectPath.Parse(args);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            return saveLoad.LoadFromPath(path!);
        }

        private static bool OpenDemo(ISaveLoadManager saveLoad, string demoPath)
        {
            if (!saveLoad.LoadFromPath(demoPath)) return false;
            saveLoad.LastPath = string.Empty;
            DemoMode.Current.Enter(demoPath);
            return true;
        }
    }
}
