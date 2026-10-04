using System;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsControlTab
    {
        internal const string TableNode = "KbTable";

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

        private readonly SettingsPage _page;
        private KeybindingRowUI? _bindings;
        private RectTransform? _table;
        private Action _afterChange = () => { };

        public SettingsControlTab(SettingsPage page) => _page = page;

        public void Build(KitchenSettings s, KeybindingCaptureGate captureGate,
            KeybindingGestureGate gestureGate, Action afterBindingChange)
        {
            _afterChange = afterBindingChange;
            _page.OnReset = ResetSection;

            _page.Section(Loc.T("settings.control.section.mouse"));

            _page.AddSlider(Loc.T("settings.control.mouseSensitivity"), KitchenSettings.MIN_INPUT_SPEED,
                KitchenSettings.MAX_INPUT_SPEED, s.MouseSensitivity, Multiplier, v => s.MouseSensitivity = v,
                false, () => s.MouseSensitivity);
            Hint(Loc.T("settings.control.mouseSensitivity"), hint: "settings.control.mouseSensitivity");
            _page.AddSlider(Loc.T("settings.control.wasdSpeed"), KitchenSettings.MIN_INPUT_SPEED,
                KitchenSettings.MAX_INPUT_SPEED, s.WasdSpeed, Multiplier, v => s.WasdSpeed = v,
                false, () => s.WasdSpeed);
            Hint(Loc.T("settings.control.wasdSpeed"), hint: "settings.control.wasdSpeed");
            _page.AddSlider(Loc.T("settings.control.arrowSpeed"), KitchenSettings.MIN_INPUT_SPEED,
                KitchenSettings.MAX_INPUT_SPEED, s.ArrowSpeed, Multiplier, v => s.ArrowSpeed = v,
                false, () => s.ArrowSpeed);
            Hint(Loc.T("settings.control.arrowSpeed"), hint: "settings.control.arrowSpeed");

            _page.AddSwitch(InvertYLabel, s.MouseInvertY,
                v => SetSettingCommand.Push(InvertYLabel, x => s.MouseInvertY = x,
                    s.MouseInvertY, v, _page.Form.ReadBackFromSettings),
                read: () => s.MouseInvertY);
            Hint(InvertYLabel, hint: "settings.control.mouseInvertY");

            _page.AddSwitch(InvertXLabel, s.MouseInvertX,
                v => SetSettingCommand.Push(InvertXLabel, x => s.MouseInvertX = x,
                    s.MouseInvertX, v, _page.Form.ReadBackFromSettings),
                read: () => s.MouseInvertX);
            Hint(InvertXLabel, hint: "settings.control.mouseInvertX");

            _page.Section(Loc.T("settings.control.section.fixed"));
            foreach (var line in ReferenceLines) _page.Note("KbRef_" + line.GetHashCode(), line);

            var header = _page.Section(Loc.T("settings.control.section.hotkeys"));
            _page.SectionAction(header, Loc.T("settings.control.resetHotkeys"), ResetHotkeys);

            BuildTable(s, captureGate, gestureGate);
        }

        public void RefreshConflicts() => _bindings?.RefreshAll();

        private void BuildTable(KitchenSettings s, KeybindingCaptureGate captureGate,
            KeybindingGestureGate gestureGate)
        {
            _table = UIFactory.CreateRect(TableNode, _page.Root);
            var origin = UIFactory.CreateRect("KbOrigin", _table);
            origin.anchorMin = origin.anchorMax = origin.pivot = new Vector2(0.5f, 1f);
            origin.sizeDelta = Vector2.zero;
            origin.anchoredPosition = Vector2.zero;

            _bindings = new KeybindingRowUI(s, captureGate, gestureGate, _afterChange);
            float y = 0f;
            _bindings.Build(origin, ref y);
            _bindings.AfterRelayout = Resize;

            _page.SetTail(_table, _bindings.SearchText);
            Resize(y);
        }

        private void Resize(float bottomY)
        {
            if (_table == null) return;
            _table.sizeDelta = new Vector2(SettingsPage.ContentW, -bottomY);
            _page.Relayout();
            _afterChange();
        }

        private void ResetHotkeys() =>
            SettingsSectionReset.Run(Loc.T("settings.control.resetHotkeys"),
                (x, defaults) => x.ResetKeyBindings(), AfterReset);

        private void ResetSection() =>
            SettingsSectionReset.Run(Loc.T("settings.reset.section"),
                (x, defaults) =>
                {
                    x.MouseSensitivity = defaults.MouseSensitivity;
                    x.WasdSpeed = defaults.WasdSpeed;
                    x.ArrowSpeed = defaults.ArrowSpeed;
                    x.MouseInvertX = defaults.MouseInvertX;
                    x.MouseInvertY = defaults.MouseInvertY;
                    x.ResetKeyBindings();
                }, AfterReset);

        private void AfterReset()
        {
            _page.Form.ReadBackFromSettings();
            _bindings?.RefreshAll();
            _afterChange();
        }

        private static string Multiplier(float v) => NumberFormat.Fixed(v, 1) + "×";

        private void Hint(string rowKey, string hint) => _page.Hint(rowKey, hint);
    }
}
