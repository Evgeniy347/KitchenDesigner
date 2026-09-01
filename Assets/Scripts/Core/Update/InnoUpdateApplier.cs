#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    public sealed class InnoUpdateApplier : IUpdateApplier
    {
        public const string SilentRelaunchArguments =
            "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH";

        public void ApplyAndRelaunch(string installerPath)
        {
            if (string.IsNullOrEmpty(installerPath)) return;

            SaveProjectTheSameWayQuittingDoes();
            if (!TryStartInstaller(installerPath)) return;
            QuitSoInnoCanReplaceOurFiles();
        }

        private static void SaveProjectTheSameWayQuittingDoes()
        {
            try { AutoSaveManager.SaveOnQuit(); }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(
                    "[Update] не удалось сохранить перед обновлением: " + e.Message);
            }
        }

        private static bool TryStartInstaller(string installerPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = SilentRelaunchArguments,
                    UseShellExecute = false,
                });
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(
                    "[Update] не удалось запустить установщик: " + e.Message);
                return false;
            }
        }

        private static void QuitSoInnoCanReplaceOurFiles() => Application.Quit();
    }
}
#endif
