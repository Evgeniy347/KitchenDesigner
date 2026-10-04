using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class DataTable
    {
        public const string HeaderNode = "Header";
        public const string HeaderRuleNode = "HeaderRule";
        public const string RowNodePrefix = "Row_";
        public const string CellNodePrefix = "Cell_";
        public const string SortGlyphAscending = UIStyle.GlyphSortAscending;
        public const string SortGlyphDescending = UIStyle.GlyphSortDescending;

        private readonly IReadOnlyList<DataColumn> _columns;
        private readonly bool _selectable;
        private readonly float[] _widths;
        private readonly float[] _lefts;
        private readonly List<TMP_Text> _headerLabels = new();
        private readonly List<DataTableRow> _rowViews = new();
        private readonly List<float> _rowTops = new();
        private IReadOnlyList<DataRow> _source = Array.Empty<DataRow>();
        private DataRow[] _shown = Array.Empty<DataRow>();
        private EmptyState? _empty;
        private bool _measuredHidden;

        private DataTable(RectTransform root, RectTransform header, ScrollArea body,
            IReadOnlyList<DataColumn> columns, bool selectable, float[] widths)
        {
            Root = root;
            Header = header;
            Body = body;
            _columns = columns;
            _selectable = selectable;
            _widths = widths;
            _lefts = TableColumnLayout.Lefts(widths);
        }

        public RectTransform Root { get; }

        public RectTransform Header { get; }

        public ScrollArea Body { get; }

        public IReadOnlyList<DataColumn> Columns => _columns;

        public IReadOnlyList<DataRow> ShownRows => _shown;

        public int SortColumn { get; private set; } = -1;

        public bool SortDescending { get; private set; }

        public DataRow? Selected { get; private set; }

        public float RowHeight => _selectable ? UIStyle.TableRowInteractiveH : UIStyle.TableRowH;

        public float ContentHeight { get; private set; }

        public EmptyState? Empty => _empty;

        public Action<DataRow, RectTransform>? RowDecorator { get; set; }

        public Func<DataRow, bool>? SelectionSource { get; set; }

        public event Action<DataRow?>? SelectionChanged;

        public event Action<DataRow>? RowActivated;

        public static DataTable Create(Transform parent, string name, Vector2 size,
            IReadOnlyList<DataColumn> columns, bool selectable = false)
        {
            var root = UIFactory.CreateRect(name, parent);
            root.sizeDelta = size;

            float tableW = size.x - WindowBody.BarW;
            var widths = TableColumnLayout.Widths(columns.Select(c => c.IsFlexible ? 0f : c.Width).ToList(), tableW);

            var header = UIFactory.CreateRect(HeaderNode, root);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, UIStyle.TableHeaderH);
            header.anchoredPosition = Vector2.zero;
            Line(HeaderRuleNode, header, UIStyle.Separator, 0f);

            var body = ScrollArea.Create(name + "Body", root, WindowBody.BarW);
            var viewport = body.Viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(0f, -UIStyle.TableHeaderH);

            var table = new DataTable(root, header, body, columns, selectable, widths);
            table.BuildHeader();
            root.gameObject.AddComponent<DataTableShownHook>().Init(table.RemeasureIfBuiltHidden);
            return table;
        }

        public void SetRows(IReadOnlyList<DataRow> rows)
        {
            _source = rows;
            if (Selected != null && !rows.Contains(Selected)) Selected = null;
            Render();
        }

        public void SortBy(int column, bool descending)
        {
            SortColumn = column;
            SortDescending = descending;
            RefreshHeaderGlyphs();
            Render();
        }

        public void ToggleSort(int column)
        {
            if (column < 0 || column >= _columns.Count || !_columns[column].Sortable) return;
            SortBy(column, column == SortColumn && !SortDescending);
        }

        public void Select(DataRow? row)
        {
            if (row != null && (!_selectable || row.Kind != DataRowKind.Item || !row.Enabled)) return;
            Selected = row;
            RepaintSelection();
            SelectionChanged?.Invoke(Selected);
        }

        public void RepaintSelection()
        {
            foreach (var view in _rowViews) view.Paint(IsSelected(view.Row!));
        }

        public int RowsPerPage() => TableScroll.RowsPerPage(Body.Viewport.rect.height, RowHeight);

        public void ScrollIntoView(int shownIndex)
        {
            if (shownIndex < 0 || shownIndex >= _rowTops.Count) return;
            var content = Body.Content;
            float height = ((RectTransform)_rowViews[shownIndex].transform).sizeDelta.y;
            float offset = TableScroll.OffsetToReveal(content.anchoredPosition.y, _rowTops[shownIndex], height,
                Body.Viewport.rect.height, ContentHeight);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, offset);
        }

        private bool IsSelected(DataRow row) => SelectionSource != null ? SelectionSource(row) : row == Selected;

        public void Activate(DataRow row)
        {
            if (!row.Enabled) return;
            Select(row);
            RowActivated?.Invoke(row);
        }

        public void SetEmptyState(string title, string hint, string? actionCaption = null, Action? onAction = null)
        {
            if (_empty != null) Discard(_empty.Root.gameObject);
            _empty = EmptyState.Create(Root, "Empty", title, hint, actionCaption, onAction);
            var rt = _empty.Root;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -UIStyle.TableHeaderH);
            _empty.Root.gameObject.SetActive(_shown.Length == 0);
        }

        public TMP_Text HeaderLabel(int column) => _headerLabels[column];

        public RectTransform RowRect(int shownIndex) => (RectTransform)_rowViews[shownIndex].transform;

        public TMP_Text? CellLabel(int shownIndex, int column)
        {
            var cell = RowRect(shownIndex).Find(CellNodePrefix + column);
            return cell != null ? cell.GetComponent<TMP_Text>() : null;
        }

        public float ColumnLeft(int column) => _lefts[column];

        public float ColumnWidth(int column) => _widths[column];

        private void BuildHeader()
        {
            for (int c = 0; c < _columns.Count; c++)
            {
                int column = c;
                var label = Cell(Header, c, _columns[c].Header, UIStyle.FontSmall, UIStyle.TextSecondary,
                    FontStyles.Normal, UIStyle.TableHeaderH, _columns[c].Align);
                if (_columns[c].Sortable)
                {
                    label.raycastTarget = true;
                    label.gameObject.AddComponent<DataTableHeaderClick>().Init(() => ToggleSort(column));
                }
                _headerLabels.Add(label);
            }
        }

        private void RefreshHeaderGlyphs()
        {
            for (int c = 0; c < _columns.Count; c++)
            {
                string text = _columns[c].Header;
                if (c == SortColumn)
                    text = _columns[c].Align == CellAlign.Right
                        ? (SortDescending ? SortGlyphDescending : SortGlyphAscending) + " " + text
                        : text + " " + (SortDescending ? SortGlyphDescending : SortGlyphAscending);
                _headerLabels[c].text = text;
            }
        }

        private void RemeasureIfBuiltHidden()
        {
            if (_measuredHidden) Render();
        }

        private void Render()
        {
            _measuredHidden = !Root.gameObject.activeInHierarchy;
            foreach (var view in _rowViews) Discard(view.gameObject);
            _rowViews.Clear();
            _rowTops.Clear();

            _shown = Ordered().ToArray();
            float y = 0f;
            for (int i = 0; i < _shown.Length; i++)
            {
                var view = BuildRow(_shown[i], i, y);
                _rowTops.Add(y);
                y += ((RectTransform)view.transform).sizeDelta.y;
                _rowViews.Add(view);
            }
            ContentHeight = y;
            Body.ContentHeight = y;
            if (_empty != null) _empty.Root.gameObject.SetActive(_shown.Length == 0);
        }

        private IEnumerable<DataRow> Ordered()
        {
            if (SortColumn < 0) return _source;
            var order = TableSort.Order(
                _source.Select(r => r.Kind == DataRowKind.Item).ToList(),
                _source.Select(r => r.Kind == DataRowKind.Group).ToList(),
                i => _source[i].SortKey(SortColumn), SortDescending);
            return order.Select(i => _source[i]);
        }

        private DataTableRow BuildRow(DataRow row, int index, float y)
        {
            float h = row.Kind switch
            {
                DataRowKind.Group => UIStyle.TableGroupRowH,
                DataRowKind.Item => RowHeight,
                _ => UIStyle.TableRowH,
            };
            var rect = UIFactory.CreateRect(RowNodePrefix + index, Body.Content);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, h);
            rect.anchoredPosition = new Vector2(0f, -y);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = UIStyle.Transparent;

            var bar = UIFactory.CreateRect("SelectionBar", rect);
            LayoutDirection.PinToStartEdge(bar, UIStyle.SelectionBarW);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = UIStyle.SelectionBar;
            barImage.raycastTarget = false;
            bar.gameObject.SetActive(false);

            if (row.Kind == DataRowKind.Group)
            {
                var title = Cell(rect, 0, row.Cell(0), UIStyle.FontSection, UIStyle.TextSecondary, FontStyles.Bold, h,
                    CellAlign.Left, spanAll: true);
                title.alignment = LayoutDirection.IsRtl ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;
                Line("Rule", rect, UIStyle.Separator, 0f);
            }
            else
            {
                int size = row.Kind == DataRowKind.Subtotal ? UIStyle.FontSmall : UIStyle.FontBody;
                var color = row.Kind == DataRowKind.Subtotal ? UIStyle.TextSecondary
                    : row.Enabled ? UIStyle.Text : UIStyle.TextDisabled;
                var style = row.Kind == DataRowKind.Total ? FontStyles.Bold : FontStyles.Normal;
                for (int c = 0; c < _columns.Count; c++)
                    if (row.Cell(c).Length > 0)
                        Cell(rect, c, row.Cell(c), size, row.CellColor(c) ?? color, style, h, _columns[c].Align);
                if (row.Kind != DataRowKind.Total) Line("Divider", rect, UIStyle.Divider, 0f);
            }

            var view = rect.gameObject.AddComponent<DataTableRow>();
            view.Init(this, row, bg, bar.gameObject, _selectable && row.Kind == DataRowKind.Item && row.Enabled);
            view.Paint(IsSelected(row));
            RowDecorator?.Invoke(row, rect);
            return view;
        }

        private TextMeshProUGUI Cell(RectTransform parent, int column, string text, int size, Color color,
            FontStyles style, float height, CellAlign align, bool spanAll = false)
        {
            float left = spanAll ? 0f : _lefts[column];
            float width = spanAll ? _widths.Sum() : _widths[column];
            float pad = UIStyle.TableCellPadX;
            bool rtl = LayoutDirection.IsRtl;
            var anchor = rtl ? LayoutDirection.Mirror(Anchor(align)) : Anchor(align);
            var label = UIFactory.CreateLabel(CellNodePrefix + column, parent, text, size, Vector2.zero,
                new Vector2(Mathf.Max(0f, width - 2f * pad), height), anchor);
            if (rtl) RightToLeftLabel.AttachKeepingAlignment(label);
            label.color = color;
            label.fontStyle = style;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            var rt = label.rectTransform;
            float x = LayoutDirection.StartX(_widths.Sum(), left, width) + (rtl ? WindowBody.BarW : 0f);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x + pad, 0f);

            if (label.GetPreferredValues(text).x > rt.sizeDelta.x + 0.5f)
            {
                label.raycastTarget = true;
                TooltipUI.Attach(label.gameObject, text);
            }
            return label;
        }

        private static TextAnchor Anchor(CellAlign align) => align switch
        {
            CellAlign.Right => TextAnchor.MiddleRight,
            CellAlign.Center => TextAnchor.MiddleCenter,
            _ => TextAnchor.MiddleLeft,
        };

        private static void Line(string name, RectTransform parent, Color color, float bottom)
        {
            var line = UIFactory.CreateRect(name, parent);
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(0.5f, 0f);
            line.sizeDelta = new Vector2(0f, UIStyle.DividerPx);
            line.anchoredPosition = new Vector2(0f, bottom);
            var img = line.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static void Discard(GameObject go)
        {
            go.SetActive(false);
            DestroyNow.The(go);
        }
    }
}
