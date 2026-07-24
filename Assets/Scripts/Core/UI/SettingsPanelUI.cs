using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SettingsPanelUI : MonoBehaviour
    {
        private GameObject? _root;
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();

        private readonly List<GameObject> _tabPages = new();
        private readonly List<Button> _tabButtons = new();
        private int _activeTab;

        private static readonly Color ActiveTabColor = new(0.28f, 0.33f, 0.42f, 1f);
        private static readonly Color InactiveTabColor = new(0.15f, 0.16f, 0.20f, 1f);

        private const float PanelW = 520;
        private const float PanelH = 680;
        private const float ContentW = 480;
        private const float RowH = 32;
        private const float RowStep = 38;
        private const float LabelW = 300;
        private const float ControlW = 150;
        private const float TitleY = 285;
        private const float TabY = 246;
        private const float ContentTopY = 202;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SettingsPanel", canvas, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;

            UIFactory.CreateLabel("SetTitle", panel.transform, "Настройки", 24,
                new Vector2(0, TitleY), new Vector2(PanelW - 40, 36), TextAnchor.MiddleCenter);

            var s = KitchenSettings.Instance;
            if (s == null)
            {
                _root!.SetActive(false);
                return;
            }

            BuildTabs(panel.transform);
            BuildProjectTab(panel.transform, s);
            BuildPhotoTab(panel.transform, s);
            BuildAboutTab(panel.transform);

            SwitchTab(0);

            float closeY = ContentTopY - 12 * RowStep - 4 * 6 - 20;
            UIFactory.CreateButton("SetClose", panel.transform, "Закрыть",
                new Vector2(0, closeY), new Vector2(160, 40),
                () => SetVisible(false));

            // Крестик — как у всех окон (правило 7 UI-GUIDELINES).
            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            _root!.SetActive(false);
        }

        // ── Tabs ─────────────────────────────────────────────

        private void BuildTabs(Transform parent)
        {
            string[] labels = { "Проект", "Фото режим", "О программе" };
            float tabW = (PanelW - 40) / labels.Length;

            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                float posX = -(PanelW - 40) * 0.5f + tabW * i + tabW * 0.5f;
                var btn = UIFactory.CreateButton($"Tab_{idx}", parent, labels[idx],
                    new Vector2(posX, TabY), new Vector2(tabW - 4, 32),
                    () => SwitchTab(idx));
                btn.GetComponent<Image>().color = InactiveTabColor;
                _tabButtons.Add(btn);
            }
        }

        private void SwitchTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].GetComponent<Image>().color = i == index ? ActiveTabColor : InactiveTabColor;
            for (int i = 0; i < _tabPages.Count; i++)
                _tabPages[i].SetActive(i == index);
        }

        // ── Tab: Проект ─────────────────────────────────────

        private void BuildProjectTab(Transform panel, KitchenSettings s)
        {
            var page = new GameObject("Tab_Project");
            page.transform.SetParent(panel, false);
            _tabPages.Add(page);
            var t = page.transform;

            float y = ContentTopY;

            AddToggleRow(t, ref y, "Сетка", s.GridEnabled,
                v => { s.GridEnabled = v; UpdateDependentStates(); });

            // Зависимое поле: с отступом и неактивно при выключенном родителе.
            _gridStepField = AddInputRow(t, ref y, "Шаг сетки", s.GridStep.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                (TMP_InputField f) =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text) ?? (int.TryParse(f.text, out int parsed) ? parsed : s.GridStep);
                    s.GridStep = val; f.text = s.GridStep.ToString();
                }, s.GridStep.ToString(), unit: "мм", indent: true);

            y -= 6;
            AddToggleRow(t, ref y, "Привязка к деталям", s.SnapEnabled,
                v => { s.SnapEnabled = v; UpdateDependentStates(); });

            _snapThresholdField = AddInputRow(t, ref y, "Порог привязки", s.SnapThreshold.ToString("F0"),
                TMP_InputField.ContentType.DecimalNumber,
                (TMP_InputField f) =>
                {
                    var val = ExpressionParser.EvaluateFloat(f.text) ?? (float.TryParse(f.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : s.SnapThreshold);
                    s.SnapThreshold = val; f.text = s.SnapThreshold.ToString("F0");
                }, s.SnapThreshold.ToString("F0"), unit: "мм", indent: true);

            y -= 6;
            AddToggleRow(t, ref y, "Блокировать недопустимые изменения", s.BlockOnViolation,
                v => { s.BlockOnViolation = v; });

            AddToggleRow(t, ref y, "Автосохранение", s.AutoSave,
                v => { s.AutoSave = v; UpdateDependentStates(); });

            _autoSaveIntervalField = AddInputRow(t, ref y, "Интервал автосохранения", s.AutoSaveInterval.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                (TMP_InputField f) =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text) ?? (int.TryParse(f.text, out int parsed) ? parsed : s.AutoSaveInterval);
                    s.AutoSaveInterval = val; f.text = s.AutoSaveInterval.ToString();
                }, s.AutoSaveInterval.ToString(), unit: "с", indent: true);

            UpdateDependentStates();

            y -= 6;
            AddToggleRow(t, ref y, "Пространственная сетка", s.SpatialGrid,
                v => { s.SpatialGrid = v; });

            AddToggleRow(t, ref y, "Контур (чёрные рёбра)", s.EdgeOutline,
                v => { s.EdgeOutline = v; });

            AddToggleRow(t, ref y, "Стены", s.WallsEnabled,
                v => { s.WallsEnabled = v; });

            AddToggleRow(t, ref y, "Опускать ближние стены", s.LowerNearWalls,
                v => { s.LowerNearWalls = v; });

            y -= 6;
            AddToggleRow(t, ref y, "Свободное панорамирование", s.CameraPanFree,
                v => { s.CameraPanFree = v; });
        }

        // ── Tab: Фото режим ─────────────────────────────────

        private Button? _presetButton;
        private Toggle? _photoActiveToggle;

        private void SyncPhotoActiveToggle()
        {
            if (_photoActiveToggle != null)
                _photoActiveToggle.SetIsOnWithoutNotify(PhotoMode.Active);
        }

        private void OnDestroy()
        {
            PhotoMode.Changed -= SyncPhotoActiveToggle;
        }

        private void BuildPhotoTab(Transform panel, KitchenSettings s)
        {
            var page = new GameObject("Tab_Photo");
            page.transform.SetParent(panel, false);
            _tabPages.Add(page);
            var t = page.transform;

            float y = ContentTopY;

            _photoActiveToggle = AddToggleRow(t, ref y, "Фоторежим", PhotoMode.Active,
                v => PhotoMode.SetActive(v));
            PhotoMode.Changed -= SyncPhotoActiveToggle;
            PhotoMode.Changed += SyncPhotoActiveToggle;

            y -= 6;
            BuildPresetRow(t, ref y, s);

            // Тумблеры, привязанные к пресету: ручное изменение любого переводит
            // пресет в «Свои настройки» (или в совпавший именованный).
            y -= 6;
            AddLinkedToggle(t, ref y, "Тени", s.PhotoShadows, v => s.PhotoShadows = v);
            AddLinkedToggle(t, ref y, "Мягкие тени", s.PhotoSoftShadows, v => s.PhotoSoftShadows = v);
            AddLinkedToggle(t, ref y, "Сглаживание", s.PhotoAntiAliasing, v => s.PhotoAntiAliasing = v);
            AddLinkedToggle(t, ref y, "Суперсэмплинг", s.PhotoSupersampling, v => s.PhotoSupersampling = v);
            AddLinkedToggle(t, ref y, "Ambient occlusion", s.PhotoAmbientOcclusion, v => s.PhotoAmbientOcclusion = v);
            AddLinkedToggle(t, ref y, "Свечение (bloom)", s.PhotoBloom, v => s.PhotoBloom = v);
            AddLinkedToggle(t, ref y, "Виньетка", s.PhotoVignette, v => s.PhotoVignette = v);

            // Потолок — не про качество, а про сцену: к пресету не привязан.
            y -= 6;
            AddToggleRow(t, ref y, "Потолок по стенам", s.PhotoCeiling,
                v => { s.PhotoCeiling = v; PhotoMode.RefreshIfActive(); });
        }

        // ── Пресет + привязанные тумблеры ───────────────────

        private readonly Dictionary<string, Toggle> _photoLinkedToggles = new();

        private void AddLinkedToggle(Transform t, ref float y, string label, bool value, Action<bool> setter)
        {
            var toggle = AddToggleRow(t, ref y, label, value, v => { setter(v); OnLinkedToggleChanged(); });
            _photoLinkedToggles[label] = toggle;
        }

        private void OnLinkedToggleChanged()
        {
            var s = KitchenSettings.Instance;
            if (s != null) s.PhotoQuality = PhotoQualityPresetTable.Detect(s);
            UpdatePresetLabel();
            PhotoMode.RefreshIfActive();
        }

        private void SyncLinkedTogglesFromSettings(KitchenSettings s)
        {
            void Set(string key, bool v)
            {
                if (_photoLinkedToggles.TryGetValue(key, out var tg)) tg.SetIsOnWithoutNotify(v);
            }
            Set("Тени", s.PhotoShadows);
            Set("Мягкие тени", s.PhotoSoftShadows);
            Set("Сглаживание", s.PhotoAntiAliasing);
            Set("Суперсэмплинг", s.PhotoSupersampling);
            Set("Ambient occlusion", s.PhotoAmbientOcclusion);
            Set("Свечение (bloom)", s.PhotoBloom);
            Set("Виньетка", s.PhotoVignette);
        }

        private void UpdatePresetLabel()
        {
            var s = KitchenSettings.Instance;
            var label = _presetButton != null ? _presetButton.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (label != null && s != null) label.text = PresetName(PhotoQualityPresetTable.Detect(s));
        }

        // Пресет качества — компактная кнопка-циклер (Низкое → Среднее → Высокое),
        // чтобы уложиться в ту же колонку контролов, что и остальные строки.
        private void BuildPresetRow(Transform parent, ref float y, KitchenSettings s)
        {
            var rowRect = UIFactory.CreateRect("RowPreset", parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            UIFactory.CreateLabel("Lbl_Quality", rowRect, "Качество", 16,
                new Vector2(-(ContentW - LabelW) * 0.5f, 0), new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);

            _presetButton = UIFactory.CreateButton("Btn_Quality", rowRect,
                PresetName(PhotoQualityPresetTable.Detect(s)),
                new Vector2(ContentW * 0.5f - ControlW * 0.5f, 0), new Vector2(ControlW, RowH),
                () => CyclePreset(s));

            y -= RowStep;
        }

        private void CyclePreset(KitchenSettings s)
        {
            var next = PhotoQualityPresetTable.Next(PhotoQualityPresetTable.Detect(s));
            PhotoQualityPresetTable.Apply(next, s);      // двигает реальные тумблеры
            SyncLinkedTogglesFromSettings(s);
            UpdatePresetLabel();
            PhotoMode.RefreshIfActive();
        }

        private static string PresetName(PhotoQualityPreset preset) => preset switch
        {
            PhotoQualityPreset.Low => "Низкое",
            PhotoQualityPreset.Medium => "Среднее",
            PhotoQualityPreset.High => "Высокое",
            _ => "Свои настройки"
        };

        // ── Tab: О программе ────────────────────────────────

        private void BuildAboutTab(Transform panel)
        {
            var page = new GameObject("Tab_About");
            page.transform.SetParent(panel, false);
            _tabPages.Add(page);
            var t = page.transform;

            UIFactory.CreateLabel("AboutVersion", t, $"Версия: {BuildInfo.Version}", 18,
                new Vector2(0, ContentTopY), new Vector2(ContentW, 32), TextAnchor.MiddleCenter);

            UIFactory.CreateLabel("AboutDate", t, $"Сборка: {BuildInfo.BuildDate}", 16,
                new Vector2(0, ContentTopY - RowStep), new Vector2(ContentW, 28), TextAnchor.MiddleCenter);
        }

        // ── Row helpers ─────────────────────────────────────

        private Toggle AddToggleRow(Transform parent, ref float y, string label, bool value, Action<bool> onChanged)
        {
            var rowRect = UIFactory.CreateRect("RowTgl_" + label, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            UIFactory.CreateLabel("Lbl_" + label, rowRect, label, 16,
                new Vector2(-(ContentW - LabelW) * 0.5f, 0), new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);

            var toggle = CreateRightToggle("Tgl_" + label, rowRect, value, onChanged);

            y -= RowStep;
            return toggle;
        }

        private Toggle CreateRightToggle(string name, Transform parent, bool value, Action<bool> onChanged)
        {
            // Хит-таргет — вся строка не нужна, но сам тоггл ≥32px (правило 8).
            var rect = UIFactory.CreateRect(name, parent);
            rect.sizeDelta = new Vector2(32, RowH);
            rect.anchoredPosition = new Vector2(ContentW * 0.5f - 16, 0);

            var toggle = rect.gameObject.AddComponent<Toggle>();

            var box = UIFactory.CreatePanel(name + "_Box", rect,
                Vector2.zero, new Vector2(22, 22), UIFactory.FieldColor);
            toggle.graphic = UIFactory.CreateCheckmark(name + "_Check", box.transform);
            toggle.targetGraphic = box;

            toggle.isOn = value;
            if (onChanged != null)
                toggle.onValueChanged.AddListener(v => onChanged(v));
            return toggle;
        }

        private TMP_InputField AddInputRow(Transform parent, ref float y, string label,
            string initial, TMP_InputField.ContentType contentType,
            Action<TMP_InputField> onEndEdit, string cleanValue,
            string? unit = null, bool indent = false)
        {
            var rowRect = UIFactory.CreateRect("RowFld_" + label, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            float indentPx = indent ? 20f : 0f;
            var lbl = UIFactory.CreateLabel("Lbl_" + label, rowRect, label, 16,
                new Vector2(-(ContentW - LabelW) * 0.5f + indentPx, 0), new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);
            _rowLabels[label] = lbl;

            var field = unit != null
                ? UIFactory.CreateNumberField("Fld_" + label, rowRect, initial,
                    new Vector2(ContentW * 0.5f - ControlW * 0.5f, 0), new Vector2(ControlW, RowH), unit)
                : UIFactory.CreateInputField("Fld_" + label, rowRect, initial,
                    new Vector2(ContentW * 0.5f - ControlW * 0.5f, 0), new Vector2(ControlW, RowH));
            field.contentType = TMP_InputField.ContentType.Custom;
            bool isDecimal = contentType == TMP_InputField.ContentType.DecimalNumber;
            field.onValidateInput = (text, idx, ch) =>
                char.IsDigit(ch) || ch == '+' || ch == '-' || ch == ' ' || (isDecimal && ch == '.') ? ch : '\0';
            TrackField(field, cleanValue);
            field.onEndEdit.AddListener(t =>
            {
                onEndEdit?.Invoke(field);
                UpdateFieldHighlight(field);
            });

            y -= RowStep;
            return field;
        }

        // ── Зависимые поля ──────────────────────────────────
        // Поле без родителя-тумблера бессмысленно — гасим его, а не оставляем
        // редактируемым «в никуда».

        private TMP_InputField? _gridStepField;
        private TMP_InputField? _snapThresholdField;
        private TMP_InputField? _autoSaveIntervalField;
        private readonly Dictionary<string, TMPro.TextMeshProUGUI> _rowLabels = new();

        private void UpdateDependentStates()
        {
            var s = KitchenSettings.Instance;
            if (s == null) return;
            SetFieldEnabled(_gridStepField, "Шаг сетки", s.GridEnabled);
            SetFieldEnabled(_snapThresholdField, "Порог привязки", s.SnapEnabled);
            SetFieldEnabled(_autoSaveIntervalField, "Интервал автосохранения", s.AutoSave);
        }

        private void SetFieldEnabled(TMP_InputField? field, string labelKey, bool enabled)
        {
            if (field == null) return;
            field.interactable = enabled;
            if (field.textComponent != null)
                field.textComponent.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
            if (_rowLabels.TryGetValue(labelKey, out var lbl))
                lbl.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
        }

        // ── Public API ──────────────────────────────────────

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
            if (visible) SyncPhotoActiveToggle();
        }

        // ── Подсветка изменённых полей ──────────────────────

        private void TrackField(TMP_InputField field, string cleanValue)
        {
            if (field == null) return;
            _cleanValues[field] = cleanValue;
            field.onValueChanged.AddListener(_ => UpdateFieldHighlight(field));
        }

        private void UpdateFieldHighlight(TMP_InputField field)
        {
            if (field == null) return;
            var clean = _cleanValues.TryGetValue(field, out var v) ? v : field.text;
            UIFactory.SetHighlight(field, field.text != clean);
        }
    }
}
