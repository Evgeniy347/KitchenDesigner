using KitchenDesigner.Core.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class MusicPanelUI : MonoBehaviour, IProjectWindow
    {
        public static MusicPanelUI? Instance { get; private set; }

        public string WindowId => "music";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        private GameObject? _root;
        private TMP_Text? _trackLabel;
        private TMP_Text? _volumeValue;
        private Button? _playButton;
        private Image? _playIcon;
        private Slider? _volumeSlider;
        private bool _shownAsPlaying;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            Instance = this;

            var chrome = WindowChrome.Create(canvas, "MusicPanel", Loc.T("window.music.title"),
                new Vector2(UIStyle.ToolPanelW, UIStyle.ToolPanelW), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = () => SetVisible(false),
                });
            var panel = chrome.Panel;
            UIFactory.AnchorTopRight(panel);
            panel.anchoredPosition = new Vector2(-UIStyle.Space3, -(UIStyle.ToolbarH + UIStyle.Space3));
            _root = panel.gameObject;
            ProjectWindows.Register(this);

            var body = chrome.CreateBody();
            var rows = new FormRows(body.Content, RowDensity.Tool);
            rows.Custom(BuildTransport(rows.Metrics.Width), UIStyle.ControlHCompact);
            rows.Gap(UIStyle.Space1);
            (_, _volumeSlider, _volumeValue) = rows.Slider("MuVolume", Loc.T("window.music.volumeLabel"), 0f, 100f,
                MusicState.VolumePct, VolumeText, v => MusicState.VolumePct = Mathf.RoundToInt(v),
                wholeNumbers: true);
            chrome.FitHeightTo(rows.Relayout());
            body.Fit();

            MusicState.Changed += Refresh;
            Refresh();
            _root.SetActive(false);
        }

        private RectTransform BuildTransport(float width)
        {
            var row = UIFactory.CreateRect("MuTransport", transform);
            row.sizeDelta = new Vector2(width, UIStyle.ControlHCompact);
            float x = 0f;
            Transport(row, "MuPrev", IconFactory.TrackPrev, Loc.T("window.music.prev"), () => Skip(-1), ref x);
            _playButton = Transport(row, "MuPlay", IconFactory.Play, Loc.T("window.music.playPause"), TogglePlay, ref x);
            _playIcon = _playButton.transform.Find("MuPlay_Icon")?.GetComponent<Image>();
            Transport(row, "MuNext", IconFactory.TrackNext, Loc.T("window.music.next"), () => Skip(1), ref x);

            float trackX = x + UIStyle.Space2;
            _trackLabel = UIFactory.CreateLabel("MuTrack", row, "", UIStyle.FontBody, Vector2.zero,
                new Vector2(width - trackX, UIStyle.ControlHCompact), TextAnchor.MiddleLeft);
            _trackLabel.enableWordWrapping = false;
            _trackLabel.overflowMode = TextOverflowModes.Ellipsis;
            var trt = _trackLabel.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(trackX, 0f);
            return row;
        }

        private static Button Transport(RectTransform row, string name, Sprite icon, string tooltip,
            System.Action onClick, ref float x)
        {
            float size = UIStyle.ControlHCompact;
            var button = UIFactory.CreateIconButton(name, row, icon, Vector2.zero, new Vector2(size, size), onClick,
                size - UIStyle.IconSizeSmall);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            TooltipUI.Attach(button.gameObject, tooltip);
            x += size + UIStyle.Space1;
            return button;
        }

        internal static string VolumeText(float v) =>
            NumberFormat.WithUnit(NumberFormat.Integer(Mathf.Round(v)), "%");

        private static void Skip(int delta)
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.Skip(delta);
            else MusicState.Track += delta;
        }

        private void TogglePlay()
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.TogglePlay();
            RefreshPlayIcon();
        }

        private void Refresh()
        {
            if (_trackLabel != null) _trackLabel.text = MusicPlaylist.DisplayName(MusicState.Track);
            if (_volumeValue != null) _volumeValue.text = VolumeText(MusicState.VolumePct);
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(MusicState.VolumePct);
            RefreshPlayIcon();
        }

        private void RefreshPlayIcon()
        {
            bool playing = MusicPlayer.Instance != null && MusicPlayer.Instance.IsPlaying;
            if (_playButton != null)
                ButtonRoles.Paint(_playButton, playing ? ButtonRole.Primary : ButtonRole.Secondary);
            if (_playIcon == null) return;
            _playIcon.sprite = playing ? IconFactory.Pause : IconFactory.Play;
            _shownAsPlaying = playing;
        }

        public void Toggle() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible) Refresh();
            _root.SetActive(visible);
        }

        private void OnDestroy()
        {
            MusicState.Changed -= Refresh;
            ProjectWindows.Unregister(this);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TryHideFromEscape();
            bool playing = MusicPlayer.Instance != null && MusicPlayer.Instance.IsPlaying;
            if (playing != _shownAsPlaying) Refresh();
        }

        internal void TryHideFromEscape()
        {
            if (OwnsEscape()) SetVisible(false);
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                ModalOpen = ModalPresence.IsOpen,
                MusicOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
                HintOpen = HintBubbleUI.IsOpen,
                ConfirmArmed = ConfirmDeleteButton.AnyArmed,
                ContextMenuOpen = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen,
                GroupMenuOpen = GroupMenuUI.Instance != null && GroupMenuUI.Instance.IsOpen,
                CatalogTileSelected = SidebarUI.CatalogClaimsEscape,
                DayNightOpen = DayNightPanelUI.Instance != null && DayNightPanelUI.Instance.IsVisible,
            }) == EscapeOwner.MusicPanel;
    }
}
