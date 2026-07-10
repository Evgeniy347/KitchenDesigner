using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Помощники для процедурной сборки uGUI (без префабов/TMP).
    /// Весь интерфейс строится из кода — в духе остального проекта.
    /// </summary>
    public static class UIFactory
    {
        private static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.30f, 1f);
        public static readonly Color FieldColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

        public static void AnchorTopLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        }

        public static void AnchorTopRight(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        }

        public static void AnchorCenter(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void StretchTopBar(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            EnsureEventSystem();

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreatePanel(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Color? color = null)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color ?? PanelColor;
            return img;
        }

        public static Text CreateLabel(string name, Transform parent, string text, int fontSize, Vector2 anchoredPos, Vector2 size, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button CreateButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 size, System.Action onClick)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var img = rect.gameObject.AddComponent<Image>();
            img.color = ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var label = CreateLabel(name + "_Label", rect, text, 18, Vector2.zero, size, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        public static InputField CreateInputField(string name, Transform parent, string initial, Vector2 anchoredPos, Vector2 size)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var img = rect.gameObject.AddComponent<Image>();
            img.color = FieldColor;

            var input = rect.gameObject.AddComponent<InputField>();

            var text = CreateLabel(name + "_Text", rect, initial, 16, Vector2.zero, size, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(6, 2);
            text.rectTransform.offsetMax = new Vector2(-6, -2);
            text.supportRichText = false;

            input.textComponent = text;
            input.text = initial;
            return input;
        }

        public static Toggle CreateToggle(string name, Transform parent, string label, bool value, Vector2 anchoredPos, Vector2 size, System.Action<bool> onChanged)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var toggle = rect.gameObject.AddComponent<Toggle>();

            var box = CreatePanel(name + "_Box", rect, new Vector2(-size.x * 0.5f + 14, 0), new Vector2(22, 22), FieldColor);
            var check = CreateLabel(name + "_Check", box.transform, "X", 16, Vector2.zero, new Vector2(22, 22), TextAnchor.MiddleCenter);
            toggle.graphic = check;
            toggle.targetGraphic = box;

            CreateLabel(name + "_Label", rect, label, 16, new Vector2(20, 0), size, TextAnchor.MiddleLeft);

            toggle.isOn = value;
            if (onChanged != null)
                toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }
    }
}
