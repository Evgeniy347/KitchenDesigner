using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Применение режима окна (оконный с рамкой / на весь экран) из настроек.</summary>
    public static class DisplaySettings
    {
        public static void ApplyWindowMode()
        {
            // В редакторе не трогаем — настройка влияет только на собранный плеер.
            if (Application.isEditor) return;

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
    }
}
