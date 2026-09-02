using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KitchenDesigner.Editor
{
    public static class BuildInfoGenerator
    {
        public const string OutputPath = "Assets/Scripts/Core/Infrastructure/BuildInfo.Generated.cs";
        public const string CounterFile = "build_count.txt";

        [MenuItem("KitchenDesigner/Generate BuildInfo")]
        public static void Generate()
        {
            WriteFile();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[BuildInfoGenerator] {GetVersion()} — {DateTime.Now:yyyy-MM-dd HH:mm}");
        }

        public static void WriteFile()
        {
            string version = GetVersion();

            // Повторная сборка без новых коммитов НЕ должна трогать файл.
            // Любая перезапись (минутный BuildDate) меняет const в сборке
            // KitchenDesigner.Runtime → Unity перекомпилирует весь рантайм, и
            // «пустая» повторная сборка переставала быть инкрементальной.
            // BuildDate теперь означает «дата первой сборки этой версии».
            if (File.Exists(OutputPath) &&
                File.ReadAllText(OutputPath).Contains($"Version = \"{version}\""))
                return;

            string buildDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            string content = $@"namespace KitchenDesigner.Core
{{
    public static partial class BuildInfo
    {{
        public const string Version = ""{version}"";
        public const string BuildDate = ""{buildDate}"";
    }}
}}
";
            File.WriteAllText(OutputPath, content);
        }

        public static string GetVersion()
        {
            int count = 0;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-list --count HEAD",
                    WorkingDirectory = Application.dataPath + "/..",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0 && int.TryParse(output, out int gitCount))
                    count = gitCount;
            }
            catch { }

            if (count == 0)
                count = ReadAndIncrementCounter();

            return $"0.{count}";
        }

        public static int ReadAndIncrementCounter()
        {
            string path = Path.Combine(Application.dataPath, "..", CounterFile);
            int count = 0;
            try
            {
                if (File.Exists(path))
                    int.TryParse(File.ReadAllText(path).Trim(), out count);
            }
            catch { }
            count++;
            try
            {
                File.WriteAllText(path, count.ToString());
            }
            catch { }
            return count;
        }
    }
}
