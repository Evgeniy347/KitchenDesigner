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
                {
                    // Реальный .ttf из Resources: текстура шрифта генерируется на этапе билда
                    // и работает на всех платформах, включая WebGL (где GetBuiltinResource
                    // возвращает шрифт без текстуры, потому что WebGL не умеет рендерить
                    // системные шрифты на лету).
                    _font = Resources.Load<Font>("Fonts/arial");
                    if (_font == null)
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static readonly Color PanelColor = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.30f, 1f);
        public static readonly Color FieldColor = new Color(0.08f, 0.08f, 0.10f, 1f);
        public static readonly Color HighlightColor = new Color(1f, 0.84f, 0.0f, 1f);
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

        public static void AnchorBottomLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
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

        /// <summary>Кнопка с иконкой-спрайтом по центру вместо текста.</summary>
        public static Button CreateIconButton(string name, Transform parent, Sprite icon, Vector2 anchoredPos, Vector2 size, System.Action onClick)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var iconRect = CreateRect(name + "_Icon", rect);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            float s = Mathf.Min(size.x, size.y) - 12f;
            iconRect.sizeDelta = new Vector2(s, s);
            iconRect.anchoredPosition = Vector2.zero;

            var img = iconRect.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;

            return button;
        }

        public static InputField CreateInputField(string name, Transform parent, string initial, Vector2 anchoredPos, Vector2 size)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var img = rect.gameObject.AddComponent<Image>();
            img.color = FieldColor;

            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = HighlightColor;
            outline.effectDistance = new Vector2(2, 2);
            outline.enabled = false;

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

        /// <summary>Подсветить/снять подсветку изменённого поля (жёлтая рамка).</summary>
        public static void SetHighlight(InputField field, bool highlight)
        {
            if (field == null) return;
            var outline = field.GetComponent<Outline>();
            if (outline != null)
                outline.enabled = highlight;
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

            // Текст начинается правее бокса с галочкой (его правый край ≈ -size.x/2+25),
            // с отступом ~11px — иначе «X» и подпись слипаются. Ширину области урезаем
            // на этот отступ, чтобы метка не вылезала за правый край тоггла.
            const float textInset = 36f;
            CreateLabel(name + "_Label", rect, label, 16,
                new Vector2(textInset * 0.5f, 0), new Vector2(size.x - textInset, size.y), TextAnchor.MiddleLeft);

            toggle.isOn = value;
            if (onChanged != null)
                toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        /// <summary>Выпадающий список (legacy uGUI Dropdown), собранный из кода —
        /// с прокручиваемым шаблоном списка. Возвращает Dropdown; текущий индекс —
        /// через .value / SetValueWithoutNotify.</summary>
        public static Dropdown CreateDropdown(string name, Transform parent,
            System.Collections.Generic.List<string> options,
            Vector2 anchoredPos, Vector2 size, System.Action<int> onChanged)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;

            var dropdown = rect.gameObject.AddComponent<Dropdown>();

            // Подпись текущего выбора + стрелка.
            var caption = CreateLabel(name + "_Label", rect, "", 15, Vector2.zero, size, TextAnchor.MiddleLeft);
            var capRt = caption.rectTransform;
            capRt.anchorMin = Vector2.zero; capRt.anchorMax = Vector2.one;
            capRt.offsetMin = new Vector2(8, 2); capRt.offsetMax = new Vector2(-18, -2);

            var arrow = CreateLabel(name + "_Arrow", rect, "▾", 14, Vector2.zero, new Vector2(16, 16), TextAnchor.MiddleCenter);
            var arRt = arrow.rectTransform;
            arRt.anchorMin = arRt.anchorMax = arRt.pivot = new Vector2(1, 0.5f);
            arRt.sizeDelta = new Vector2(16, 16); arRt.anchoredPosition = new Vector2(-4, 0);

            // Шаблон списка (выключен, пока список закрыт) со скроллом.
            var template = CreateRect(name + "_Template", rect);
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0, 2);
            template.sizeDelta = new Vector2(0, 170);
            var templateImg = template.gameObject.AddComponent<Image>();
            templateImg.color = PanelColor;
            var scroll = template.gameObject.AddComponent<ScrollRect>();

            var viewport = CreateRect("Viewport", template);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0, 1); viewport.sizeDelta = Vector2.zero;
            var viewportImg = viewport.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0, 0, 0, 0.01f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = new Vector2(0, 26);

            var item = CreateRect("Item", content);
            item.anchorMin = new Vector2(0, 0.5f); item.anchorMax = new Vector2(1, 0.5f);
            item.pivot = new Vector2(0.5f, 0.5f); item.sizeDelta = new Vector2(0, 24);
            var itemToggle = item.gameObject.AddComponent<Toggle>();

            var itemBg = CreateRect("Item Background", item);
            itemBg.anchorMin = Vector2.zero; itemBg.anchorMax = Vector2.one; itemBg.sizeDelta = Vector2.zero;
            var itemBgImg = itemBg.gameObject.AddComponent<Image>();
            itemBgImg.color = ButtonColor;

            var itemCheck = CreateRect("Item Checkmark", item);
            itemCheck.anchorMin = itemCheck.anchorMax = itemCheck.pivot = new Vector2(0, 0.5f);
            itemCheck.sizeDelta = new Vector2(14, 14); itemCheck.anchoredPosition = new Vector2(12, 0);
            var itemCheckImg = itemCheck.gameObject.AddComponent<Image>();
            itemCheckImg.color = new Color(0.4f, 0.7f, 1f, 1f);

            var itemLabel = CreateLabel("Item Label", item, "Option", 14, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
            var ilRt = itemLabel.rectTransform;
            ilRt.anchorMin = Vector2.zero; ilRt.anchorMax = Vector2.one;
            ilRt.offsetMin = new Vector2(26, 1); ilRt.offsetMax = new Vector2(-8, -1);

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;
            itemToggle.isOn = true;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.targetGraphic = bg;

            dropdown.options.Clear();
            if (options != null)
                foreach (var o in options)
                    dropdown.options.Add(new Dropdown.OptionData(o));

            template.gameObject.SetActive(false);
            dropdown.value = 0;
            dropdown.RefreshShownValue();

            if (onChanged != null)
                dropdown.onValueChanged.AddListener(v => onChanged(v));

            return dropdown;
        }
    }
}
