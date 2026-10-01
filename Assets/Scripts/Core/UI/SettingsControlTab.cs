using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsControlTab
    {
        private const float ReferenceLineH = 20f;
        private const float SectionGap = 16f;
        private static string InvertYLabel => Loc.T("settings.control.mouseInvertY");
        private static string InvertXLabel => Loc.T("settings.control.mouseInvertX");

        private static readonly LocalizedCache<string[]> ReferenceLinesCache =
            new LocalizedCache<string[]>(() => new string[] {
            Loc.T("settings.control.ref.panLmb"),
            Loc.T("settings.control.ref.ctrlSnap"),
            Loc.T("settings.control.ref.escapeOrder"),
            Loc.T("settings.control.ref.catalogKeys"),
            Loc.T("settings.control.ref.catalogFocus"),
        });

        private static string[] ReferenceLines => ReferenceLinesCache.Value;

        private readonly SettingsRowFactory _rows;
        private readonly List<RectTransform> _referenceRects = new();
        private KeybindingRowUI? _bindings;
        private float _referenceTop;

        public SettingsControlTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY,
            KeybindingCaptureGate captureGate, KeybindingGestureGate gestureGate,
            System.Action afterBindingChange)
        {
            float y = topY;

            _rows.AddSpeedSlider(page, ref y, Loc.T("settings.control.mouseSensitivity"), s.MouseSensitivity,
                v => s.MouseSensitivity = v, read: () => s.MouseSensitivity);
            Hint(Loc.T("settings.control.mouseSensitivity"), hint: "settings.control.mouseSensitivity");
            _rows.AddSpeedSlider(page, ref y, Loc.T("settings.control.wasdSpeed"), s.WasdSpeed,
                v => s.WasdSpeed = v, read: () => s.WasdSpeed);
            Hint(Loc.T("settings.control.wasdSpeed"), hint: "settings.control.wasdSpeed");
            _rows.AddSpeedSlider(page, ref y, Loc.T("settings.control.arrowSpeed"), s.ArrowSpeed,
                v => s.ArrowSpeed = v, read: () => s.ArrowSpeed);
            Hint(Loc.T("settings.control.arrowSpeed"), hint: "settings.control.arrowSpeed");

            _rows.AddToggle(page, ref y, InvertYLabel, s.MouseInvertY,
                v => SetSettingCommand.Push(InvertYLabel, x => s.MouseInvertY = x,
                    s.MouseInvertY, v, _rows.ReadBackFromSettings),
                read: () => s.MouseInvertY);
            Hint(InvertYLabel, hint: "settings.control.mouseInvertY");

            _rows.AddToggle(page, ref y, InvertXLabel, s.MouseInvertX,
                v => SetSettingCommand.Push(InvertXLabel, x => s.MouseInvertX = x,
                    s.MouseInvertX, v, _rows.ReadBackFromSettings),
                read: () => s.MouseInvertX);
            Hint(InvertXLabel, hint: "settings.control.mouseInvertX");

            y -= SectionGap;
            var header = UIFactory.CreateSectionHeader(
                "KbSection", page, Loc.T("settings.control.section.hotkeys"), SettingsRowFactory.ContentW);
            header.anchoredPosition = new Vector2(0f, y - 9f);
            y -= 18f + SettingsRowFactory.GapPx;

            _bindings = new KeybindingRowUI(s, captureGate, gestureGate, afterBindingChange);
            _bindings.Build(page, ref y);
            _bindings.AfterRelayout = ShiftReferenceBlock;

            y -= SectionGap;
            _referenceTop = y;
            BuildReferenceBlock(page, ref y);
        }

        private void ShiftReferenceBlock(float listBottom)
        {
            float delta = listBottom - SectionGap - _referenceTop;
            if (Mathf.Approximately(delta, 0f)) return;

            foreach (var rect in _referenceRects)
                rect.anchoredPosition += new Vector2(0f, delta);

            _referenceTop += delta;
        }

        public void RefreshConflicts() => _bindings?.RefreshAll();

        private void BuildReferenceBlock(Transform page, ref float y)
        {
            var header = UIFactory.CreateSectionHeader(
                "KbRefSection", page, Loc.T("settings.control.section.fixed"),
                SettingsRowFactory.ContentW);
            header.anchoredPosition = new Vector2(0f, y - 9f);
            _referenceRects.Add(header);
            y -= 18f + SettingsRowFactory.GapPx;

            foreach (var line in ReferenceLines)
            {
                var rect = UIFactory.CreateRect("KbRef_" + line.GetHashCode(), page);
                rect.sizeDelta = new Vector2(SettingsRowFactory.ContentW, ReferenceLineH);

                var label = UIFactory.CreateLabel("KbRefLbl_" + line.GetHashCode(), rect, line,
                    UIStyle.FontSmall, Vector2.zero, new Vector2(SettingsRowFactory.ContentW, ReferenceLineH),
                    TextAnchor.UpperLeft);
                label.color = UIStyle.TextSecondary;
                label.enableWordWrapping = true;

                float height = Mathf.Max(ReferenceLineH,
                    label.GetPreferredValues(line, SettingsRowFactory.ContentW, 0f).y);
                rect.sizeDelta = new Vector2(SettingsRowFactory.ContentW, height);
                rect.anchoredPosition = new Vector2(0f, y - height * 0.5f);
                label.rectTransform.sizeDelta = new Vector2(SettingsRowFactory.ContentW, height);
                _referenceRects.Add(rect);

                y -= height + 4f;
            }
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);
    }
}
