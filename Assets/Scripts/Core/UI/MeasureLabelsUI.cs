using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core.UI
{
    public class MeasureLabelsUI : MonoBehaviour
    {
        internal const float MarginBySegmentMustExceedTextPx = 8f;
        internal const float LiftAboveTheDottedLinePx = 14f;

        private Transform? _root;
        private Canvas? _canvas;
        private readonly List<TextMeshProUGUI> _pool = new List<TextMeshProUGUI>();
        private int _used;

        public void Build(Transform canvas)
        {
            var rect = UIFactory.CreateRect("MeasureLabels", canvas);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _root = rect;
            _canvas = rect.GetComponentInParent<Canvas>();
        }

        private void LateUpdate()
        {
            using var _ = PerfMarkers.MeasureLabelsLateUpdate.Auto();
            ReuseThePoolFromTheStart();
            var cam = Camera.main;
            if (_root != null && MeasureMode.Active && cam != null)
            {
                foreach (var seg in MeasureStore.Segments)
                    Place(cam, seg.A, seg.B);

                var ctrl = MeasureController.Instance;
                if (ctrl != null && ctrl.HasPreview)
                    Place(cam, ctrl.Anchor!.Value, ctrl.PreviewEnd!.Value);
            }

            for (int i = _used; i < _pool.Count; i++)
                _pool[i].gameObject.SetActive(false);
        }

        internal void ReuseThePoolFromTheStart() => _used = 0;

        internal void Place(Camera cam, Vector3 a, Vector3 b)
        {
            Vector3 sa = cam.WorldToScreenPoint(a);
            Vector3 sb = cam.WorldToScreenPoint(b);
            if (sa.z <= 0f || sb.z <= 0f) return;

            var label = Take();
            label.text = MeasureGeometry.FormatMm((b - a).magnitude,
                MeasureGeometry.AxisOf(a, b) >= 0);

            float canvasUnitsToScreenPx = _canvas != null ? _canvas.scaleFactor : 1f;
            float textWidthPx = label.preferredWidth * canvasUnitsToScreenPx;
            float segmentLengthPx = (new Vector2(sb.x, sb.y) - new Vector2(sa.x, sa.y)).magnitude;
            if (segmentLengthPx < textWidthPx + MarginBySegmentMustExceedTextPx)
            {
                label.gameObject.SetActive(false);
                return;
            }

            var mid = (sa + sb) * 0.5f;
            label.rectTransform.position =
                new Vector3(mid.x, mid.y + LiftAboveTheDottedLinePx, 0f);
        }

        private TextMeshProUGUI Take()
        {
            if (_used < _pool.Count)
            {
                var existing = _pool[_used++];
                existing.gameObject.SetActive(true);
                return existing;
            }

            var label = UIFactory.CreateLabel($"MeasureLabel{_pool.Count}", _root!, "",
                UIStyle.FontBody, Vector2.zero, new Vector2(200f, 24f), TextAnchor.MiddleCenter);
            label.color = UIStyle.MeasureLine;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _pool.Add(label);
            _used++;
            return label;
        }
    }
}
