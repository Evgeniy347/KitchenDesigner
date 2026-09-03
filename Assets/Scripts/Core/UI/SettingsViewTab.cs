using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsViewTab
    {
        public const string WallOutlineId = "Контур стен";
        public const string ObjectOutlineId = "Контур объектов";

        private const float PresetRowH = 46f;

        private static readonly string[] PresetCaptions = { "Обычный", "Помещение", "Фоторежим" };

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

            AddViewToggle(page, ref y, ViewField.Walls, "Стены", "Стены", 0);
            AddViewToggle(page, ref y, ViewField.WallOutline, "Контур", WallOutlineId, 1);
            AddViewToggle(page, ref y, ViewField.LowerNearWalls, "Опускать ближние стены",
                "Опускать ближние стены", 1);
            AddViewToggle(page, ref y, ViewField.LowerAllWalls, "Опускать все стены",
                "Опускать все стены", 2);
            AddViewToggle(page, ref y, ViewField.HideOpeningsOnLoweredWalls, "Скрывать окна и двери",
                "Скрывать окна и двери", 2);

            y -= SettingsRowFactory.GapPx;
            AddViewToggle(page, ref y, ViewField.Objects, "Объекты", "Объекты", 0);
            AddViewToggle(page, ref y, ViewField.ObjectOutline, "Контур", ObjectOutlineId, 1);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Освещение");
            AddViewToggle(page, ref y, ViewField.HideLightSources, "Скрыть источники света",
                "Скрыть источники света", 1);

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
                caption.text = PresetCaptions[i] + (i == currentIdx ? " (текущий)" : "");
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
            float btnW = (SettingsRowFactory.ContentW - gap * (count - 1)) / count;
            float firstX = -(SettingsRowFactory.ContentW - btnW) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var btn = UIFactory.CreateButton($"ViewPreset_{idx}", rowRect, "",
                    new Vector2(firstX + idx * (btnW + gap), 0),
                    new Vector2(btnW, PresetRowH),
                    () => SwitchPreset(idx));
                _presetButtons.Add(btn);
            }

            y -= PresetRowH + SettingsRowFactory.RowStep - SettingsRowFactory.RowH;
        }

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
