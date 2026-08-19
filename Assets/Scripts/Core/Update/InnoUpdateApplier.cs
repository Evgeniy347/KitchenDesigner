#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using UnityEngine;

namespace KitchenDesigner.Core.Update
{
    /// <summary>
    /// Применяет скачанный установщик: сохраняет сцену (как при обычном выходе),
    /// запускает Inno Setup в тихом режиме и завершает текущий процесс. Inno сам
    /// дождётся закрытия приложения (<c>CloseApplications=yes</c>) и — по флагу
    /// <c>/RELAUNCH</c> (<see cref="KitchenDesigner.iss"/>) — запустит новую версию.
    /// Компилируется только в собранном Windows-плеере, поэтому в редакторе/тестах
    /// не может ничего запустить на ПК.
    /// </summary>
    public sealed class InnoUpdateApplier : IUpdateApplier
    {
        public void ApplyAndRelaunch(string installerPath)
        {
            if (string.IsNullOrEmpty(installerPath)) return;

            try { AutoSaveManager.SaveOnQuit(); }
            catch (Exception e) { UnityEngine.Debug.Log("[Update] не удалось сохранить перед обновлением: " + e.Message); }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH",
                    UseShellExecute = false,
                };
                Process.Start(psi);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError("[Update] не удалось запустить установщик: " + e.Message);
                return; // остаёмся живы — юзер ничего не потерял
            }

            // Устанавливаемая папка не залочена нашим процессом к моменту,
            // когда Inno дойдёт до замены файлов; сам Inno подстрахуется CloseApplications.
            Application.Quit();
        }
    }
}
#endif
