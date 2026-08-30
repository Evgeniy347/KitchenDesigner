using System;
using KitchenDesigner.Core.Analysis;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class IssueTableView
    {
        public const float RowH = 24f;
        public const float RowStep = 26f;
        public const float ScrollbarW = 14f;
        public const float HeaderTopY = -102f;
        public const float ContentTopInset = 130f;
        public const float ContentBottomInset = 40f;

        private const float ColLevel = 0f;
        private const float ColCode = 110f;
        private const float ColDetail = 205f;
        private const float ColMessage = 470f;

        private static readonly Color RowZebra = UIStyle.Field;
        private static readonly Color RowTransparent = new Color(0, 0, 0, 0.01f);
        private static readonly Color ScrollTrack = new Color(0.10f, 0.10f, 0.13f, 0.6f);
        private static readonly Color ScrollHandle = new Color(0.38f, 0.40f, 0.46f, 1f);

        private float _contentW;
        private RectTransform? _content;
        private RectTransform? _viewport;
        private ScrollRect? _scroll;

        public int RowCount => _content != null ? _content.childCount : 0;

        public void Build(Transform parent, float panelW, float pad)
        {
            _contentW = panelW - pad * 2f - ScrollbarW;
            BuildHeader(parent, pad);
            BuildScrollArea(parent, pad);
        }

        public void ClearRows(Action<GameObject> destroy)
        {
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
                destroy(_content.GetChild(i).gameObject);
        }

        public void AddRow(AnalysisIssue iss, float y, int index,
            Action onClick, Action onDoubleClick, Action<GameObject> destroy)
        {
            var btn = UIFactory.CreateButton("Row", _content!, "", Vector2.zero,
                new Vector2(_contentW, RowH), onClick);
            var dbl = btn.gameObject.AddComponent<ListRowDoubleClick>();
            dbl.OnDoubleClick = () => onDoubleClick();
            var row = btn.GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(0, RowH);

            Color baseColor = index % 2 == 1 ? RowZebra : RowTransparent;
            btn.GetComponent<Image>().color = baseColor;

            var autoLabel = btn.transform.Find("Row_Label");
            if (autoLabel != null) destroy(autoLabel.gameObject);

            var meta = btn.gameObject.AddComponent<RowMeta>();
            meta.BaseColor = baseColor;
            meta.Issue = iss;

            Cell(row, IssueDisplay.LevelName(iss.Level), ColLevel, ColCode - ColLevel,
                IssueDisplay.LevelColor(iss.Level));
            Cell(row, iss.Code, ColCode, ColDetail - ColCode, UIStyle.Text);
            Cell(row, iss.Detail, ColDetail, ColMessage - ColDetail, UIStyle.Text);
            Cell(row, iss.Message, ColMessage, _contentW - ColMessage, UIStyle.Text);
        }

        public void SetContentHeight(float height)
        {
            if (_content != null) _content.sizeDelta = new Vector2(0, height);
        }

        public void RepaintHighlights(Func<AnalysisIssue, bool> isSelected)
        {
            if (_content == null) return;
            for (int i = 0; i < _content.childCount; i++)
            {
                var row = _content.GetChild(i);
                var meta = row.GetComponent<RowMeta>();
                if (meta == null) continue;
                var img = row.GetComponent<Image>();
                if (img != null)
                    img.color = isSelected(meta.Issue) ? UIStyle.RowSelected : meta.BaseColor;
            }
        }

        public int RowsPerPage()
        {
            if (_viewport == null) return 5;
            return Mathf.Max(1, Mathf.FloorToInt(_viewport.rect.height / RowStep));
        }

        public void ScrollRowIntoView(int visibleIdx, int visibleCount)
        {
            if (_scroll == null || _viewport == null || _content == null) return;
            if (visibleIdx < 0 || visibleCount == 0) return;

            float vpH = _viewport.rect.height;
            float contentH = _content.rect.height;
            if (contentH <= vpH + 1f) return;

            float total = contentH - vpH;
            if (total <= 0f) return;

            float desiredScroll = Mathf.Clamp(visibleIdx * RowStep, 0f, total);
            _scroll.verticalNormalizedPosition = 1f - desiredScroll / total;
        }

        private sealed class RowMeta : MonoBehaviour
        {
            public Color BaseColor;
            public AnalysisIssue Issue;
        }

        private void BuildHeader(Transform parent, float pad)
        {
            var header = UIFactory.CreateRect("ErrHeader", parent);
            UIFactory.AnchorTopLeft(header);
            header.sizeDelta = new Vector2(_contentW, 22);
            header.anchoredPosition = new Vector2(pad, HeaderTopY);

            HeaderCell(header, "Уровень", ColLevel, ColCode - ColLevel);
            HeaderCell(header, "Код", ColCode, ColDetail - ColCode);
            HeaderCell(header, "Деталь", ColDetail, ColMessage - ColDetail);
            HeaderCell(header, "Текст ошибки", ColMessage, _contentW - ColMessage);

            var sep = UIFactory.CreateRect("ErrHeaderSep", parent);
            UIFactory.AnchorTopLeft(sep);
            sep.sizeDelta = new Vector2(_contentW, 2);
            sep.anchoredPosition = new Vector2(pad, HeaderTopY - 22f);
            sep.gameObject.AddComponent<Image>().color = UIStyle.Separator;
        }

        private static void HeaderCell(RectTransform header, string text, float x, float w)
        {
            var lbl = UIFactory.CreateLabel("H_" + text, header, text, UIStyle.FontSmall,
                Vector2.zero, new Vector2(w, 22), TextAnchor.MiddleLeft);
            lbl.color = UIStyle.TextSecondary;
            var rt = lbl.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, 0);
        }

        private void BuildScrollArea(Transform parent, float pad)
        {
            var viewport = UIFactory.CreateRect("ErrViewport", parent);
            viewport.anchorMin = new Vector2(0, 0);
            viewport.anchorMax = new Vector2(1, 1);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(pad, ContentBottomInset);
            viewport.offsetMax = new Vector2(-(pad + ScrollbarW), -ContentTopInset);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = RowTransparent;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _content = UIFactory.CreateRect("ErrContent", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            _viewport = viewport;
            _scroll = scroll;

            var sbRect = UIFactory.CreateRect("ErrScrollbar", parent);
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 0.5f);
            sbRect.offsetMin = new Vector2(-(pad - 2f), ContentBottomInset);
            sbRect.offsetMax = new Vector2(-4, -ContentTopInset);
            sbRect.gameObject.AddComponent<Image>().color = ScrollTrack;
            var scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = UIFactory.CreateRect("Handle", sbRect);
            handle.sizeDelta = new Vector2(8, 100);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = ScrollHandle;
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static void Cell(RectTransform row, string text, float x, float w, Color color)
        {
            var lbl = UIFactory.CreateLabel("Cell", row, text, UIStyle.FontSmall,
                Vector2.zero, new Vector2(w, RowH), TextAnchor.MiddleLeft);
            lbl.color = color;
            lbl.raycastTarget = false;
            lbl.enableWordWrapping = false;
            lbl.overflowMode = TextOverflowModes.Ellipsis;
            var rt = lbl.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x + 4f, 0);
        }
    }
}
