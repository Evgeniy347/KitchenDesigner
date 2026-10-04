using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsViewTab
    {
        public static string WallOutlineId => Loc.T("settings.view.wallOutline");
        public static string ObjectOutlineId => Loc.T("settings.view.objectOutline");

        internal const string PresetNode = "ViewPreset";

        private static readonly LocalizedCache<string[]> PresetCaptionsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("settings.view.presetNormal"), Loc.T("settings.view.presetRoom"), Loc.T("settings.view.presetPhoto") });

        private static string[] PresetCaptions => PresetCaptionsCache.Value;

        private static readonly EditMode[] PresetModes =
            { EditMode.Normal, EditMode.Room, EditMode.Photo };

        private readonly SettingsPage _page;
        private readonly Action _dependentStatesChanged;

        private readonly Dictionary<ViewField, Toggle> _toggles = new();
        private readonly Dictionary<ViewField, string> _toggleKeys = new();
        private SegmentedControl? _presets;
        private int _presetTab;

        public SettingsViewTab(SettingsPage page, Action dependentStatesChanged)
        {
            _page = page;
            _dependentStatesChanged = dependentStatesChanged;
        }

        private EditMode EditedPresetMode => PresetModes[_presetTab];

        public void Build()
        {
            _page.Section(Loc.T("settings.view.section.preset"));
            BuildPresetSwitch();

            _page.Section(Loc.T("settings.view.section.scene"));
            AddViewToggle(ViewField.Walls, Loc.T("settings.view.walls"), Loc.T("settings.view.walls"), 0);
            Hint(Loc.T("settings.view.walls"), hint: "settings.view.walls");
            AddViewToggle(ViewField.WallOutline, Loc.T("settings.view.outline"), WallOutlineId, 1);
            Hint(WallOutlineId, hint: "settings.view.wallOutline");
            AddViewToggle(ViewField.LowerNearWalls, Loc.T("settings.view.lowerNearWalls"),
                Loc.T("settings.view.lowerNearWalls"), 1);
            Hint(Loc.T("settings.view.lowerNearWalls"), hint: "settings.view.lowerNearWalls");
            AddViewToggle(ViewField.LowerAllWalls, Loc.T("settings.view.lowerAllWalls"),
                Loc.T("settings.view.lowerAllWalls"), 2);
            Hint(Loc.T("settings.view.lowerAllWalls"), hint: "settings.view.lowerAllWalls");
            AddViewToggle(ViewField.HideOpeningsOnLoweredWalls, Loc.T("settings.view.hideOpeningsOnLoweredWalls"),
                Loc.T("settings.view.hideOpeningsOnLoweredWalls"), 2);
            Hint(Loc.T("settings.view.hideOpeningsOnLoweredWalls"), hint: "settings.view.hideOpeningsOnLoweredWalls");

            AddViewToggle(ViewField.Objects, Loc.T("settings.view.objects"), Loc.T("settings.view.objects"), 0);
            Hint(Loc.T("settings.view.objects"), hint: "settings.view.objects");
            AddViewToggle(ViewField.ObjectOutline, Loc.T("settings.view.outline"), ObjectOutlineId, 1);
            Hint(ObjectOutlineId, hint: "settings.view.objectOutline");

            _page.Section(Loc.T("settings.view.section.lighting"));
            AddViewToggle(ViewField.HideLightSources, Loc.T("settings.view.hideLightSources"),
                Loc.T("settings.view.hideLightSources"), 0);
            Hint(Loc.T("settings.view.hideLightSources"), hint: "settings.view.hideLightSources");

            EditModeManager.Changed -= FollowEditMode;
            EditModeManager.Changed += FollowEditMode;
        }

        public void Dispose() => EditModeManager.Changed -= FollowEditMode;

        public void FollowEditMode()
        {
            _presetTab = CurrentModeTab;
            _dependentStatesChanged();
        }

        private static int CurrentModeTab => Array.IndexOf(PresetModes, EditModeManager.Mode);

        public void Refresh()
        {
            var edited = EditedPresetMode;
            var state = ViewResolver.Resolve(edited);

            if (_presets != null)
            {
                _presets.SetValueWithoutNotify(_presetTab);
                int currentIdx = CurrentModeTab;
                for (int i = 0; i < _presets.Segments.Count; i++)
                {
                    var button = _presets.Segments[i];
                    if (button == null) continue;
                    var caption = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (caption != null) caption.text = PresetCaptions[i];
                    PresetCurrentMark.Set(button, i == currentIdx);
                }
            }

            foreach (var kv in _toggles)
            {
                var field = kv.Key;
                var toggle = kv.Value;
                if (toggle == null) continue;

                SwitchControl.SetWithoutNotify(toggle, state.Get(field));

                var parent = ViewResolver.ParentOf(field);
                bool parentOn = parent == null || state.Get(parent.Value);
                bool enabled = parentOn && ViewResolver.IsEditable(edited, field);
                _page.Form.SetToggleEnabled(toggle, _toggleKeys[field], enabled);
            }
        }

        private void BuildPresetSwitch()
        {
            var row = UIFactory.CreateRect("Row_" + PresetNode, _page.Root);
            float stripW = _page.Rows.Metrics.Width - HintBadge.LaneWidth;
            _presets = SegmentedControl.Create(PresetNode, row, PresetCaptions, _presetTab, stripW,
                UIStyle.ControlH, SwitchPreset);
            var strip = (RectTransform)_presets.transform;
            strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0f, 0.5f);
            strip.anchoredPosition = Vector2.zero;
            foreach (var segment in _presets.Segments) PresetCurrentMark.Attach(segment);

            _page.Block(row, UIStyle.ControlH, string.Join(" ", PresetCaptions));
            float width = _page.Rows.Metrics.Width;
            HintBadge.Attach(row, new Vector2((width - UIStyle.HintBadgeSize) * 0.5f, 0f),
                hint: "settings.view.preset");
        }

        private void Hint(string rowKey, string hint) => _page.Hint(rowKey, hint);

        private void SwitchPreset(int index)
        {
            _presetTab = index;
            _dependentStatesChanged();
        }

        private void AddViewToggle(ViewField field, string label, string key, int indentLevel)
        {
            var toggle = _page.AddSwitch(label,
                ViewResolver.Resolve(EditedPresetMode).Get(field),
                v =>
                {
                    var s = KitchenSettings.Instance;
                    if (s == null) return;
                    ViewResolver.PresetFor(EditedPresetMode, s).Set(field, v);
                    SceneVisibilityManager.Invalidate();
                    _dependentStatesChanged();
                }, id: key, indent: indentLevel);
            _toggles[field] = toggle;
            _toggleKeys[field] = key;
        }
    }
}
