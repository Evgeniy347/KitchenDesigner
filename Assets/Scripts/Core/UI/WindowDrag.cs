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
        private float _handleHeight;
        private Vector2 _grabOffset;
        private bool _dragging;

        public void Init(float handleHeight) => _handleHeight = handleHeight;

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
