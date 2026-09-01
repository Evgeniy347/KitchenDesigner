using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class WindowDrag
    {
        private const float ResizeHandleHeight = 10f;
        private static readonly Vector2 GripSize = new Vector2(36f, 4f);

        public static void Attach(RectTransform window, float handleHeight)
        {
            window.gameObject.AddComponent<WindowDragHandle>().Init(handleHeight);
            if (window.GetComponent<WindowScreenGuard>() == null)
                window.gameObject.AddComponent<WindowScreenGuard>();
        }

        public static void AttachResizeBottom(RectTransform window, float minHeight)
        {
            var handle = UIFactory.CreateRect("ResizeHandle", window);
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(1, 0);
            handle.pivot = new Vector2(0.5f, 0);
            handle.sizeDelta = new Vector2(0, ResizeHandleHeight);
            handle.anchoredPosition = Vector2.zero;

            var img = handle.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);

            var grip = UIFactory.CreatePanel("Grip", handle, new Vector2(0, 3f),
                GripSize, UIFactory.ButtonColor);
            grip.raycastTarget = false;
            var gripRt = grip.rectTransform;
            gripRt.anchorMin = gripRt.anchorMax = new Vector2(0.5f, 0);
            gripRt.pivot = new Vector2(0.5f, 0);

            handle.gameObject.AddComponent<WindowResizeHandle>().Init(window, minHeight);
        }

        public static void BringToFront(RectTransform window) => window.SetAsLastSibling();

        public static void ClampToParent(RectTransform window)
        {
            var parent = window.parent as RectTransform;
            if (parent == null) return;

            var corners = new Vector3[4];
            window.GetWorldCorners(corners);
            Vector2 bottomLeft = parent.InverseTransformPoint(corners[0]);
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);
            float w = topRight.x - bottomLeft.x, h = topRight.y - bottomLeft.y;
            Rect pr = parent.rect;

            float dx = Mathf.Max(pr.xMin, Mathf.Min(bottomLeft.x, pr.xMax - w)) - bottomLeft.x;
            float dy = Mathf.Min(pr.yMax, Mathf.Max(topRight.y, pr.yMin + h)) - topRight.y;
            if (dx != 0f || dy != 0f)
                window.anchoredPosition += new Vector2(dx, dy);
        }
    }

    public class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>();

        private float _handleHeight;
        private Vector2 _grabOffset;
        private bool _dragging;

        public void Init(float handleHeight) => _handleHeight = handleHeight;

        internal static bool TopHitBelongsTo(List<RaycastResult> hits, Transform window) =>
            hits.Count > 0 && hits[0].gameObject != null &&
            hits[0].gameObject.transform.IsChildOf(window);

        internal static bool PressedOnInteractiveControl(GameObject? pressed) =>
            pressed != null && pressed.GetComponentInParent<Selectable>() != null;

        internal static bool InsideTitleBar(float localY, float windowTopY, float handleHeight) =>
            localY >= windowTopY - handleHeight;

        private void OnEnable() => WindowDrag.BringToFront((RectTransform)transform);

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            var es = EventSystem.current;
            if (es == null) return;

            var ped = new PointerEventData(es) { position = Input.mousePosition };
            RaycastBuffer.Clear();
            es.RaycastAll(ped, RaycastBuffer);
            if (TopHitBelongsTo(RaycastBuffer, transform))
                WindowDrag.BringToFront((RectTransform)transform);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = false;
            if (e.button != PointerEventData.InputButton.Left) return;
            if (PressedOnInteractiveControl(e.pointerPressRaycast.gameObject)) return;

            var window = (RectTransform)transform;
            var parent = window.parent as RectTransform;
            if (parent == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    window, e.pressPosition, e.pressEventCamera, out var local)) return;
            if (!InsideTitleBar(local.y, window.rect.yMax, _handleHeight)) return;

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

        public void ResizeTo(float height)
        {
            if (_window == null) return;
            float maxH = HeightFromCurrentTopToParentBottom();
            float h = Mathf.Clamp(height, _minHeight, Mathf.Max(_minHeight, maxH));
            _window.sizeDelta = new Vector2(_window.sizeDelta.x, h);
        }

        private float HeightFromCurrentTopToParentBottom()
        {
            var parent = _window!.parent as RectTransform;
            if (parent == null) return float.MaxValue;

            var corners = new Vector3[4];
            _window.GetWorldCorners(corners);
            float top = ((Vector2)parent.InverseTransformPoint(corners[1])).y;
            return top - parent.rect.yMin;
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
            {
                float pointerTravelDown = _startPointerY - p.y;
                ResizeTo(_startHeight + pointerTravelDown);
            }
        }
    }

    public class WindowScreenGuard : MonoBehaviour
    {
        private Rect _lastParentRect;
        private bool _clampPending;

        private void OnEnable() => _clampPending = true;

        private void OnRectTransformDimensionsChange() => _clampPending = true;

        private void LateUpdate()
        {
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
