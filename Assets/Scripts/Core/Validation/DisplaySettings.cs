using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DisplaySettings
    {
        public static void ApplyWindowMode()
        {
            if (!TheAppOwnsItsWindowSize()) return;

            var s = KitchenSettings.Instance;
            bool windowed = s == null || s.WindowedMode;

            if (windowed)
            {
                int w = Mathf.Min(1280, Screen.currentResolution.width);
                int h = Mathf.Min(720, Screen.currentResolution.height);
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
            }
            else
            {
                Screen.SetResolution(
                    Screen.currentResolution.width,
                    Screen.currentResolution.height,
                    FullScreenMode.FullScreenWindow);
            }

            ReapplyHiddenWindow();
        }

        private static void ReapplyHiddenWindow()
        {
            if (!HideWindowArgument.Parse(Environment.GetCommandLineArgs())) return;
#if UNITY_STANDALONE_WIN
            Win32WindowVisibility.HideActiveWindow();
#endif
        }

        internal static bool TheAppOwnsItsWindowSize()
        {
            return !Application.isEditor;
        }
    }
}
