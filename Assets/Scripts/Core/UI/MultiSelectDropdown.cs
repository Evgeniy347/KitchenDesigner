using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Выпадающий список с множественным выбором (галочки). В проекте штатный
    /// <see cref="TMP_Dropdown"/> — одиночный, поэтому фильтры окна ошибок
    /// используют этот виджет.
    ///
    /// Семантика фильтра: пустой набор = «нет ограничения» (проходит всё). Как
    /// только отмечен хотя бы один пункт — проходят только отмеченные. Это
    /// устойчиво к пересборке списка опций (новые коды не отсеиваются молча).
    ///
    /// Раскрытие: клик по полю открывает поповер под ним; клик мимо (полупрозрачный
    /// «ловец» на весь холст) — закрывает. Изменение отметок применяется сразу
    /// (onChanged), окно не имеет кнопки «Применить».
    /// </summary>
    public class MultiSelectDropdown : MonoBehaviour
    {
        private const float RowH = 26f;
        private const float PopupPad = 6f;
        private const float PopupMaxH = 240f;

        private RectTransform _fieldRect = null!;
        private TMP_Text _caption = null!;
        private string _placeholder = "";
        private System.Action? _onChanged;

        private readonly List<string> _options = new List<string>();
        private readonly HashSet<string> _selected = new HashSet<string>();

        private GameObject? _overlay; // ловец + поповер, живут только пока открыто

        /// <summary>Создать поле мультиселекта. Описательная подпись над полем —
        /// ответственность вызывающего (правило 5 UI-GUIDELINES).</summary>
        public static MultiSelectDropdown Create(string name, Transform parent, string placeholder,
            Vector2 anchoredPos, Vector2 size, System.Action? onChanged)
        {
            var btn = UIFactory.CreateButton(name, parent, "", anchoredPos, size, null);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
            btn.GetComponent<RectTransform>().anchoredPosition = anchoredPos;

            var self = btn.gameObject.AddComponent<MultiSelectDropdown>();
            self._fieldRect = btn.GetComponent<RectTransform>();
            self._placeholder = placeholder;
            self._onChanged = onChanged;

            var caption = btn.GetComponentInChildren<TMP_Text>();
            caption.alignment = TextAlignmentOptions.Left;
            caption.margin = new Vector4(8, 0, 20, 0);
            caption.fontSize = 15;
            self._caption = caption;

            // Стрелка ▼ справа — как у одиночного дропдауна.
            var arrow = UIFactory.CreateLabel(name + "_Arrow", btn.transform, UIStyle.GlyphDropdown, 10,
                Vector2.zero, new Vector2(16, 16), TextAnchor.MiddleCenter);
            arrow.color = UIStyle.TextSecondary;
            arrow.raycastTarget = false;
            var arRt = arrow.rectTransform;
            arRt.anchorMin = arRt.anchorMax = arRt.pivot = new Vector2(1, 0.5f);
            arRt.anchoredPosition = new Vector2(-4, 0);

            btn.onClick.AddListener(self.TogglePopup);
            self.UpdateCaption();
            return self;
        }

        /// <summary>Задать список опций, сохранив пересечение с текущим выбором
        /// (пункты, которых больше нет, из выбора убираются).</summary>
        public void SetOptions(IEnumerable<string> options)
        {
            _options.Clear();
            foreach (var o in options)
                if (!_options.Contains(o)) _options.Add(o);

            _selected.RemoveWhere(s => !_options.Contains(s));
            if (_overlay != null) ClosePopup(); // список изменился — старый поповер невалиден
            UpdateCaption();
        }

        /// <summary>Проходит ли значение фильтр. Пустой выбор = проходит всё.</summary>
        public bool IsAllowed(string value) => _selected.Count == 0 || _selected.Contains(value);

        private void UpdateCaption()
        {
            if (_selected.Count == 0 || _selected.Count == _options.Count)
                _caption.text = _placeholder;
            else
                _caption.text = $"Выбрано: {_selected.Count}";
        }

        private void TogglePopup()
        {
            if (_overlay != null) ClosePopup();
            else OpenPopup();
        }

        private void OpenPopup()
        {
            if (_options.Count == 0) return;
            var canvas = _fieldRect.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // Оверлей на весь холст: ловец кликов мимо + сам поповер сверху.
            _overlay = new GameObject("MultiSelectOverlay", typeof(RectTransform));
            var oRt = (RectTransform)_overlay.transform;
            oRt.SetParent(canvas.transform, false);
            oRt.anchorMin = Vector2.zero;
            oRt.anchorMax = Vector2.one;
            oRt.offsetMin = oRt.offsetMax = Vector2.zero;
            oRt.SetAsLastSibling();

            var catcher = UIFactory.CreateRect("Catcher", oRt);
            catcher.anchorMin = Vector2.zero;
            catcher.anchorMax = Vector2.one;
            catcher.offsetMin = catcher.offsetMax = Vector2.zero;
            var catcherImg = catcher.gameObject.AddComponent<Image>();
            catcherImg.color = new Color(0, 0, 0, 0.01f); // невидим, но ловит клик
            var catcherBtn = catcher.gameObject.AddComponent<Button>();
            catcherBtn.transition = Selectable.Transition.None;
            catcherBtn.onClick.AddListener(ClosePopup);

            float popupH = Mathf.Min(_options.Count * RowH + PopupPad * 2f, PopupMaxH);
            var popup = UIFactory.CreatePanel("Popup", oRt, Vector2.zero,
                new Vector2(_fieldRect.rect.width, popupH));
            var pRt = popup.rectTransform;
            pRt.pivot = new Vector2(0, 1); // верхний левый угол поповера
            // Ставим поповер ровно под полем: нижний-левый угол поля = верхний-левый поповера.
            var corners = new Vector3[4];
            _fieldRect.GetWorldCorners(corners);
            pRt.position = corners[0];

            BuildRows(pRt, popupH);
        }

        private void BuildRows(RectTransform popup, float popupH)
        {
            // Прокрутка на случай длинного списка; для 3–6 пунктов не мешает.
            var viewport = UIFactory.CreateRect("Viewport", popup);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0, 0);
            viewport.offsetMax = new Vector2(0, 0);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1f);
            float contentH = _options.Count * RowH + PopupPad * 2f;
            content.sizeDelta = new Vector2(0, contentH);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = contentH > popupH;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;

            // CreateToggle позиционирует бокс/подпись из переданного size.x —
            // ширина строки фиксированная (по ширине поля), не stretch.
            float rowW = _fieldRect.rect.width - 16f;
            float y = -PopupPad;
            foreach (var opt in _options)
            {
                string value = opt;
                var toggle = UIFactory.CreateToggle("Opt", content, value, _selected.Contains(value),
                    Vector2.zero, new Vector2(rowW, RowH), on => OnToggle(value, on));
                var tRt = toggle.GetComponent<RectTransform>();
                tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0, 1);
                tRt.sizeDelta = new Vector2(rowW, RowH);
                tRt.anchoredPosition = new Vector2(8, y);
                y -= RowH;
            }
        }

        private void OnToggle(string value, bool on)
        {
            if (on) _selected.Add(value);
            else _selected.Remove(value);
            UpdateCaption();
            _onChanged?.Invoke();
        }

        private void ClosePopup()
        {
            if (_overlay != null)
            {
                Destroy(_overlay);
                _overlay = null;
            }
        }

        private void OnDisable() => ClosePopup();
        private void OnDestroy() => ClosePopup();
    }
}
