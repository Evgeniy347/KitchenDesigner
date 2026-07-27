using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KitchenDesigner.Core.Measure;

namespace KitchenDesigner.Core.UI
{
    /// <summary>Подписи замеров: обычный экранный текст постоянного размера в
    /// середине отрезка. Не world-space — иначе при отдалении и повороте камеры
    /// надпись меняла бы размер и разворачивалась ребром к зрителю.</summary>
    public class MeasureLabelsUI : MonoBehaviour
    {
        /// <summary>Запас, на который отрезок должен быть длиннее надписи, чтобы
        /// та не выходила за его концы.</summary>
        private const float FitMarginPx = 8f;
        /// <summary>Подпись приподнята над отрезком, чтобы не лежать на пунктире.</summary>
        private const float VerticalOffsetPx = 14f;

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
            _used = 0;
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

        // Надпись прячется, когда экранная проекция отрезка короче самого текста:
        // иначе число вылезает за концы и читается как чужое.
        private void Place(Camera cam, Vector3 a, Vector3 b)
        {
            Vector3 sa = cam.WorldToScreenPoint(a);
            Vector3 sb = cam.WorldToScreenPoint(b);
            if (sa.z <= 0f || sb.z <= 0f) return;

            var label = Take();
            label.text = MeasureGeometry.FormatMm((b - a).magnitude,
                MeasureGeometry.AxisOf(a, b) >= 0);

            // Ширина текста — в единицах канвы, длина отрезка — в пикселях
            // экрана; приводим к пикселям через scaleFactor CanvasScaler.
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            float screenLength = (new Vector2(sb.x, sb.y) - new Vector2(sa.x, sa.y)).magnitude;
            if (screenLength < label.preferredWidth * scale + FitMarginPx)
            {
                label.gameObject.SetActive(false);
                return;
            }

            var mid = (sa + sb) * 0.5f;
            label.rectTransform.position = new Vector3(mid.x, mid.y + VerticalOffsetPx, 0f);
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
