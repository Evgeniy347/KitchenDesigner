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

        internal static bool OpenDemoOrLastSession(ISaveLoadManager saveLoad)
        {
            bool firstRunRecorded = FirstRunMarker.Recorded;
            FirstRunMarker.Record(Environment.GetCommandLineArgs());

            string demo = DemoPath;
            bool wanted = DemoMode.ShouldOpenDemo(
                firstRunRecorded, saveLoad.HasLastPath, !string.IsNullOrEmpty(demo) && File.Exists(demo));

            if (wanted && OpenDemo(saveLoad, demo)) return true;
            return saveLoad.LoadLastSession();
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
