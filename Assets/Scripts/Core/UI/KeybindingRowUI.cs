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
        private const float LabelW = 220f;
        private const float ChordBtnW = 96f;
        private const float ClearBtnW = 18f;
        private const float GapSmall = 3f;
        private const float GapMed = 6f;
        private const float GapGroup = 10f;
        private const float HeaderH = 22f;
        private const int ChordFontMax = 14;
        private const int ChordFontMin = 8;

        private readonly KitchenSettings _settings;
        private readonly KeybindingCaptureGate _captureGate;
        private readonly KeybindingGestureGate _gestureGate;
        private readonly Action _afterChange;
        private readonly List<Cell> _cells = new();

        private sealed class Cell
        {
            public InputAction Action;
            public bool Primary;
            public Button Button = null!;
            public TextMeshProUGUI Label = null!;
            public GameObject Clear = null!;
        }

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
            BuildColumnHeader(parent, ref y);

            foreach (var row in KeybindingRowList.Build())
            {
                if (row.IsGroupHeader) BuildGroupHeader(parent, ref y, row.Group);
                else BuildActionRow(parent, ref y, row.Action);
            }

            RefreshAll();
        }

        private void BuildColumnHeader(Transform parent, ref float y)
        {
            var rowRect = UIFactory.CreateRect("KbColHdr", parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0f, y - RowH * 0.5f);

            CreateColumnLabel("KbColHdrAction", rowRect, "Действие", -ContentW * 0.5f, LabelW,
                TextAnchor.MiddleLeft);

            float x = -ContentW * 0.5f + LabelW + GapMed;
            CreateColumnLabel("KbColHdrPrimary", rowRect, "Основная", x, ChordBtnW + GapSmall + ClearBtnW,
                TextAnchor.MiddleCenter);

            x += ChordBtnW + GapSmall + ClearBtnW + GapGroup;
            CreateColumnLabel("KbColHdrAlt", rowRect, "Альтернативная", x, ChordBtnW + GapSmall + ClearBtnW,
                TextAnchor.MiddleCenter);

            y -= RowH + RowGap;
        }

        private static void CreateColumnLabel(string name, Transform parent, string text, float left,
            float width, TextAnchor align)
        {
            var label = UIFactory.CreateLabel(name, parent, text, UIStyle.FontSmall,
                new Vector2(left + width * 0.5f, 0f), new Vector2(width, RowH), align);
            label.color = UIStyle.TextSecondary;
        }

        private void BuildGroupHeader(Transform parent, ref float y, InputActionGroup group)
        {
            var header = UIFactory.CreateSectionHeader(
                "KbGroup_" + group, parent, InputActionGroupTitles.Of(group), ContentW);
            header.anchoredPosition = new Vector2(0f, y - HeaderH * 0.5f);
            y -= HeaderH + RowGap;
        }

        private void BuildActionRow(Transform parent, ref float y, InputAction action)
        {
            string name = InputActionCatalog.DisplayNameOf(action);

            var rowRect = UIFactory.CreateRect("KbRow_" + action, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0f, y);

            var label = UIFactory.CreateLabel("KbLbl_" + action, rowRect, name, UIStyle.FontSmall,
                new Vector2(-ContentW * 0.5f + LabelW * 0.5f, 0f), new Vector2(LabelW, RowH),
                TextAnchor.UpperLeft);
            label.enableWordWrapping = true;
            float rowHeight = Mathf.Max(RowH, label.GetPreferredValues(name, LabelW, 0f).y);

            rowRect.sizeDelta = new Vector2(ContentW, rowHeight);
            rowRect.anchoredPosition = new Vector2(0f, y - rowHeight * 0.5f);
            label.rectTransform.sizeDelta = new Vector2(LabelW, rowHeight);

            float x = -ContentW * 0.5f + LabelW + GapMed;
            BuildCell(rowRect, action, primary: true, ref x);
            x += GapGroup;
            BuildCell(rowRect, action, primary: false, ref x);

            if (action == InputAction.PerfMonitorToggleRecording)
                HintBadge.AttachAfterLabel(label, hint: "settings.control.perfRecordingFile");

            y -= rowHeight + RowGap;
        }

        private void BuildCell(Transform rowRect, InputAction action, bool primary, ref float x)
        {
            var button = UIFactory.CreateButton("KbBtn_" + action + (primary ? "_P" : "_A"), rowRect,
                string.Empty, new Vector2(x + ChordBtnW * 0.5f, 0f), new Vector2(ChordBtnW, RowH - 4f), null);
            var caption = button.GetComponentInChildren<TextMeshProUGUI>();
            if (caption != null)
            {
                caption.enableAutoSizing = true;
                caption.fontSizeMin = ChordFontMin;
                caption.fontSizeMax = ChordFontMax;
                caption.enableWordWrapping = false;
            }

            var cell = new Cell { Action = action, Primary = primary, Button = button, Label = caption! };
            _cells.Add(cell);
            button.onClick.AddListener(() => BeginCapture(cell));
            TooltipUI.Attach(button.gameObject, () => ConflictTooltip(cell));

            x += ChordBtnW + GapSmall;

            var clear = UIFactory.CreateButton("KbClr_" + action + (primary ? "_P" : "_A"), rowRect,
                UIStyle.GlyphClose, new Vector2(x + ClearBtnW * 0.5f, 0f), new Vector2(ClearBtnW, RowH - 4f),
                () => ClearCell(cell));
            var clearCaption = clear.GetComponentInChildren<TextMeshProUGUI>();
            if (clearCaption != null)
            {
                clearCaption.fontSize = 12;
                clearCaption.color = UIStyle.TextSecondary;
            }
            TooltipUI.Attach(clear.gameObject, "Очистить");
            cell.Clear = clear.gameObject;

            x += ClearBtnW;
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
            var conflicts = Bindings.FindConflicts();
            foreach (var cell in _cells)
            {
                var binding = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
                bool inConflict = KeybindingConflicts.IsInConflict(conflicts, binding);

                cell.Label.text = BindingText(binding, inConflict);
                cell.Label.color = BindingColor(binding, inConflict);
                cell.Button.GetComponent<Image>().color = UIFactory.ButtonColor;
                cell.Clear.SetActive(!binding.IsEmpty);
            }
        }

        private static string BindingText(InputBinding binding, bool inConflict)
        {
            string text = binding.IsEmpty ? "—" : InputBindingDisplay.Of(binding);
            return inConflict ? "! " + text : text;
        }

        private static Color BindingColor(InputBinding binding, bool inConflict)
        {
            if (inConflict) return UIStyle.HighlightError;
            return binding.IsEmpty ? UIStyle.TextSecondary : UIStyle.Text;
        }

        private string ConflictTooltip(Cell cell)
        {
            var binding = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
            var conflicts = Bindings.FindConflicts();
            string others = KeybindingConflicts.DescribeOthers(conflicts, cell.Action, binding);
            if (!string.IsNullOrEmpty(others)) return "Конфликт с: " + others;

            return WantsAGesture(cell.Action)
                ? "Нажмите, затем сделайте жест мышью. Esc отменяет."
                : "Нажмите, затем клавишу. Esc отменяет.";
        }
    }
}
