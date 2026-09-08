using System;
using System.ComponentModel;
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
            int count = TryGetGitCommitCount("git", Application.dataPath + "/..");
            string source = "git rev-list";

            if (count == 0)
            {
                count = ReadAndIncrementCounter();
                source = CounterFile;
            }

            string version = $"0.{count}";
            UnityEngine.Debug.Log($"[BuildInfoGenerator] version taken: {version} (source: {source})");
            return version;
        }

        public static int TryGetGitCommitCount(string gitExecutable, string workingDirectory)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = gitExecutable,
                    Arguments = "rev-list --count HEAD",
                    WorkingDirectory = workingDirectory,
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
                    return gitCount;

                UnityEngine.Debug.LogWarning(
                    $"[BuildInfoGenerator] git rev-list exited {proc.ExitCode} with output '{output}' — " +
                    $"falling back to {CounterFile}");
                return 0;
            }
            catch (Win32Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BuildInfoGenerator] could not run '{gitExecutable}' ({ex.Message}) — " +
                    $"falling back to {CounterFile}");
                return 0;
            }
        }

        public static int ReadAndIncrementCounter()
        {
            string path = Path.Combine(Application.dataPath, "..", CounterFile);
            int count = ReadCounter(path) + 1;
            WriteCounter(path, count);
            return count;
        }

        private static int ReadCounter(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return 0;
                int.TryParse(File.ReadAllText(path).Trim(), out int count);
                return count;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                UnityEngine.Debug.LogWarning(
                    $"[BuildInfoGenerator] could not read {CounterFile} ({ex.Message}) — counter reset to 0");
                return 0;
            }
        }

        private static void WriteCounter(string path, int count)
        {
            try
            {
                File.WriteAllText(path, count.ToString());
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                UnityEngine.Debug.LogError(
                    $"[BuildInfoGenerator] FAILED to persist {CounterFile}={count} ({ex.Message}) — " +
                    "the next build will compute this same number again");
            }
        }
    }
}
