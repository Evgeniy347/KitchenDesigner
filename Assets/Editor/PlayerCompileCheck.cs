using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace KitchenDesigner.Editor
{
    public static class PlayerCompileCheck
    {
        private const string OutputFolder = "Temp/PlayerCompileCheck";

        public sealed class Outcome
        {
            public int Assemblies;
            public List<string> Errors = new List<string>();
            public bool Ok => Errors.Count == 0 && Assemblies > 0;
        }

        [MenuItem("KitchenDesigner/Check Player Compile")]
        public static void Run()
        {
            var outcome = Compile(development: false);
            Report(outcome);
            EditorApplication.Exit(outcome.Ok ? 0 : 1);
        }

        public static Outcome Compile(bool development)
        {
            var outcome = new Outcome();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                    outcome.Errors.Add(message);
            }

            Application.logMessageReceived += OnLog;
            try
            {
                Directory.CreateDirectory(OutputFolder);
                var settings = new ScriptCompilationSettings
                {
                    group = BuildTargetGroup.Standalone,
                    target = BuildTarget.StandaloneWindows64,
                    options = development ? ScriptCompilationOptions.DevelopmentBuild : ScriptCompilationOptions.None
                };
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, OutputFolder);
                outcome.Assemblies = result.assemblies?.Count ?? 0;
            }
            catch (Exception e)
            {
                outcome.Errors.Add(e.Message);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }

            sw.Stop();
            Debug.Log($"[PlayerCompileCheck] {(development ? "development" : "release")}: " +
                      $"{outcome.Assemblies} assemblies, {outcome.Errors.Count} errors, {sw.Elapsed.TotalSeconds:F1}s");
            return outcome;
        }

        private static void Report(Outcome outcome)
        {
            if (outcome.Ok)
            {
                Debug.Log("[PlayerCompileCheck] OK");
                return;
            }
            Debug.LogError($"[PlayerCompileCheck] FAILED: {outcome.Errors.Count} errors, {outcome.Assemblies} assemblies");
        }
    }
}
