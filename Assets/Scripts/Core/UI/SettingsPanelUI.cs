using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SettingsPanelUI : MonoBehaviour, IProjectWindow
    {
        private GameObject? _root;
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();

        private readonly List<GameObject> _tabPages = new();
        private readonly List<Button> _tabButtons = new();
        private int _activeTab;

        private static readonly Color ActiveTabColor = new(0.28f, 0.33f, 0.42f, 1f);
        private static readonly Color InactiveTabColor = new(0.15f, 0.16f, 0.20f, 1f);

        // Шесть вкладок: при прежних 520 px «Управление» и «О программе»
        // ломались на две строки, поэтому панель шире ровно на одну вкладку.
        private const float PanelW = 600;
        private const float PanelH = 900;
        private const float ContentW = 480;
        private const float RowH = 32;
        private const float RowStep = 38;
        private const float LabelW = 300;
        private const float ControlW = 150;
        private const float TitleY = PanelH * 0.5f - 55;
        private const float TabY = PanelH * 0.5f - 94;
        private const float ContentTopY = PanelH * 0.5f - 138;

        /// <summary>Отступ одного уровня вложенности подопции (правило дерева
        /// из UI-GUIDELINES).</summary>
        private const float IndentPx = 20f;

        /// <summary>«Закрыть» стоит у нижнего края панели, а не под последней
        /// строкой самой длинной вкладки: вкладок шесть, содержимое у них разной
        /// высоты, и кнопка не должна прыгать при переключении (а заодно —
        /// съезжать вверх, когда строку переносят на другую вкладку).</summary>
        private const float CloseY = -PanelH * 0.5f + 54;
        private const float TabFontSize = 14f;
        private const float GapPx = 6f;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("SettingsPanel", canvas, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("SetTitle", panel.transform, "Настройки", 24,
                new Vector2(0, TitleY), new Vector2(PanelW - 40, 36), TextAnchor.MiddleCenter);

            var s = KitchenSettings.Instance;
            if (s == null)
            {
                _root!.SetActive(false);
                return;
            }

            BuildTabs(panel.transform);
            // Порядок вызовов = порядок подписей в BuildTabs: страницы ложатся в
            // _tabPages, и SwitchTab адресует их тем же индексом.
            BuildProjectTab(panel.transform, s);
            BuildViewTab(panel.transform, s);
            BuildControlTab(panel.transform, s);
            BuildPhotoTab(panel.transform, s);
            BuildLightTab(panel.transform, s);
            BuildAboutTab(panel.transform);

            SwitchTab(0);

            // Зависимые строки живут на двух вкладках сразу (поля — на «Проекте»,
            // тумблеры вида — на «Виде»), поэтому синхронизация одна на всё
            // окно и только после того, как построены обе страницы. Заодно
            // вкладка «Вид» встаёт на пресет текущего режима.
            OnEditModeChanged();

            UIFactory.CreateButton("SetClose", panel.transform, "Закрыть",
                new Vector2(0, CloseY), new Vector2(160, 40),
                () => SetVisible(false));

            // Крестик — как у всех окон (правило 7 UI-GUIDELINES).
            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            _root!.SetActive(false);
        }

        // ── Tabs ─────────────────────────────────────────────

        private void BuildTabs(Transform parent)
        {
            string[] labels = { "Проект", "Вид", "Управление", "Фото режим", "Свет", "О программе" };
            float tabW = (PanelW - 40) / labels.Length;

            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                float posX = -(PanelW - 40) * 0.5f + tabW * i + tabW * 0.5f;
                var btn = UIFactory.CreateButton($"Tab_{idx}", parent, labels[idx],
                    new Vector2(posX, TabY), new Vector2(tabW - 4, 32),
                    () => SwitchTab(idx));
                btn.GetComponent<Image>().color = InactiveTabColor;
                // Вкладок шесть, каждая ~93 px: без уменьшенного кегля
                // «Управление» и «О программе» ломаются на две строки.
                var caption = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (caption != null) caption.fontSize = TabFontSize;
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

            y -= GapPx;
            AddToggleRow(t, ref y, "Пространственная сетка", s.SpatialGrid,
                v => { s.SpatialGrid = v; });

            // Ниже этого процента наезд соседа на торец не считается ошибкой
            // EDG-01 (планка, царга — нормальная конструкция).
            _edgeThresholdField = AddInputRow(t, ref y, "Нижний порог кромки",
                s.EdgePartialThresholdPct.ToString(),
                TMP_InputField.ContentType.IntegerNumber,
                (TMP_InputField f) =>
                {
                    var val = ExpressionParser.EvaluateInt(f.text)
                        ?? (int.TryParse(f.text, out int parsed) ? parsed : s.EdgePartialThresholdPct);
                    s.EdgePartialThresholdPct = val;
                    f.text = s.EdgePartialThresholdPct.ToString();
                }, s.EdgePartialThresholdPct.ToString(), unit: "%");

            // «Объекты» и их контур переехали на вкладку «Вид»: они часть пресета
            // режима, а не правил работы с деталями.
            y -= GapPx;
            AddToggleRow(t, ref y, "Свободное панорамирование", s.CameraPanFree,
                v => { s.CameraPanFree = v; });
        }

        // ── Tab: Вид ────────────────────────────────────────

        /// <summary>Что показывать в сцене. Настройка своя для каждого режима
        /// работы: пресет «обычный» и пресет «помещение» — отдельные наборы,
        /// поэтому погашенные для работы с деталями стены не мешают правке
        /// помещения и возвращаются при выходе из него. Сверху — переключатель,
        /// какой пресет мы сейчас правим; часть тумблеров режим форсирует, они
        /// показывают своё значение серыми (см. <see cref="ViewResolver"/>).</summary>
        private void BuildViewTab(Transform panel, KitchenSettings s)
        {
            var page = new GameObject("Tab_View");
            page.transform.SetParent(panel, false);
            _tabPages.Add(page);
            var t = page.transform;

            float y = ContentTopY;

            BuildViewPresetSwitch(t, ref y);
            y -= GapPx;

            AddViewToggle(t, ref y, ViewField.Walls, "Стены", "Стены", 0);
            AddViewToggle(t, ref y, ViewField.WallOutline, "Контур", WallOutlineId, 1);
            AddViewToggle(t, ref y, ViewField.LowerNearWalls, "Опускать ближние стены",
                "Опускать ближние стены", 1);
            AddViewToggle(t, ref y, ViewField.HideOpeningsOnLoweredWalls, "Скрывать окна и двери",
                "Скрывать окна и двери", 2);

            y -= GapPx;
            AddViewToggle(t, ref y, ViewField.Objects, "Объекты", "Объекты", 0);
            AddViewToggle(t, ref y, ViewField.ObjectOutline, "Контур", ObjectOutlineId, 1);

            y -= GapPx;
            AddHeaderRow(t, ref y, "Освещение");
            AddViewToggle(t, ref y, ViewField.HideLightSources, "Скрыть источники света",
                "Скрыть источники света", 1);

            // Режим форсирует часть тумблеров — состояние вкладки должно следовать
            // за переключением режима, а не только за открытием панели.
            EditModeManager.Changed -= OnEditModeChanged;
            EditModeManager.Changed += OnEditModeChanged;
        }

        /// <summary>Переключатель «какой пресет правим». Не переключает режим
        /// редактора — только то, что показано на вкладке.</summary>
        private void BuildViewPresetSwitch(Transform parent, ref float y)
        {
            var rowRect = UIFactory.CreateRect("RowViewPreset", parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            float btnW = (ContentW - 8) * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                var btn = UIFactory.CreateButton($"ViewPreset_{idx}", rowRect, "",
                    new Vector2(-btnW * 0.5f - 2 + idx * (btnW + 4), 0), new Vector2(btnW, RowH),
                    () => SwitchViewPreset(idx));
                _viewPresetButtons.Add(btn);
            }

            y -= RowStep;
        }

        private void SwitchViewPreset(int index)
        {
            _viewPresetTab = index;
            UpdateDependentStates();
        }

        /// <summary>Режим, чей пресет открыт на вкладке.</summary>
        private EditMode EditedPresetMode => _viewPresetTab == 1 ? EditMode.Room : EditMode.Normal;

        private void AddViewToggle(Transform parent, ref float y, ViewField field,
            string label, string key, int indentLevel)
        {
            var toggle = AddToggleRow(parent, ref y, label, ViewResolver.Resolve(EditedPresetMode).Get(field),
                v =>
                {
                    var s = KitchenSettings.Instance;
                    if (s == null) return;
                    ViewResolver.PresetFor(EditedPresetMode, s).Set(field, v);
                    SceneVisibilityManager.Invalidate();
                    UpdateDependentStates();
                }, id: key, indentLevel: indentLevel);
            _viewToggles[field] = toggle;
            _viewToggleKeys[field] = key;
        }

        /// <summary>При смене режима вкладка показывает пресет нового режима —
        /// иначе пользователь правил бы не то, что видит в сцене.</summary>
        private void OnEditModeChanged()
        {
            _viewPresetTab = EditModeManager.Mode == EditMode.Room ? 1 : 0;
            UpdateDependentStates();
        }

        // ── Tab: Управление ─────────────────────────────────

        private void BuildControlTab(Transform panel, KitchenSettings s)
        {
            var page = new GameObject("Tab_Control");
            page.transform.SetParent(panel, false);
            _tabPages.Add(page);
            var t = page.transform;

            float y = ContentTopY;

            AddSliderRow(t, ref y, "Чувствительность мыши", s.MouseSensitivity,
                v => s.MouseSensitivity = v);
            AddSliderRow(t, ref y, "Скорость WASD", s.WasdSpeed,
                v => s.WasdSpeed = v);
            AddSliderRow(t, ref y, "Скорость ←→↑↓", s.ArrowSpeed,
                v => s.ArrowSpeed = v);
        }

        // ── Tab: Фото режим ─────────────────────────────────

        private Button? _presetButton;
        private Toggle? _photoActiveToggle;
        private Toggle? _photoSSGIToggle;

        private void SyncPhotoActiveToggle()
        {
            if (_photoActiveToggle != null)
                _photoActiveToggle.SetIsOnWithoutNotify(PhotoMode.Active);
        }

        private void OnDestroy()
        {
            ProjectWindows.Unregister(this);
            PhotoMode.Changed -= SyncPhotoActiveToggle;
            EditModeManager.Changed -= OnEditModeChanged;
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

            // Сцена/эксперимент — к пресету не привязаны.
            y -= 6;
            AddToggleRow(t, ref y, "Потолок по стенам", s.PhotoCeiling,
                v => { s.PhotoCeiling = v; PhotoMode.RefreshIfActive(); });

            _photoSSGIToggle = AddToggleRow(t, ref y, "Отражённый свет (SSGI)", s.PhotoSSGI,
                v => { s.PhotoSSGI = v; PhotoMode.RefreshIfActive(); });

#if UNITY_WEBGL
            SetToggleEnabled(_photoSSGIToggle, "Отражённый свет (SSGI)", false);
            if (_rowLabels.TryGetValue("Отражённый свет (SSGI)", out var ssgiLbl))
                ssgiLbl.text = "Отражённый свет (SSGI)*";

            if (_photoLinkedToggles.TryGetValue("Суперсэмплинг", out var supToggle))
            {
                supToggle.interactable = false;
                if (supToggle.graphic != null)
                    supToggle.graphic.color = UIStyle.TextDisabled;
            }
            SetLabelEnabled("Суперсэмплинг", false);
            if (_rowLabels.TryGetValue("Суперсэмплинг", out var supLbl))
                supLbl.text = "Суперсэмплинг*";

            y -= 4;
            UIFactory.CreateLabel("PhotoWebGLLimitations", t,
                "* данные функции отключены в WebGL", 13,
                new Vector2(0, y), new Vector2(ContentW, 24), TextAnchor.MiddleCenter);
#endif
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

        // ── Tab: Свет ───────────────────────────────────────
        // Тонкая настройка света фоторежима: всё, что раньше было зашито в
        // PhotoQualityController константами. Дефолты равны прежним числам,
        // поэтому «не трогал ничего» = прежняя картинка.

        private void BuildLightTab(Transform panel, KitchenSettings s)
        {
            var page = new GameObject("Tab_Light");
            page.transform.SetParent(panel, false);
            _tabPages.Add(page);
            var t = page.transform;

            float y = ContentTopY;

            AddHeaderRow(t, ref y, "Заполняющий свет");
            AddIntSliderRow(t, ref y, "Окружающий свет", 0, KitchenSettings.PHOTO_AMBIENT_MAX_PCT,
                s.PhotoAmbientPct, Pct, v => { s.PhotoAmbientPct = v; PhotoMode.RefreshIfActive(); });
            AddIntSliderRow(t, ref y, "Отскок от пола", 0, KitchenSettings.PHOTO_FLOOR_BOUNCE_MAX_PCT,
                s.PhotoFloorBouncePct, Pct, v => { s.PhotoFloorBouncePct = v; PhotoMode.RefreshIfActive(); });

            y -= 6;
            AddHeaderRow(t, ref y, "Экспозиция и тон");
            AddIntSliderRow(t, ref y, "Экспозиция",
                KitchenSettings.PHOTO_EXPOSURE_MIN_PCT, KitchenSettings.PHOTO_EXPOSURE_MAX_PCT,
                s.PhotoExposurePct, Ev, v => { s.PhotoExposurePct = v; PhotoMode.RefreshIfActive(); });
            AddIntSliderRow(t, ref y, "Контраст",
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoContrastPct, Pct, v => { s.PhotoContrastPct = v; PhotoMode.RefreshIfActive(); });
            AddIntSliderRow(t, ref y, "Насыщенность",
                KitchenSettings.PHOTO_COLOR_MIN_PCT, KitchenSettings.PHOTO_COLOR_MAX_PCT,
                s.PhotoSaturationPct, Pct, v => { s.PhotoSaturationPct = v; PhotoMode.RefreshIfActive(); });

            y -= 6;
            AddHeaderRow(t, ref y, "Эффекты");
            AddIntSliderRow(t, ref y, "Сила свечения", 0, KitchenSettings.PHOTO_BLOOM_MAX_PCT,
                s.PhotoBloomPct, Pct, v => { s.PhotoBloomPct = v; PhotoMode.RefreshIfActive(); });
            AddIntSliderRow(t, ref y, "Порог свечения", 0, KitchenSettings.PHOTO_BLOOM_THRESHOLD_MAX_PCT,
                s.PhotoBloomThresholdPct, Pct, v => { s.PhotoBloomThresholdPct = v; PhotoMode.RefreshIfActive(); });
            AddIntSliderRow(t, ref y, "Сила виньетки", 0, 100,
                s.PhotoVignettePct, Pct, v => { s.PhotoVignettePct = v; PhotoMode.RefreshIfActive(); });

            y -= 6;
            // Подпись отличается от одноимённого тумблера на вкладке «Фото
            // режим»: имена объектов сцены обязаны быть уникальными.
            AddHeaderRow(t, ref y, "Тени сцены");
            AddIntSliderRow(t, ref y, "Сила теней солнца", 0, 100,
                s.PhotoSunShadowStrengthPct, Pct, v => { s.PhotoSunShadowStrengthPct = v; PhotoMode.RefreshIfActive(); });
            AddIntSliderRow(t, ref y, "Дальность теней",
                KitchenSettings.PHOTO_SHADOW_DISTANCE_MIN_M, KitchenSettings.PHOTO_SHADOW_DISTANCE_MAX_M,
                s.PhotoShadowDistanceM, Meters, v => { s.PhotoShadowDistanceM = v; PhotoMode.RefreshIfActive(); });
            AddToggleRow(t, ref y, "Тени от ламп", s.PhotoLampShadows,
                v =>
                {
                    s.PhotoLampShadows = v;
                    LightSourceElement.RefreshAll();   // режим тени у каждой лампы свой
                    PhotoMode.RefreshIfActive();
                });
        }

        private static string Pct(int v) => v + " %";
        private static string Meters(int v) => v + " м";
        private static string Ev(int v) =>
            (v / 100f).ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture) + " EV";

        /// <summary>Строка-ползунок с целым значением и своей единицей
        /// измерения справа. В отличие от <see cref="AddSliderRow"/> диапазон
        /// задаётся вызывающим — множители скорости тут ни при чём.</summary>
        private Slider AddIntSliderRow(Transform parent, ref float y, string label,
            int min, int max, int value, Func<int, string> format, Action<int> onChanged)
        {
            var rowRect = UIFactory.CreateRect("RowSld_" + label, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            var lbl = UIFactory.CreateLabel("Lbl_" + label, rowRect, label, 16,
                new Vector2(-(ContentW - LabelW) * 0.5f, 0), new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);
            _rowLabels[label] = lbl;

            var valueLabel = UIFactory.CreateLabel("Val_" + label, rowRect, format(value), 16,
                new Vector2(ContentW * 0.5f - 32, 0), new Vector2(64, RowH), TextAnchor.MiddleRight);

            var slider = UIFactory.CreateSlider("Sld_" + label, rowRect, min, max, value,
                // Ползунок сдвинут влево ровно настолько, чтобы не наехать на
                // колонку значения справа («250 %», «−1.5 EV»).
                new Vector2(ContentW * 0.5f - ControlW * 0.5f - 46, 0), new Vector2(110, RowH),
                v =>
                {
                    int iv = Mathf.RoundToInt(v);
                    onChanged(iv);
                    if (valueLabel != null) valueLabel.text = format(iv);
                });
            slider.wholeNumbers = true;

            y -= RowStep;
            return slider;
        }

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

        /// <summary>Строка-тумблер. <paramref name="id"/> нужен, когда подпись
        /// повторяется у разных родителей («Контур» у стен и у объектов):
        /// имена объектов сцены обязаны оставаться уникальными.</summary>
        private Toggle AddToggleRow(Transform parent, ref float y, string label, bool value,
            Action<bool> onChanged, string? id = null, int indentLevel = 0)
        {
            string key = id ?? label;
            var rowRect = UIFactory.CreateRect("RowTgl_" + key, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            var lbl = UIFactory.CreateLabel("Lbl_" + key, rowRect, label, 16,
                new Vector2(-(ContentW - LabelW) * 0.5f + indentLevel * IndentPx, 0),
                new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);
            _rowLabels[key] = lbl;

            var toggle = CreateRightToggle("Tgl_" + key, rowRect, value, onChanged);

            y -= RowStep;
            return toggle;
        }

        /// <summary>Заголовок группы без собственного тумблера («Освещение»).</summary>
        private void AddHeaderRow(Transform parent, ref float y, string label)
        {
            var rowRect = UIFactory.CreateRect("RowHdr_" + label, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            UIFactory.CreateLabel("Lbl_" + label, rowRect, label, 16,
                new Vector2(-(ContentW - LabelW) * 0.5f, 0), new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);

            y -= RowStep;
        }

        /// <summary>Строка-ползунок: подпись, сам ползунок и текущее значение
        /// множителя справа («1.0×»), чтобы цифра была видна без перетаскивания.</summary>
        private Slider AddSliderRow(Transform parent, ref float y, string label,
            float value, Action<float> onChanged)
        {
            var rowRect = UIFactory.CreateRect("RowSld_" + label, parent);
            rowRect.sizeDelta = new Vector2(ContentW, RowH);
            rowRect.anchoredPosition = new Vector2(0, y);

            UIFactory.CreateLabel("Lbl_" + label, rowRect, label, 16,
                new Vector2(-(ContentW - LabelW) * 0.5f, 0), new Vector2(LabelW, RowH), TextAnchor.MiddleLeft);

            var valueLabel = UIFactory.CreateLabel("Val_" + label, rowRect, FormatMultiplier(value), 16,
                new Vector2(ContentW * 0.5f - 20, 0), new Vector2(40, RowH), TextAnchor.MiddleRight);

            var slider = UIFactory.CreateSlider("Sld_" + label, rowRect,
                KitchenSettings.MIN_INPUT_SPEED, KitchenSettings.MAX_INPUT_SPEED, value,
                new Vector2(ContentW * 0.5f - ControlW * 0.5f - 20, 0), new Vector2(110, RowH),
                v =>
                {
                    onChanged(v);
                    if (valueLabel != null) valueLabel.text = FormatMultiplier(v);
                });

            y -= RowStep;
            return slider;
        }

        private static string FormatMultiplier(float v) =>
            v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "×";

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
                ExpressionParser.IsValidDimensionChar(ch, allowDecimal: isDecimal) ? ch : '\0';
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
        private TMP_InputField? _edgeThresholdField;
        private readonly Dictionary<string, TMPro.TextMeshProUGUI> _rowLabels = new();

        private const string WallOutlineId = "Контур стен";
        private const string ObjectOutlineId = "Контур объектов";

        private int _viewPresetTab;
        private readonly List<Button> _viewPresetButtons = new();
        private readonly Dictionary<ViewField, Toggle> _viewToggles = new();
        private readonly Dictionary<ViewField, string> _viewToggleKeys = new();

        private void UpdateDependentStates()
        {
            var s = KitchenSettings.Instance;
            if (s == null) return;
            SetFieldEnabled(_gridStepField, "Шаг сетки", s.GridEnabled);
            SetFieldEnabled(_snapThresholdField, "Порог привязки", s.SnapEnabled);
            SetFieldEnabled(_autoSaveIntervalField, "Интервал автосохранения", s.AutoSave);
            RefreshViewTab();
        }

        /// <summary>Значения и доступность тумблеров вида берутся из одной
        /// таблицы, что и рендер — иначе UI разрешал бы то, что сцена всё равно
        /// проигнорирует. Форсированные режимом поля показывают своё реальное
        /// значение, но серыми; в фоторежиме серое всё.</summary>
        private void RefreshViewTab()
        {
            var edited = EditedPresetMode;
            var state = ViewResolver.Resolve(edited);

            for (int i = 0; i < _viewPresetButtons.Count; i++)
            {
                var btn = _viewPresetButtons[i];
                if (btn == null) continue;
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = i == _viewPresetTab ? ActiveTabColor : InactiveTabColor;
                var caption = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (caption == null) continue;
                // Фоторежим правит пресет обычного — он и помечается текущим.
                int currentIdx = EditModeManager.Mode == EditMode.Room ? 1 : 0;
                caption.text = (i == 1 ? "Помещение" : "Обычный") + (i == currentIdx ? " (текущий)" : "");
            }

            foreach (var kv in _viewToggles)
            {
                var field = kv.Key;
                var toggle = kv.Value;
                if (toggle == null) continue;

                toggle.SetIsOnWithoutNotify(state.Get(field));

                // Подопция без включённого родителя бессмысленна — гасим её
                // (правило дерева из UI-GUIDELINES).
                var parent = ViewResolver.ParentOf(field);
                bool parentOn = parent == null || state.Get(parent.Value);
                bool enabled = parentOn
                    && ViewResolver.IsEditable(EditModeManager.Mode, edited, field);
                SetToggleEnabled(toggle, _viewToggleKeys[field], enabled);
            }
        }

        private void SetFieldEnabled(TMP_InputField? field, string labelKey, bool enabled)
        {
            if (field == null) return;
            field.interactable = enabled;
            if (field.textComponent != null)
                field.textComponent.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
            SetLabelEnabled(labelKey, enabled);
        }

        private void SetToggleEnabled(Toggle? toggle, string labelKey, bool enabled)
        {
            if (toggle == null) return;
            toggle.interactable = enabled;
            // Галочка — акцентный квадрат (UIFactory.CreateCheckmark); гасим её
            // цветом, а не подменяем на цвет текста.
            if (toggle.graphic != null)
                toggle.graphic.color = enabled ? UIStyle.Accent : UIStyle.TextDisabled;
            SetLabelEnabled(labelKey, enabled);
        }

        private void SetLabelEnabled(string labelKey, bool enabled)
        {
            if (_rowLabels.TryGetValue(labelKey, out var lbl) && lbl != null)
                lbl.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
        }

        // ── Public API ──────────────────────────────────────

        public string WindowId => "settings";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
            if (visible)
            {
                SyncPhotoActiveToggle();
                UpdateDependentStates();
            }
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
