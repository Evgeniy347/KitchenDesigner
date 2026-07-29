using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Всплывающая подсказка у иконных кнопок.
    ///
    /// У кнопки со значком нет подписи, и назначение приходится угадывать — это
    /// незакрытый пункт чек-листа docs/UI-GUIDELINES.md («tooltip у иконных
    /// кнопок»). Одна панель на весь UI: подсказка живёт ровно одна, поэтому
    /// плодить по объекту на кнопку смысла нет.
    ///
    /// Наведение ловится тем же приёмом, что в <see cref="DropdownHover"/> —
    /// <see cref="EventTrigger"/> с PointerEnter/PointerExit.</summary>
    public class TooltipUI : MonoBehaviour
    {
        /// <summary>Задержка перед показом. Без неё подсказки вспыхивали бы при
        /// каждом проходе курсора по тулбару.</summary>
        private const float DelaySec = 0.4f;
        /// <summary>Зазор между кнопкой и подсказкой.</summary>
        private const float GapPx = 6f;
        private const float PadX = 10f;
        private const float PadY = 5f;
        /// <summary>Отступ от края экрана при прижатии.</summary>
        private const float ScreenPad = 4f;

        private static TooltipUI? _instance;

        private RectTransform _canvasRect = null!;
        private Image? _panel;
        private TextMeshProUGUI? _label;

        private RectTransform? _pendingTarget;
        private string _pendingText = string.Empty;
        private float _showAt;

        /// <summary>Повесить подсказку на кнопку. Пустой текст — ничего не делает,
        /// чтобы вызывающему не приходилось проверять это самому.</summary>
        public static void Attach(GameObject target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var rect = target.GetComponent<RectTransform>();
            Add(trigger, EventTriggerType.PointerEnter, () => Request(rect, text));
            Add(trigger, EventTriggerType.PointerExit, Hide);
        }

        /// <summary>Спрятать подсказку и снять отложенный показ (уход курсора,
        /// клик, закрытие панели).</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            _instance._pendingTarget = null;
            if (_instance._panel != null) _instance._panel.gameObject.SetActive(false);
        }

        private static void Request(RectTransform? target, string text)
        {
            if (target == null) return;
            var self = Ensure(target);
            if (self == null) return;

            self._pendingTarget = target;
            self._pendingText = text;
            self._showAt = Time.unscaledTime + DelaySec;
            if (self._panel != null) self._panel.gameObject.SetActive(false);
        }

        /// <summary>Синглтон создаётся на КАНВЕ кнопки, а не на отдельном объекте:
        /// подсказка должна лежать в той же иерархии и рисоваться поверх всего,
        /// что в этой канве уже есть. Корень — RectTransform во всю канву: у
        /// обычного Transform-родителя якоря вложенной панели не считаются.</summary>
        private static TooltipUI? Ensure(RectTransform target)
        {
            if (_instance != null) return _instance;

            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var canvasRect = (RectTransform)canvas.transform;
            var root = UIFactory.CreateRect("Tooltip", canvasRect);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var self = root.gameObject.AddComponent<TooltipUI>();
            self._canvasRect = canvasRect;
            _instance = self;
            return self;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_pendingTarget == null) return;

            // Кнопку могли выключить или снести, пока подсказка ждала своей
            // очереди — тогда показывать нечего.
            if (!_pendingTarget.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            if (Time.unscaledTime < _showAt) return;

            var target = _pendingTarget;
            _pendingTarget = null;
            Show(target, _pendingText);
        }

        private void Show(RectTransform target, string text)
        {
            Build();
            if (_panel == null || _label == null) return;

            _label.text = text;
            var textSize = _label.GetPreferredValues(text);
            var size = new Vector2(textSize.x + PadX * 2f, textSize.y + PadY * 2f);
            _panel.rectTransform.sizeDelta = size;
            _panel.rectTransform.anchoredPosition = Place(target, size);
            _panel.gameObject.SetActive(true);
            // Подсказка обязана быть выше любой панели этой канвы, а порядок
            // отрисовки uGUI — это порядок в иерархии. Двигаем КОРЕНЬ: внутри
            // себя подсказка и так одна.
            transform.SetAsLastSibling();
        }

        /// <summary>Точка под серединой нижней границы кнопки, в локальных
        /// координатах канвы, прижатая к её краям.</summary>
        private Vector2 Place(RectTransform target, Vector2 size)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            // ScreenSpaceOverlay: мировые углы — это уже пиксели экрана.
            Vector2 bottomCenter = (corners[0] + corners[3]) * 0.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, bottomCenter, null, out Vector2 local);

            var canvasSize = _canvasRect.rect.size;
            float limitX = Mathf.Max(0f, canvasSize.x * 0.5f - size.x * 0.5f - ScreenPad);
            float x = Mathf.Clamp(local.x, -limitX, limitX);

            // Не хватает места снизу — подсказка уходит НАД кнопкой (её pivot
            // сверху, поэтому смещаем на полную высоту плюс высоту кнопки).
            float y = local.y - GapPx;
            if (y - size.y < -canvasSize.y * 0.5f + ScreenPad)
                y = local.y + target.rect.height + GapPx + size.y;

            return new Vector2(x, y);
        }

        private void Build()
        {
            if (_panel != null) return;

            _panel = UIFactory.CreatePanel("TooltipPanel", transform, Vector2.zero,
                Vector2.zero, UIStyle.Field);
            UIFactory.AnchorCenter(_panel.rectTransform);
            _panel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _panel.raycastTarget = false;

            _label = UIFactory.CreateLabel("TooltipText", _panel.transform, string.Empty,
                UIStyle.FontSmall, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            var lr = _label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(PadX, PadY);
            lr.offsetMax = new Vector2(-PadX, -PadY);
            _label.raycastTarget = false;

            _panel.gameObject.SetActive(false);
        }

        private static void Add(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
