using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsPhotoTab
    {
        private readonly SettingsRowFactory _rows;
        private readonly Dictionary<string, Toggle> _presetLinkedToggles = new();

        private Button? _presetButton;
        private Toggle? _photoActiveToggle;
        private Toggle? _ssgiToggle;

        public SettingsPhotoTab(SettingsRowFactory rows) => _rows = rows;

        public void Build(Transform page, KitchenSettings s, float topY)
        {
            float y = topY;

            _photoActiveToggle = _rows.AddToggle(page, ref y, "Фоторежим", PhotoMode.Active,
                v => PhotoMode.SetActive(v));
            PhotoMode.Changed -= SyncActiveToggle;
            PhotoMode.Changed += SyncActiveToggle;

            y -= SettingsRowFactory.GapPx;
            BuildPresetRow(page, ref y, s);

            y -= SettingsRowFactory.GapPx;
            AddPresetLinkedToggle(page, ref y, "Тени", s.PhotoShadows,
                v => s.PhotoShadows = v, () => s.PhotoShadows);
            AddPresetLinkedToggle(page, ref y, "Мягкие тени", s.PhotoSoftShadows,
                v => s.PhotoSoftShadows = v, () => s.PhotoSoftShadows);
            AddPresetLinkedToggle(page, ref y, "Сглаживание", s.PhotoAntiAliasing,
                v => s.PhotoAntiAliasing = v, () => s.PhotoAntiAliasing);
            AddPresetLinkedToggle(page, ref y, "Суперсэмплинг", s.PhotoSupersampling,
                v => s.PhotoSupersampling = v, () => s.PhotoSupersampling);
            AddPresetLinkedToggle(page, ref y, "Ambient occlusion", s.PhotoAmbientOcclusion,
                v => s.PhotoAmbientOcclusion = v, () => s.PhotoAmbientOcclusion);
            AddPresetLinkedToggle(page, ref y, "Свечение (bloom)", s.PhotoBloom,
                v => s.PhotoBloom = v, () => s.PhotoBloom);
            AddPresetLinkedToggle(page, ref y, "Виньетка", s.PhotoVignette,
                v => s.PhotoVignette = v, () => s.PhotoVignette);

            y -= SettingsRowFactory.GapPx;
            _rows.AddToggle(page, ref y, "Потолок по стенам", s.PhotoCeiling,
                v => { s.PhotoCeiling = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoCeiling);

            _ssgiToggle = _rows.AddToggle(page, ref y, "Отражённый свет (SSGI)", s.PhotoSSGI,
                v => { s.PhotoSSGI = v; PhotoMode.RefreshIfActive(); }, read: () => s.PhotoSSGI);

        }

        public void Dispose() => PhotoMode.Changed -= SyncActiveToggle;

        public void SyncActiveToggle()
        {
            if (_photoActiveToggle != null)
                _photoActiveToggle.SetIsOnWithoutNotify(PhotoMode.Active);
        }

        public void RefreshPresetLabel()
        {
            var s = KitchenSettings.Instance;
            var label = _presetButton != null
                ? _presetButton.GetComponentInChildren<TMPro.TextMeshProUGUI>()
                : null;
            if (label != null && s != null) label.text = PresetName(PhotoQualityPresetTable.Detect(s));
        }


        private void AddPresetLinkedToggle(Transform page, ref float y, string label, bool value,
            Action<bool> setter, Func<bool> read)
        {
            var toggle = _rows.AddToggle(page, ref y, label, value,
                v => { setter(v); OnPresetLinkedToggleChanged(); }, read: read);
            _presetLinkedToggles[label] = toggle;
        }

        private void OnPresetLinkedToggleChanged()
        {
            var s = KitchenSettings.Instance;
            if (s != null) s.PhotoQuality = PhotoQualityPresetTable.Detect(s);
            RefreshPresetLabel();
            PhotoMode.RefreshIfActive();
        }

        private void SyncPresetLinkedTogglesFromSettings(KitchenSettings s)
        {
            void Set(string key, bool v)
            {
                if (_presetLinkedToggles.TryGetValue(key, out var tg)) tg.SetIsOnWithoutNotify(v);
            }
            Set("Тени", s.PhotoShadows);
            Set("Мягкие тени", s.PhotoSoftShadows);
            Set("Сглаживание", s.PhotoAntiAliasing);
            Set("Суперсэмплинг", s.PhotoSupersampling);
            Set("Ambient occlusion", s.PhotoAmbientOcclusion);
            Set("Свечение (bloom)", s.PhotoBloom);
            Set("Виньетка", s.PhotoVignette);
        }

        private void BuildPresetRow(Transform parent, ref float y, KitchenSettings s)
        {
            var rowRect = SettingsRowFactory.CreateRow("RowPreset", parent, y);

            SettingsRowFactory.CreateRowLabel("Lbl_Quality", rowRect, "Качество", 0f);

            _presetButton = UIFactory.CreateButton("Btn_Quality", rowRect,
                PresetName(PhotoQualityPresetTable.Detect(s)),
                new Vector2(SettingsRowFactory.ContentW * 0.5f - SettingsRowFactory.ControlW * 0.5f, 0),
                new Vector2(SettingsRowFactory.ControlW, SettingsRowFactory.RowH),
                () => CyclePreset(s));

            y -= SettingsRowFactory.RowStep;
        }

        private void CyclePreset(KitchenSettings s)
        {
            var next = PhotoQualityPresetTable.Next(PhotoQualityPresetTable.Detect(s));
            PhotoQualityPresetTable.Apply(next, s);
            SyncPresetLinkedTogglesFromSettings(s);
            RefreshPresetLabel();
            PhotoMode.RefreshIfActive();
        }

        private static string PresetName(PhotoQualityPreset preset) => preset switch
        {
            PhotoQualityPreset.Low => "Низкое",
            PhotoQualityPreset.Medium => "Среднее",
            PhotoQualityPreset.High => "Высокое",
            _ => "Свои настройки"
        };
    }
}
