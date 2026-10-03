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

        private const float PresetRowH = 46f;

        private static readonly LocalizedCache<string[]> PresetCaptionsCache =
            new LocalizedCache<string[]>(() => new string[] { Loc.T("settings.view.presetNormal"), Loc.T("settings.view.presetRoom"), Loc.T("settings.view.presetPhoto") });

        private static string[] PresetCaptions => PresetCaptionsCache.Value;

        private static readonly EditMode[] PresetModes =
            { EditMode.Normal, EditMode.Room, EditMode.Photo };

        private readonly SettingsRowFactory _rows;
        private readonly Action _dependentStatesChanged;

        private readonly List<Button> _presetButtons = new();
        private readonly Dictionary<ViewField, Toggle> _toggles = new();
        private readonly Dictionary<ViewField, string> _toggleKeys = new();
        private int _presetTab;

        public SettingsViewTab(SettingsRowFactory rows, Action dependentStatesChanged)
        {
            _rows = rows;
            _dependentStatesChanged = dependentStatesChanged;
        }

        private EditMode EditedPresetMode => PresetModes[_presetTab];

        public void Build(Transform page, float topY)
        {
            float y = topY;

            BuildPresetSwitch(page, ref y);
            y -= SettingsRowFactory.GapPx;

            AddViewToggle(page, ref y, ViewField.Walls, Loc.T("settings.view.walls"), Loc.T("settings.view.walls"), 0);
            Hint(Loc.T("settings.view.walls"), hint: "settings.view.walls");
            AddViewToggle(page, ref y, ViewField.WallOutline, Loc.T("settings.view.outline"), WallOutlineId, 1);
            Hint(WallOutlineId, hint: "settings.view.wallOutline");
            AddViewToggle(page, ref y, ViewField.LowerNearWalls, Loc.T("settings.view.lowerNearWalls"),
                Loc.T("settings.view.lowerNearWalls"), 1);
            Hint(Loc.T("settings.view.lowerNearWalls"), hint: "settings.view.lowerNearWalls");
            AddViewToggle(page, ref y, ViewField.LowerAllWalls, Loc.T("settings.view.lowerAllWalls"),
                Loc.T("settings.view.lowerAllWalls"), 2);
            Hint(Loc.T("settings.view.lowerAllWalls"), hint: "settings.view.lowerAllWalls");
            AddViewToggle(page, ref y, ViewField.HideOpeningsOnLoweredWalls, Loc.T("settings.view.hideOpeningsOnLoweredWalls"),
                Loc.T("settings.view.hideOpeningsOnLoweredWalls"), 2);
            Hint(Loc.T("settings.view.hideOpeningsOnLoweredWalls"), hint: "settings.view.hideOpeningsOnLoweredWalls");

            y -= SettingsRowFactory.GapPx;
            AddViewToggle(page, ref y, ViewField.Objects, Loc.T("settings.view.objects"), Loc.T("settings.view.objects"), 0);
            Hint(Loc.T("settings.view.objects"), hint: "settings.view.objects");
            AddViewToggle(page, ref y, ViewField.ObjectOutline, Loc.T("settings.view.outline"), ObjectOutlineId, 1);
            Hint(ObjectOutlineId, hint: "settings.view.objectOutline");

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.view.section.lighting"));
            AddViewToggle(page, ref y, ViewField.HideLightSources, Loc.T("settings.view.hideLightSources"),
                Loc.T("settings.view.hideLightSources"), 1);
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

            int currentIdx = CurrentModeTab;
            for (int i = 0; i < _presetButtons.Count; i++)
            {
                var btn = _presetButtons[i];
                if (btn == null) continue;
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = i == _presetTab ? UIStyle.SurfaceActive : UIStyle.SurfaceInactive;
                var caption = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (caption == null) continue;
                caption.text = PresetCaptions[i];
                PresetCurrentMark.Set(btn, i == currentIdx);
            }

            foreach (var kv in _toggles)
            {
                var field = kv.Key;
                var toggle = kv.Value;
                if (toggle == null) continue;

                toggle.SetIsOnWithoutNotify(state.Get(field));

                var parent = ViewResolver.ParentOf(field);
                bool parentOn = parent == null || state.Get(parent.Value);
                bool enabled = parentOn && ViewResolver.IsEditable(edited, field);
                _rows.SetToggleEnabled(toggle, _toggleKeys[field], enabled);
            }
        }

        private void BuildPresetSwitch(Transform parent, ref float y)
        {
            var rowRect = SettingsRowFactory.CreateRow("RowViewPreset", parent, y);

            int count = PresetModes.Length;
            float gap = 4f;
            float stripW = SettingsRowFactory.ContentW - HintBadge.LaneWidth;
            float btnW = (stripW - gap * (count - 1)) / count;
            float firstX = -(SettingsRowFactory.ContentW - btnW) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var btn = UIFactory.CreateButton($"ViewPreset_{idx}", rowRect, "",
                    new Vector2(firstX + idx * (btnW + gap), 0),
                    new Vector2(btnW, PresetRowH),
                    () => SwitchPreset(idx));
                PresetCurrentMark.Attach(btn);
                _presetButtons.Add(btn);
            }

            HintBadge.Attach(rowRect,
                new Vector2(SettingsRowFactory.ContentW * 0.5f - UIStyle.HintBadgeSize * 0.5f, 0f),
                hint: "settings.view.preset");

            y -= PresetRowH + SettingsRowFactory.RowStep - SettingsRowFactory.RowH;
        }

        private void Hint(string rowKey, string hint) =>
            HintBadge.AttachAfterLabel(_rows.RowLabel(rowKey), hint);

        private void SwitchPreset(int index)
        {
            _presetTab = index;
            _dependentStatesChanged();
        }

        private void AddViewToggle(Transform parent, ref float y, ViewField field,
            string label, string key, int indentLevel)
        {
            var toggle = _rows.AddToggle(parent, ref y, label,
                ViewResolver.Resolve(EditedPresetMode).Get(field),
                v =>
                {
                    var s = KitchenSettings.Instance;
                    if (s == null) return;
                    ViewResolver.PresetFor(EditedPresetMode, s).Set(field, v);
                    SceneVisibilityManager.Invalidate();
                    _dependentStatesChanged();
                }, id: key, indentLevel: indentLevel);
            _toggles[field] = toggle;
            _toggleKeys[field] = key;
        }
    }
}
