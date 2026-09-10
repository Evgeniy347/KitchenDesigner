using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class DayNightPanelUI : MonoBehaviour, IProjectWindow
    {
        public static DayNightPanelUI? Instance { get; private set; }

        public string WindowId => "dayNight";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        private GameObject? _root;
        private TMP_Text? _timeLabel;
        private TMP_Text? _azimuthLabel;
        private TMP_Text? _intensityLabel;
        private Slider? _timeSlider;
        private Slider? _azimuthSlider;
        private Slider? _intensitySlider;

        private void Awake()
        {
            Instance = this;
        }

        internal const float PanelHeight = 272f;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("DayNightPanel", canvas, Vector2.zero,
                new Vector2(300, PanelHeight));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, UIStyle.DragStripHeight);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("DnTitle", panel.transform, "День / Ночь", 20,
                new Vector2(0, 104), new Vector2(280, 28), TextAnchor.MiddleCenter);

            float y = 66f;
            _timeLabel = UIFactory.CreateLabel("DnTimeL", panel.transform, "", 15,
                new Vector2(0, y), new Vector2(264, 22), TextAnchor.MiddleLeft);
            _timeSlider = UIFactory.CreateSlider("DnTime", panel.transform, 0f, 24f,
                SunController.TimeOfDay, new Vector2(0, y - 24), new Vector2(264, 22),
                v => { SunController.SetTimeOfDay(v); RefreshLabels(); });

            y -= 58f;
            _azimuthLabel = UIFactory.CreateLabel("DnAzimuthL", panel.transform, "", 15,
                new Vector2(0, y), new Vector2(264, 22), TextAnchor.MiddleLeft);
            _azimuthSlider = UIFactory.CreateSlider("DnAzimuth", panel.transform, 0f, 360f,
                SunController.Azimuth, new Vector2(0, y - 24), new Vector2(264, 22),
                v => { SunController.SetAzimuth(v); RefreshLabels(); });

            y -= 58f;
            _intensityLabel = UIFactory.CreateLabel("DnIntensityL", panel.transform, "", 15,
                new Vector2(0, y), new Vector2(264, 22), TextAnchor.MiddleLeft);
            _intensitySlider = UIFactory.CreateSlider("DnIntensity", panel.transform, 0f, 2f,
                SunController.Intensity, new Vector2(0, y - 24), new Vector2(264, 22),
                v => { SunController.SetIntensity(v); RefreshLabels(); });

            UIFactory.CreateButton("DnReset", panel.transform, "Сброс (полдень)",
                new Vector2(0, y - 62), new Vector2(264, 30), ResetSun);

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            RefreshLabels();
            _root.SetActive(false);
        }

        private void ResetSun()
        {
            SunController.Reset();
            if (_timeSlider != null) _timeSlider.SetValueWithoutNotify(SunController.TimeOfDay);
            if (_azimuthSlider != null) _azimuthSlider.SetValueWithoutNotify(SunController.Azimuth);
            if (_intensitySlider != null) _intensitySlider.SetValueWithoutNotify(SunController.Intensity);
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (_timeLabel != null)
            {
                int h = Mathf.FloorToInt(SunController.TimeOfDay);
                int m = Mathf.FloorToInt((SunController.TimeOfDay - h) * 60f);
                string phase = SunController.IsNight ? "ночь" : "день";
                _timeLabel.text = $"Время: {h:00}:{m:00} ({phase})";
            }
            if (_azimuthLabel != null)
                _azimuthLabel.text = $"Азимут солнца: {SunController.Azimuth:0}°";
            if (_intensityLabel != null)
                _intensityLabel.text = $"Яркость: {SunController.Intensity:0.00}";
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        public void Toggle()
        {
            if (_root != null) SetVisible(!_root.activeSelf);
        }

        private void OnDestroy() => ProjectWindows.Unregister(this);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && IsVisible)
                TryHideFromEscape();
        }

        internal void TryHideFromEscape()
        {
            if (OwnsEscape()) SetVisible(false);
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                DayNightOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
                HintOpen = HintBubbleUI.IsOpen,
                ConfirmArmed = ConfirmDeleteButton.AnyArmed,
                ContextMenuOpen = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen,
                GroupMenuOpen = GroupMenuUI.Instance != null && GroupMenuUI.Instance.IsOpen,
                CatalogTileSelected = SidebarUI.CatalogClaimsEscape,
            }) == EscapeOwner.DayNightPanel;
    }
}
