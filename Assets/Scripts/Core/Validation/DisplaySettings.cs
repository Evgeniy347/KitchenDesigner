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
        }

        internal static bool TheAppOwnsItsWindowSize()
        {
#if UNITY_WEBGL
            return false;
#else
            return !Application.isEditor;
#endif
        }
    }
}
