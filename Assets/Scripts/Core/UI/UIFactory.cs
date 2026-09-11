using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class UIFactory
    {
        private static TMP_FontAsset? _fontAsset;
        public static TMP_FontAsset? FontAsset
        {
            get
            {
                if (_fontAsset == null)
                {
                    _fontAsset = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF");
                    if (_fontAsset == null)
                        _fontAsset = BuildCyrillicCapableFontAtRuntime();
                    if (_fontAsset == null)
                        _fontAsset = TMP_Settings.defaultFontAsset;
                    if (_fontAsset == null)
                        Debug.LogError("[UIFactory] No TMP_FontAsset available!");
                }
                return _fontAsset;
            }
        }

        private static TMP_FontAsset? BuildCyrillicCapableFontAtRuntime()
        {
            var unityFont = Resources.Load<Font>("Fonts/LiberationSans");
            if (unityFont == null) return null;

            Debug.LogWarning("[UIFactory] TMPro font created at runtime — run Tools/Kitchen/Create TMP Font From LiberationSans in Editor for better quality and no first-frame hitch.");
            return TMP_FontAsset.CreateFontAsset(unityFont);
        }

        private static TextAlignmentOptions MapAlignment(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Left
        };

        public static Color PanelColor => UIStyle.Panel;
        public static Color ButtonColor => UIStyle.Surface;
        public static Color FieldColor => UIStyle.Field;
        public static Color HighlightColor => UIStyle.HighlightChanged;
        public static Color TextColor => UIStyle.Text;

        internal static ColorBlock InteractiveColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
            c.selectedColor = Color.white;
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            c.disabledColor = UIStyle.DisabledTint;
            return c;
        }

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

        public const int MainUiSortingOrder = 100;

        public static Canvas CreateCanvas(string name)
        {
            EnsureEventSystem();

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = MainUiSortingOrder;

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

        public static TextMeshProUGUI CreateLabel(string name, Transform parent, string text, int fontSize, Vector2 anchoredPos, Vector2 size, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = FontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.alignment = MapAlignment(align);
            return label;
        }

        public static Button CreateButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 size, System.Action? onClick)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var img = rect.gameObject.AddComponent<Image>();
            img.color = ButtonColor;

            var button = rect.gameObject.AddComponent<UIButton>();
            button.colors = InteractiveColors();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var label = CreateLabel(name + "_Label", rect, text, 18, Vector2.zero, size, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        public static Button CreateDangerButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 size, System.Action? onClick)
        {
            var button = CreateButton(name, parent, text, anchoredPos, size, onClick);
            button.GetComponent<Image>().color = UIStyle.Danger;
            return button;
        }

        public static Button CreateConfirmDeleteButton(string name, Transform parent, string text,
            Vector2 anchoredPos, Vector2 size, System.Action onConfirm)
        {
            var button = CreateDangerButton(name, parent, text, anchoredPos, size, null);
            ConfirmDeleteButton.Attach(button, onConfirm);
            return button;
        }

        public static Button CreateCloseButton(Transform windowPanel, System.Action onClose)
        {
            var btn = CreateButton("CloseBtn", windowPanel, UIStyle.GlyphClose,
                Vector2.zero, new Vector2(UIStyle.CloseBtnSize, UIStyle.CloseBtnSize), onClose);
            var rt = btn.GetComponent<RectTransform>();
            AnchorTopRight(rt);
            rt.anchoredPosition = new Vector2(-UIStyle.CloseBtnInset, -UIStyle.CloseBtnInset);
            var lbl = btn.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.fontSize = 20;
            btn.transform.SetAsLastSibling();
            return btn;
        }

        public static RectTransform CreateSectionHeader(string name, Transform parent, string title, float width)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = new Vector2(width, 18f);

            var label = CreateLabel(name + "_Label", rect, title, UIStyle.FontSection,
                Vector2.zero, new Vector2(width, 18f), TextAnchor.MiddleLeft);
            label.color = UIStyle.TextSecondary;
            var lRt = label.rectTransform;
            lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero; lRt.offsetMax = Vector2.zero;
            label.ForceMeshUpdate();
            float textW = label.GetPreferredValues(title, width, 18f).x;

            var line = CreateRect(name + "_Line", rect);
            line.anchorMin = new Vector2(0, 0.5f);
            line.anchorMax = new Vector2(1, 0.5f);
            line.pivot = new Vector2(0, 0.5f);
            line.offsetMin = new Vector2(textW + 8f, -0.5f);
            line.offsetMax = new Vector2(0, 0.5f);
            var lineImg = line.gameObject.AddComponent<Image>();
            lineImg.color = UIStyle.Separator;
            lineImg.raycastTarget = false;

            return rect;
        }

        public const float MinIconSize = 4f;

        public static Button CreateIconButton(string name, Transform parent, Sprite icon, Vector2 anchoredPos, Vector2 size, System.Action onClick, float iconPaddingBothEdges = 12f)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;

            var button = rect.gameObject.AddComponent<UIButton>();
            button.colors = InteractiveColors();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            var iconRect = CreateRect(name + "_Icon", rect);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            float s = Mathf.Max(MinIconSize, Mathf.Min(size.x, size.y) - iconPaddingBothEdges);
            iconRect.sizeDelta = new Vector2(s, s);
            iconRect.anchoredPosition = Vector2.zero;

            var img = iconRect.gameObject.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;

            return button;
        }

        public static TMP_InputField CreateInputField(string name, Transform parent, string initial, Vector2 anchoredPos, Vector2 size)
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

            var input = rect.gameObject.AddComponent<TMP_InputField>();

            var textArea = CreateRect("Text Area", rect);
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(6, 2);
            textArea.offsetMax = new Vector2(-6, -2);

            var text = textArea.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = FontAsset;
            text.text = initial;
            text.fontSize = 16;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Left;
            text.richText = false;

            input.textViewport = textArea;
            input.textComponent = text;
            input.text = initial;
            return input;
        }

        public static TMP_InputField CreateNumberField(string name, Transform parent, string initial,
            Vector2 anchoredPos, Vector2 size, string unit)
        {
            var input = CreateInputField(name, parent, initial, anchoredPos, size);

            var rect = (RectTransform)input.transform;
            var unitLbl = CreateLabel(name + "_Unit", rect, unit, 13,
                Vector2.zero, new Vector2(34, size.y), TextAnchor.MiddleRight);
            unitLbl.color = UIStyle.TextSecondary;
            unitLbl.raycastTarget = false;
            var uRt = unitLbl.rectTransform;
            uRt.anchorMin = uRt.anchorMax = uRt.pivot = new Vector2(1, 0.5f);
            uRt.anchoredPosition = new Vector2(-6, 0);

            KeepTypedTextClearOfTheUnitSuffix(input, unitLbl, unit);
            return input;
        }

        private static void KeepTypedTextClearOfTheUnitSuffix(TMP_InputField input, TMP_Text unitLbl,
            string unit)
        {
            const float gapBeforeTheSuffix = 10f;
            var viewport = input.textViewport;
            if (viewport == null) return;

            float suffixWidth = unitLbl.GetPreferredValues(unit).x;
            viewport.offsetMax = new Vector2(-(gapBeforeTheSuffix + suffixWidth), viewport.offsetMax.y);
        }

        public static void SetHighlight(TMP_InputField field, bool highlight)
        {
            if (field == null) return;
            var outline = field.GetComponent<Outline>();
            if (outline == null) return;
            outline.effectColor = UIStyle.HighlightChanged;
            outline.enabled = highlight;
        }

        public static void SetErrorHighlight(TMP_InputField field)
        {
            if (field == null) return;
            var outline = field.GetComponent<Outline>();
            if (outline == null) return;
            outline.effectColor = UIStyle.HighlightError;
            outline.enabled = true;
        }

        public static Slider CreateSlider(string name, Transform parent, float min, float max,
            float value, Vector2 anchoredPos, Vector2 size, System.Action<float> onChanged)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var slider = rect.gameObject.AddComponent<Slider>();

            var bg = CreateRect(name + "_Bg", rect);
            bg.anchorMin = new Vector2(0, 0.35f);
            bg.anchorMax = new Vector2(1, 0.65f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = FieldColor;

            var fillArea = CreateRect(name + "_FillArea", rect);
            fillArea.anchorMin = new Vector2(0, 0.35f);
            fillArea.anchorMax = new Vector2(1, 0.65f);
            fillArea.offsetMin = new Vector2(4, 0);
            fillArea.offsetMax = new Vector2(-4, 0);

            var fill = CreateRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0, 1);
            fill.sizeDelta = new Vector2(4, 0);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.4f, 0.7f, 1f, 1f);

            var handleArea = CreateRect(name + "_HandleArea", rect);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(7, 0);
            handleArea.offsetMax = new Vector2(-7, 0);

            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(14, 0);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = TextColor;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            if (onChanged != null)
                slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static Toggle CreateToggle(string name, Transform parent, string label, bool value, Vector2 anchoredPos, Vector2 size, System.Action<bool> onChanged, float reservedRightLaneW = 0f)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.colors = InteractiveColors();

            var box = CreatePanel(name + "_Box", rect, new Vector2(-size.x * 0.5f + 14, 0), new Vector2(22, 22), FieldColor);
            toggle.graphic = CreateCheckmark(name + "_Check", box.transform);
            toggle.targetGraphic = box;

            const float textInset = 36f;
            CreateLabel(name + "_Label", rect, label, 16,
                new Vector2((textInset - reservedRightLaneW) * 0.5f, 0),
                new Vector2(size.x - textInset - reservedRightLaneW, size.y), TextAnchor.MiddleLeft);

            toggle.isOn = value;
            if (onChanged != null)
                toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        public static Image CreateCheckmark(string name, Transform box)
        {
            var rt = CreateRect(name, box);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(12, 12);
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = UIStyle.Accent;
            img.raycastTarget = false;
            return img;
        }

        public const float DropdownItemLabelLeft = 26f;
        public const float DropdownItemLabelRight = 8f;
        public const int DropdownItemFontSize = 14;

        private const int DropdownVisibleItems = 7;
        private const float DropdownListMaxH = 320f;
        private const float DropdownListMaxW = 420f;

        public static TMP_Dropdown CreateDropdown(string name, Transform parent,
            System.Collections.Generic.List<string> options,
            Vector2 anchoredPos, Vector2 size, System.Action<int> onChanged)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;

            var dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.colors = InteractiveColors();

            var caption = CreateLabel(name + "_Label", rect, "", 15, Vector2.zero, size, TextAnchor.MiddleLeft);
            var capRt = caption.rectTransform;
            capRt.anchorMin = Vector2.zero; capRt.anchorMax = Vector2.one;
            capRt.offsetMin = new Vector2(8, 2); capRt.offsetMax = new Vector2(-18, -2);
            ClipTheClosedCaptionToOneLine(caption);

            var arrow = CreateLabel(name + "_Arrow", rect, UIStyle.GlyphDropdown, 10, Vector2.zero, new Vector2(16, 16), TextAnchor.MiddleCenter);
            arrow.color = UIStyle.TextSecondary;
            arrow.raycastTarget = false;
            var arRt = arrow.rectTransform;
            arRt.anchorMin = arRt.anchorMax = arRt.pivot = new Vector2(1, 0.5f);
            arRt.sizeDelta = new Vector2(16, 16); arRt.anchoredPosition = new Vector2(-4, 0);

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

            var itemLabel = CreateLabel("Item Label", item, "Option", DropdownItemFontSize,
                Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
            var ilRt = itemLabel.rectTransform;
            ilRt.anchorMin = Vector2.zero; ilRt.anchorMax = Vector2.one;
            ilRt.offsetMin = new Vector2(DropdownItemLabelLeft, 1);
            ilRt.offsetMax = new Vector2(-DropdownItemLabelRight, -1);
            WrapTheOpenListItemOverSeveralLines(itemLabel);

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

            dropdown.options = options?.ConvertAll(o => new TMP_Dropdown.OptionData(o));

            template.gameObject.SetActive(false);
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            FitDropdownItems(dropdown);

            if (onChanged != null)
                dropdown.onValueChanged.AddListener(v => onChanged(v));

            return dropdown;
        }

        public static void FitDropdownItems(TMP_Dropdown? dropdown)
        {
            if (dropdown == null) return;
            var template = dropdown.template;
            if (template == null) return;

            var item = template.Find("Viewport/Content/Item") as RectTransform;
            var content = template.Find("Viewport/Content") as RectTransform;
            if (item == null || content == null) return;

            var texts = new System.Collections.Generic.List<string>(dropdown.options.Count);
            foreach (var o in dropdown.options) texts.Add(o.text);

            var ddRt = dropdown.GetComponent<RectTransform>();
            float ddWidth = ddRt.rect.width > 1f ? ddRt.rect.width : ddRt.sizeDelta.x;
            const float pad = DropdownItemLabelLeft + DropdownItemLabelRight;

            float listWidth = Mathf.Clamp(
                DropdownItemFit.WidthFor(texts, DropdownItemFontSize) + pad,
                ddWidth, DropdownListMaxW);
            float listWidthOverTheClosedControl = listWidth - ddWidth;
            template.sizeDelta = new Vector2(listWidthOverTheClosedControl, template.sizeDelta.y);

            float itemH = DropdownItemFit.HeightFor(texts, listWidth - pad, DropdownItemFontSize);
            item.sizeDelta = new Vector2(item.sizeDelta.x, itemH);
            content.sizeDelta = new Vector2(content.sizeDelta.x, itemH + 2f);
            template.sizeDelta = new Vector2(template.sizeDelta.x,
                Mathf.Min(itemH * DropdownVisibleItems, DropdownListMaxH));
        }

        private static void ClipTheClosedCaptionToOneLine(TMP_Text caption)
        {
            caption.enableWordWrapping = false;
            caption.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void WrapTheOpenListItemOverSeveralLines(TMP_Text itemLabel)
        {
            itemLabel.enableWordWrapping = true;
            itemLabel.overflowMode = TextOverflowModes.Truncate;
        }
    }
}
