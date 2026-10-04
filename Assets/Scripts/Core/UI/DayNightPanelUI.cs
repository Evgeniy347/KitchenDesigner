using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class DayNightPanelUI : MonoBehaviour, IProjectWindow
    {
        public static DayNightPanelUI? Instance { get; private set; }

        public string WindowId => "dayNight";
        public RectTransform? WindowRect => _chrome?.Panel;
        public bool HeightAdjustable => false;

        private WindowChrome? _chrome;
        private SliderFieldRow? _time;
        private SliderFieldRow? _azimuth;
        private SliderFieldRow? _intensity;

        internal SliderFieldRow? TimeRow => _time;
        internal SliderFieldRow? AzimuthRow => _azimuth;
        internal SliderFieldRow? IntensityRow => _intensity;

        private void Awake()
        {
            Instance = this;
        }

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "DayNightPanel", Loc.T("window.dayNight.title"),
                new Vector2(UIStyle.ToolPanelW, UIStyle.ToolPanelW), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = () => SetVisible(false),
                    RuledHeader = true,
                });
            var panel = _chrome.Panel;
            UIFactory.AnchorTopRight(panel);
            panel.anchoredPosition = new Vector2(-UIStyle.Space3, -(UIStyle.ToolbarH + UIStyle.Space3));
            ProjectWindows.Register(this);

            var body = _chrome.CreateBody();
            var rows = new FormRows(body.Content, RowDensity.Tool);
            _time = SliderFieldRow.Add(rows, body.Content, "DnTime", Loc.T("window.dayNight.timeLabel"),
                0f, ClockTime.HoursPerDay, SunController.TimeOfDay, "", ClockTime.Format, ParseTime,
                SunController.SetTimeOfDay);
            _azimuth = SliderFieldRow.Add(rows, body.Content, "DnAzimuth", Loc.T("window.dayNight.azimuthLabel"),
                0f, 360f, SunController.Azimuth, "°", AzimuthText, ParseNumber, SunController.SetAzimuth);
            _intensity = SliderFieldRow.Add(rows, body.Content, "DnIntensity",
                Loc.T("window.dayNight.intensityLabel"), 0f, 2f, SunController.Intensity, "", IntensityText,
                ParseNumber, SunController.SetIntensity);
            rows.Gap(UIStyle.Space2);
            rows.Custom(BuildReset(), UIStyle.ControlHCompact);
            _chrome.FitHeightTo(rows.Relayout());
            body.Fit();

            panel.gameObject.SetActive(false);
        }

        internal static string AzimuthText(float degrees) => NumberFormat.Integer(Mathf.Round(degrees));

        internal static string IntensityText(float intensity) => NumberFormat.Fixed(intensity, 2);

        private static (bool ok, float value) ParseTime(string text) =>
            (ClockTime.TryParse(text, out float hours), hours);

        private static (bool ok, float value) ParseNumber(string text) =>
            (NumberFormat.TryParse(text, out double value), (float)value);

        private RectTransform BuildReset()
        {
            var row = UIFactory.CreateRect("Row_DnReset", _chrome!.Panel);
            row.sizeDelta = new Vector2(_chrome.BodyWidth, UIStyle.ControlHCompact);
            var button = UIFactory.CreateButton("DnReset", row, Loc.T("window.dayNight.reset"),
                Vector2.zero, new Vector2(0f, UIStyle.ControlHCompact), ResetSun);
            ButtonRoles.Paint(button, ButtonRole.Link);
            var label = button.GetComponentInChildren<TMP_Text>();
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(LayoutDirection.IsRtl ? 1f : 0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Ceil(label.GetPreferredValues(label.text).x) + 2f * UIStyle.Space2,
                UIStyle.ControlHCompact);
            return row;
        }

        private void ResetSun()
        {
            SunController.Reset();
            _time?.SetValue(SunController.TimeOfDay);
            _azimuth?.SetValue(SunController.Azimuth);
            _intensity?.SetValue(SunController.Intensity);
        }

        public bool IsVisible => _chrome != null && !IsGone(_chrome.Panel) && _chrome.Panel.gameObject.activeSelf;

        private static bool IsGone(RectTransform panel) => panel == null;

        public void SetVisible(bool visible)
        {
            if (_chrome != null && !IsGone(_chrome.Panel)) _chrome.Panel.gameObject.SetActive(visible);
        }

        public void Toggle()
        {
            if (_chrome != null && !IsGone(_chrome.Panel)) SetVisible(!IsVisible);
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
                ModalOpen = ModalPresence.IsOpen,
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
