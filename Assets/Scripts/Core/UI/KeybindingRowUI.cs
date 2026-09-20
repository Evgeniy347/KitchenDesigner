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
        private readonly Action _afterChange;
        private readonly List<Cell> _cells = new();

        private sealed class Cell
        {
            public InputAction Action;
            public bool Primary;
            public Button Button = null!;
            public TextMeshProUGUI Label = null!;
        }

        public KeybindingRowUI(KitchenSettings settings, KeybindingCaptureGate captureGate, Action afterChange)
        {
            _settings = settings;
            _captureGate = captureGate;
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

            var clear = UIFactory.CreateDangerButton("KbClr_" + action + (primary ? "_P" : "_A"), rowRect,
                UIStyle.GlyphClose, new Vector2(x + ClearBtnW * 0.5f, 0f), new Vector2(ClearBtnW, RowH - 4f),
                () => ClearCell(cell));
            var clearCaption = clear.GetComponentInChildren<TextMeshProUGUI>();
            if (clearCaption != null) clearCaption.fontSize = 12;
            TooltipUI.Attach(clear.gameObject, "Очистить");

            x += ClearBtnW;
        }

        private void BeginCapture(Cell cell)
        {
            _captureGate.Begin(result =>
            {
                if (!result.WasCancelled) Commit(cell, result.Chord);
                RefreshAll();
                _afterChange();
            });

            if (!_captureGate.IsCapturing) return;
            cell.Label.text = "...";
            cell.Button.GetComponent<Image>().color = UIStyle.Accent;
        }

        private void ClearCell(Cell cell)
        {
            Commit(cell, KeyChord.Empty);
            RefreshAll();
            _afterChange();
        }

        private void Commit(Cell cell, KeyChord chord)
        {
            var before = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
            if (before == chord) return;

            var command = new SetKeyBindingCommand(Bindings, cell.Action, cell.Primary, before, chord);
            CommandStack.Execute(command);
        }

        public void RefreshAll()
        {
            var conflicts = Bindings.FindConflicts();
            foreach (var cell in _cells)
            {
                var chord = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
                bool inConflict = KeybindingConflicts.IsInConflict(conflicts, chord);

                cell.Label.text = ChordText(chord, inConflict);
                cell.Label.color = inConflict ? UIStyle.HighlightError : UIStyle.Text;
                cell.Button.GetComponent<Image>().color = UIFactory.ButtonColor;
            }
        }

        private static string ChordText(KeyChord chord, bool inConflict)
        {
            string text = chord.IsEmpty ? "—" : KeyChord.Format(chord);
            return inConflict ? "! " + text : text;
        }

        private string ConflictTooltip(Cell cell)
        {
            var chord = KeybindingEditing.Read(Bindings, cell.Action, cell.Primary);
            var conflicts = Bindings.FindConflicts();
            string others = KeybindingConflicts.DescribeOthers(conflicts, cell.Action, chord);
            return string.IsNullOrEmpty(others)
                ? "Нажмите, затем клавишу. Esc отменяет."
                : "Конфликт с: " + others;
        }
    }
}
