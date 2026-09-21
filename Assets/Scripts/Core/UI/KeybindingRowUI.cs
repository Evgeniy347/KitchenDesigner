using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KitchenDesigner.Core.Keybinding;

namespace KitchenDesigner.Core.UI
{
    public sealed class KeybindingRowUI
    {
        private const float ContentW = SettingsRowFactory.ContentW;
        private const float RowH = 32f;
        private const float RowGap = 6f;
        private const float ClearBtnW = KeybindingCellLayout.ClearWidth;
        private const float HeaderH = 22f;
        private const int ChordFontMax = KeybindingCellLayout.MaxCaptionFontSize;

        private readonly KitchenSettings _settings;
        private readonly KeybindingCaptureGate _captureGate;
        private readonly KeybindingGestureGate _gestureGate;
        private readonly Action _afterChange;
        private readonly List<Cell> _cells = new();
        private readonly List<Node> _nodes = new();

        private float _topY;
        private KeybindingRowRuler _ruler;

        private sealed class Cell
        {
            public InputAction Action;
            public bool Primary;
            public Button Button = null!;
            public TextMeshProUGUI Label = null!;
            public GameObject Clear = null!;
            public GameObject Marker = null!;
        }

        private sealed class Node
        {
            public RectTransform Rect = null!;
            public TextMeshProUGUI? Label;
            public string Text = string.Empty;
            public bool HasHint;
            public float FixedHeight;
            public Cell? Primary;
            public Cell? Alt;
            public RectTransform? Hint;
            public TextMeshProUGUI[] ColumnLabels = Array.Empty<TextMeshProUGUI>();
        }

        public Action<float>? AfterRelayout { get; set; }

        public float BottomY { get; private set; }

        public KeybindingRowUI(KitchenSettings settings, KeybindingCaptureGate captureGate,
            KeybindingGestureGate gestureGate, Action afterChange)
        {
            _settings = settings;
            _captureGate = captureGate;
            _gestureGate = gestureGate;
            _afterChange = afterChange;
        }

        private KeyBindings Bindings => _settings.KeyBindings;

        public void Build(Transform parent, ref float y)
        {
            _cells.Clear();
            _nodes.Clear();
            _topY = y;
            AdoptWidthsForTheCurrentBindings();

            BuildColumnHeader(parent);

            foreach (var row in KeybindingRowList.Build())
            {
                if (row.IsGroupHeader) BuildGroupHeader(parent, row.Group);
                else BuildActionRow(parent, row.Action);
            }

            y = LayOut();
            RefreshAll();
        }

        private void AdoptWidthsForTheCurrentBindings() =>
            _ruler = KeybindingRowRuler.For(ContentW, KeybindingCaption.LongestBoundLength(Bindings));

        private void BuildColumnHeader(Transform parent)
        {
            var rowRect = UIFactory.CreateRect("KbColHdr", parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);

            var action = ColumnLabel("KbColHdrAction", rowRect, "Действие", TextAnchor.MiddleLeft);
            var primary = ColumnLabel("KbColHdrPrimary", rowRect, "Основная", TextAnchor.MiddleCenter);
            var alt = ColumnLabel("KbColHdrAlt", rowRect, "Альтернативная", TextAnchor.MiddleCenter);

            _nodes.Add(new Node
            {
                Rect = rowRect,
                FixedHeight = RowH,
                ColumnLabels = new[] { action, primary, alt },
            });
        }

        private static TextMeshProUGUI ColumnLabel(string name, Transform parent, string text,
            TextAnchor align)
        {
            var label = UIFactory.CreateLabel(name, parent, text, UIStyle.FontSmall,
                Vector2.zero, new Vector2(10f, RowH), align);
            label.color = UIStyle.TextSecondary;
            return label;
        }

        private void BuildGroupHeader(Transform parent, InputActionGroup group)
        {
            var header = UIFactory.CreateSectionHeader(
                "KbGroup_" + group, parent, InputActionGroupTitles.Of(group), ContentW);
            _nodes.Add(new Node { Rect = header, FixedHeight = HeaderH });
        }

        private void BuildActionRow(Transform parent, InputAction action)
        {
            string name = InputActionCatalog.DisplayNameOf(action);
            bool hasHint = action == InputAction.PerfMonitorToggleRecording;

            var rowRect = UIFactory.CreateRect("KbRow_" + action, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);

            var label = UIFactory.CreateLabel("KbLbl_" + action, rowRect, name, UIStyle.FontSmall,
                Vector2.zero, new Vector2(10f, RowH), TextAnchor.UpperLeft);
            label.enableWordWrapping = true;

            var node = new Node
            {
                Rect = rowRect,
                Label = label,
                Text = name,
                HasHint = hasHint,
                Primary = BuildCell(rowRect, action, primary: true),
                Alt = BuildCell(rowRect, action, primary: false),
            };
            _nodes.Add(node);

            if (!hasHint) return;

            var badge = HintBadge.Attach(label.transform, Vector2.zero,
                hint: "settings.control.perfRecordingFile");
            node.Hint = (RectTransform)badge.transform;
        }

        private Cell BuildCell(Transform rowRect, InputAction action, bool primary)
        {
            string suffix = primary ? "_P" : "_A";

            var button = UIFactory.CreateButton("KbBtn_" + action + suffix, rowRect,
                string.Empty, Vector2.zero, new Vector2(10f, RowH - 4f), null);
            var caption = button.GetComponentInChildren<TextMeshProUGUI>();
            if (caption != null)
            {
                caption.enableWordWrapping = false;
                caption.overflowMode = TextOverflowModes.Ellipsis;
            }

            var cell = new Cell
            {
                Action = action,
                Primary = primary,
                Button = button,
                Label = caption!,
            };
            _cells.Add(cell);
            cell.Marker = BuildMarker(rowRect, "KbMark_" + action + suffix);
            button.onClick.AddListener(() => BeginCapture(cell));
            TooltipUI.Attach(button.gameObject, () => CellTooltip(cell));

            var clear = UIFactory.CreateButton("KbClr_" + action + suffix, rowRect,
                UIStyle.GlyphClose, Vector2.zero, new Vector2(ClearBtnW, RowH - 4f),
                () => ClearCell(cell));
            var clearCaption = clear.GetComponentInChildren<TextMeshProUGUI>();
            if (clearCaption != null)
            {
                clearCaption.fontSize = 12;
                clearCaption.color = UIStyle.TextSecondary;
            }
            TooltipUI.Attach(clear.gameObject, "Очистить");
            cell.Clear = clear.gameObject;

            return cell;
        }

        private static GameObject BuildMarker(Transform rowRect, string name)
        {
            var marker = UIFactory.CreateLabel(name, rowRect,
                KeybindingCaption.MarkerText, ChordFontMax, Vector2.zero,
                new Vector2(KeybindingCellLayout.MarkerLaneWidth, RowH - 4f),
                TextAnchor.MiddleCenter);
            marker.color = UIStyle.HighlightError;
            marker.raycastTarget = false;
            marker.gameObject.SetActive(false);
            return marker.gameObject;
        }

        private float LayOut()
        {
            float y = _topY;

            foreach (var node in _nodes)
            {
                float height = node.FixedHeight > 0f ? node.FixedHeight : RowHeightOf(node);
                node.Rect.sizeDelta = new Vector2(ContentW, height);
                node.Rect.anchoredPosition = new Vector2(0f, y - height * 0.5f);

                if (node.ColumnLabels.Length == 3) LayOutColumnHeader(node);
                else if (node.Label != null) LayOutActionRow(node, height);

                y -= height + RowGap;
            }

            BottomY = y;
            return y;
        }

        private void LayOutColumnHeader(Node node)
        {
            Place(node.ColumnLabels[0], _ruler.LabelLeft, _ruler.LabelWidth, RowH);
            Place(node.ColumnLabels[1], _ruler.ColumnHeaderLeft(primary: true),
                _ruler.ColumnHeaderWidth, RowH);
            Place(node.ColumnLabels[2], _ruler.ColumnHeaderLeft(primary: false),
                _ruler.ColumnHeaderWidth, RowH);
        }

        private void LayOutActionRow(Node node, float height)
        {
            float textW = KeybindingCellLayout.LabelTextWidth(_ruler.LabelWidth, node.HasHint);
            Place(node.Label!, _ruler.LabelLeft, textW, height);

            if (node.Hint != null)
                node.Hint.anchoredPosition =
                    new Vector2(KeybindingCellLayout.HintBadgeCentreX(textW), 0f);

            PlaceCell(node.Primary!);
            PlaceCell(node.Alt!);
        }

        private void PlaceCell(Cell cell)
        {
            Place((RectTransform)cell.Marker.transform,
                _ruler.MarkerLeft(cell.Primary), _ruler.MarkerWidth, RowH - 4f);
            Place((RectTransform)cell.Button.transform,
                _ruler.CellLeft(cell.Primary), _ruler.CellWidth, RowH - 4f);
            Place((RectTransform)cell.Clear.transform,
                _ruler.ClearLeft(cell.Primary), _ruler.ClearWidth, RowH - 4f);
        }

        private static void Place(TMP_Text label, float left, float width, float height) =>
            Place(label.rectTransform, left, width, height);

        private static void Place(RectTransform rect, float left, float width, float height)
        {
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left + width * 0.5f, 0f);
        }

        private float RowHeightOf(Node node)
        {
            if (node.Label == null) return RowH;
            float textW = KeybindingCellLayout.LabelTextWidth(_ruler.LabelWidth, node.HasHint);
            float preferred = node.Label.GetPreferredValues(node.Text, textW, 0f).y;
            return Mathf.Max(RowH, preferred);
        }

        private void BeginCapture(Cell cell)
        {
            if (WantsAGesture(cell.Action)) BeginGestureCapture(cell);
            else BeginKeyCapture(cell);
        }

        private static bool WantsAGesture(InputAction action) =>
            InputActionCatalog.GroupOf(action) == InputActionGroup.Mouse;

        private void BeginKeyCapture(Cell cell)
        {
            _captureGate.Begin(result =>
            {
                if (!result.WasCancelled) Commit(cell, InputBinding.FromKey(result.Chord));
                RefreshAll();
                _afterChange();
            });

            if (_captureGate.IsCapturing) ShowWaiting(cell);
        }

        private void BeginGestureCapture(Cell cell)
        {
            _gestureGate.Begin(result =>
            {
                if (!result.WasCancelled) Commit(cell, InputBinding.FromGesture(result.Gesture));
                RefreshAll();
                _afterChange();
            });

            if (_gestureGate.IsCapturing) ShowWaiting(cell);
        }

        private static void ShowWaiting(Cell cell)
        {
            cell.Label.text = "...";
            cell.Button.GetComponent<Image>().color = UIStyle.Accent;
        }

        private void ClearCell(Cell cell)
        {
            Commit(cell, InputBinding.Empty);
            RefreshAll();
            _afterChange();
        }

        private void Commit(Cell cell, InputBinding binding)
        {
            var before = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
            if (before == binding) return;

            var command = new SetInputBindingCommand(Bindings, cell.Action, cell.Primary, before, binding);
            CommandStack.Execute(command);
        }

        public void RefreshAll()
        {
            float wasCell = _ruler.CellWidth;
            AdoptWidthsForTheCurrentBindings();
            if (!Mathf.Approximately(wasCell, _ruler.CellWidth) && _nodes.Count > 0)
                AfterRelayout?.Invoke(LayOut());

            var conflicts = Bindings.FindConflicts();
            foreach (var cell in _cells)
            {
                var binding = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
                bool inConflict = KeybindingConflicts.IsInConflict(conflicts, binding);
                string text = KeybindingCaption.CellText(binding);

                cell.Label.text = text;
                cell.Label.color = BindingColor(binding, inConflict);
                cell.Button.GetComponent<Image>().color = UIFactory.ButtonColor;
                cell.Clear.SetActive(!binding.IsEmpty);
                ShowTheMarker(cell, inConflict, text);
            }
        }

        private void ShowTheMarker(Cell cell, bool inConflict, string text)
        {
            cell.Marker.SetActive(inConflict);
            cell.Label.fontSize = KeybindingCellLayout.FontSizeFor(text.Length, _ruler.CellWidth);
        }

        private static Color BindingColor(InputBinding binding, bool inConflict)
        {
            if (inConflict) return UIStyle.HighlightError;
            return binding.IsEmpty ? UIStyle.TextSecondary : UIStyle.Text;
        }

        private string CellTooltip(Cell cell)
        {
            var binding = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
            var conflicts = Bindings.FindConflicts();
            string others = KeybindingConflicts.DescribeOthers(conflicts, cell.Action, binding);
            if (!string.IsNullOrEmpty(others))
                return InputBindingDisplay.Of(binding) + " — конфликт с: " + others;

            string how = WantsAGesture(cell.Action)
                ? "Нажмите, затем сделайте жест мышью. Esc отменяет."
                : "Нажмите, затем клавишу. Esc отменяет.";
            return binding.IsEmpty ? how : InputBindingDisplay.Of(binding) + ". " + how;
        }
    }
}
