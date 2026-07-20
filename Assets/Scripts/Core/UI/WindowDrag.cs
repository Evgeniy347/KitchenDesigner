using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Перетаскивание окна за зону заголовка (зажатая ЛКМ) с гарантией,
    /// что окно остаётся в пределах экрана: во время перетаскивания, при
    /// изменении размера экрана, при изменении размера самого окна и при каждом
    /// его открытии.</summary>
    public static class WindowDrag
    {
        /// <summary>Сделать окно перетаскиваемым за верхнюю полосу высотой
        /// handleHeight. Обработчик вешается на саму панель: события drag
        /// всплывают от лейблов заголовка к панели, а клики по контролам
        /// (кнопки, поля, дропдауны) окно не тянут.</summary>
        public static void Attach(RectTransform window, float handleHeight)
        {
            window.gameObject.AddComponent<WindowDragHandle>().Init(handleHeight);
            if (window.GetComponent<WindowScreenGuard>() == null)
                window.gameObject.AddComponent<WindowScreenGuard>();
        }

        /// <summary>Ресайз окна по высоте за нижний край: невидимая полоса-хэндл
        /// плюс небольшой видимый грип по центру. Ширина окна не меняется,
        /// содержимое пересчитывается якорями (stretch-зоны растягиваются).</summary>
        public static void AttachResizeBottom(RectTransform window, float minHeight)
        {
            var handle = UIFactory.CreateRect("ResizeHandle", window);
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(1, 0);
            handle.pivot = new Vector2(0.5f, 0);
            handle.sizeDelta = new Vector2(0, 10f);
            handle.anchoredPosition = Vector2.zero;

            var img = handle.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0); // невидимая, но ловит raycast

            // Видимый грип-полоска — подсказка, что за край можно тянуть.
            var grip = UIFactory.CreatePanel("Grip", handle, new Vector2(0, 3f),
                new Vector2(36f, 4f), UIFactory.ButtonColor);
            grip.raycastTarget = false;
            var gripRt = grip.rectTransform;
            gripRt.anchorMin = gripRt.anchorMax = new Vector2(0.5f, 0);
            gripRt.pivot = new Vector2(0.5f, 0);

            handle.gameObject.AddComponent<WindowResizeHandle>().Init(window, minHeight);
        }

        /// <summary>Поднять окно поверх остальных окон своего слоя.</summary>
        public static void BringToFront(RectTransform window) => window.SetAsLastSibling();

        /// <summary>Сдвинуть окно так, чтобы оно целиком помещалось в границы
        /// родителя (канваса). Если окно больше родителя, приоритет — левый
        /// верхний угол, чтобы заголовок оставался доступным.</summary>
        public static void ClampToParent(RectTransform window)
        {
            var parent = window.parent as RectTransform;
            if (parent == null) return;

            var corners = new Vector3[4];
            window.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]); // левый нижний
            Vector2 max = parent.InverseTransformPoint(corners[2]); // правый верхний
            float w = max.x - min.x, h = max.y - min.y;
            Rect pr = parent.rect;

            float dx = Mathf.Max(pr.xMin, Mathf.Min(min.x, pr.xMax - w)) - min.x;
            float dy = Mathf.Min(pr.yMax, Mathf.Max(max.y, pr.yMin + h)) - max.y;
            if (dx != 0f || dy != 0f)
                window.anchoredPosition += new Vector2(dx, dy);
        }
    }

    /// <summary>Тянет окно за зону заголовка, не выпуская его за пределы
    /// экрана. Вешается на панель окна через WindowDrag.Attach.</summary>
    public class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>();

        private float _handleHeight;
        private Vector2 _grabOffset;
        private bool _dragging;

        public void Init(float handleHeight) => _handleHeight = handleHeight;

        // Открытое окно — сразу на передний план.
        private void OnEnable() => WindowDrag.BringToFront((RectTransform)transform);

        // Любой клик по окну (включая кнопки/поля, съедающие события) поднимает
        // его: raycast от курсора вручную, т.к. PointerDown до панели не всплывает,
        // если его обработал дочерний контрол.
        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            var es = EventSystem.current;
            if (es == null) return;

            var ped = new PointerEventData(es) { position = Input.mousePosition };
            RaycastBuffer.Clear();
            es.RaycastAll(ped, RaycastBuffer);
            if (RaycastBuffer.Count > 0 && RaycastBuffer[0].gameObject != null &&
                RaycastBuffer[0].gameObject.transform.IsChildOf(transform))
                WindowDrag.BringToFront((RectTransform)transform);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = false;
            if (e.button != PointerEventData.InputButton.Left) return;

            // Нажатие на интерактивном контроле (кнопка, дропдаун, поле, слайдер)
            // окно не тянет — только заголовок и «немые» лейблы над ним.
            var pressGo = e.pointerPressRaycast.gameObject;
            if (pressGo != null && pressGo.GetComponentInParent<Selectable>() != null) return;

            var window = (RectTransform)transform;
            var parent = window.parent as RectTransform;
            if (parent == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    window, e.pressPosition, e.pressEventCamera, out var local)) return;
            if (local.y < window.rect.yMax - _handleHeight) return; // ниже зоны заголовка

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, e.position, e.pressEventCamera, out var p))
            {
                _grabOffset = window.anchoredPosition - p;
                _dragging = true;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            var window = (RectTransform)transform;
            var parent = window.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, e.position, e.pressEventCamera, out var p))
            {
                window.anchoredPosition = p + _grabOffset;
                WindowDrag.ClampToParent(window);
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_dragging)
                WindowDrag.ClampToParent((RectTransform)transform);
            _dragging = false;
        }
    }

    /// <summary>Хэндл нижнего края: тянет высоту окна (верх на месте — pivot
    /// сверху), в пределах от minHeight до низа канваса. Вешается через
    /// WindowDrag.AttachResizeBottom.</summary>
    public class WindowResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform? _window;
        private float _minHeight;
        private float _startHeight;
        private float _startPointerY;
        private bool _dragging;

        public void Init(RectTransform window, float minHeight)
        {
            _window = window;
            _minHeight = minHeight;
        }

        /// <summary>Установить высоту окна с клампом [minHeight; до низа родителя].
        /// Выделено в метод ради тестируемости и переиспользования.</summary>
        public void ResizeTo(float height)
        {
            if (_window == null) return;
            var parent = _window.parent as RectTransform;
            float maxH = float.MaxValue;
            if (parent != null)
            {
                // От текущего верха окна до нижней границы родителя.
                var corners = new Vector3[4];
                _window.GetWorldCorners(corners);
                float top = ((Vector2)parent.InverseTransformPoint(corners[1])).y;
                maxH = top - parent.rect.yMin;
            }
            float h = Mathf.Clamp(height, _minHeight, Mathf.Max(_minHeight, maxH));
            _window.sizeDelta = new Vector2(_window.sizeDelta.x, h);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = false;
            if (_window == null || e.button != PointerEventData.InputButton.Left) return;
            var parent = _window.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, e.position, e.pressEventCamera, out var p))
            {
                _startHeight = _window.sizeDelta.y;
                _startPointerY = p.y;
                _dragging = true;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || _window == null) return;
            var parent = _window.parent as RectTransform;
            if (parent == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, e.position, e.pressEventCamera, out var p))
                ResizeTo(_startHeight + (_startPointerY - p.y)); // вниз = выше окно
        }
    }

    /// <summary>Следит, чтобы окно не оказалось за пределами экрана: клампит при
    /// открытии (OnEnable), при изменении размера экрана и при изменении размера
    /// самого окна (например, ContextMenu меняет высоту в Layout). Кламп отложен
    /// до LateUpdate, чтобы не вмешиваться в проход layout-системы.</summary>
    public class WindowScreenGuard : MonoBehaviour
    {
        private Rect _lastParentRect;
        private bool _clampPending;

        private void OnEnable() => _clampPending = true;

        private void OnRectTransformDimensionsChange() => _clampPending = true;

        private void LateUpdate()
        {
            // Ресайз экрана меняет rect канваса-родителя (сама панель при этом
            // OnRectTransformDimensionsChange не получает) — следим за ним напрямую.
            var parent = transform.parent as RectTransform;
            if (parent != null && parent.rect != _lastParentRect)
            {
                _lastParentRect = parent.rect;
                _clampPending = true;
            }
            if (_clampPending)
            {
                _clampPending = false;
                WindowDrag.ClampToParent((RectTransform)transform);
            }
        }
    }
}
