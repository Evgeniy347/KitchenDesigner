#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Экранный оверлей с метриками кадра. Намеренно на IMGUI: дев-инструмент не
    /// должен зависеть от UI-канваса приложения и его состояния (панели, фоторежим).
    /// Текст готовит <see cref="PerfMonitor"/> четыре раза в секунду — здесь только отрисовка,
    /// и только на Repaint, чтобы оверлей не стоил заметно в кадре, который сам измеряет.</summary>
    public class PerfHud : MonoBehaviour
    {
        private const int Width = 460;
        private const int Margin = 8;

        private GUIStyle? _style;
        private Texture2D? _background;

        private void OnGUI()
        {
            if (!PerfMonitor.Enabled) return;

            var monitor = PerfMonitor.Instance;
            if (monitor == null || monitor.HudText.Length == 0) return;
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyle();
            var content = new GUIContent(monitor.HudText);
            float height = _style!.CalcHeight(content, Width);
            GUI.Label(new Rect(Margin, Margin, Width, height), content, _style);
        }

        private void EnsureStyle()
        {
            if (_style != null) return;

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            _background.Apply();

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 6, 6),
                wordWrap = false,
            };
            _style.normal.textColor = Color.white;
            _style.normal.background = _background;

            // Колонки в тексте выровнены пробелами — без моноширинного шрифта они «плывут».
            var mono = Font.CreateDynamicFontFromOSFont("Consolas", 13);
            if (mono != null) _style.font = mono;
        }

        private void OnDestroy()
        {
            if (_background != null) Destroy(_background);
        }
    }
}

#endif
