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

#if UNITY_WEBGL
            // WebGL: браузер сам управляет размером canvas через CSS (ширина 100%).
            // Screen.SetResolution фиксирует canvas в DOM-пикселях, оставляя пустые поля
            // вокруг Unity, пока пользователь не нажмёт F11 — не вызываем.
            return;
#else
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
#endif
        }
    }
}
