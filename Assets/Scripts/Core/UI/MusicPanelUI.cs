using KitchenDesigner.Core.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class MusicPanelUI : MonoBehaviour, IProjectWindow
    {
        internal const float PanelWidth = 280f;
        internal const float PanelHeight = 184f;

        public string WindowId => "music";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        private GameObject? _root;
        private TMP_Text? _trackLabel;
        private TMP_Text? _volumeLabel;
        private Image? _playIcon;
        private Slider? _volumeSlider;
        private bool _shownAsPlaying;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("MusicPanel", canvas, Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(-10, -60);
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, 40f);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("MuTitle", panel.transform, "Музыка", UIStyle.FontTitle,
                new Vector2(0, 64), new Vector2(240, 28), TextAnchor.MiddleCenter);
            _trackLabel = UIFactory.CreateLabel("MuTrack", panel.transform, "", UIStyle.FontSection,
                new Vector2(0, 32), new Vector2(248, 24), TextAnchor.MiddleCenter);

            BuildTransport(panel.transform);

            _volumeLabel = UIFactory.CreateLabel("MuVolumeL", panel.transform, "", UIStyle.FontSection,
                new Vector2(0, -44), new Vector2(248, 22), TextAnchor.MiddleLeft);
            _volumeSlider = UIFactory.CreateSlider("MuVolume", panel.transform, 0f, 100f,
                MusicState.VolumePct, new Vector2(0, -68), new Vector2(248, 22),
                v => MusicState.VolumePct = Mathf.RoundToInt(v));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            MusicState.Changed += Refresh;
            Refresh();
            _root.SetActive(false);
        }

        private void BuildTransport(Transform panel)
        {
            const float size = 40f;
            UIFactory.CreateIconButton("MuPrev", panel, IconFactory.TrackPrev,
                new Vector2(-56, -6), new Vector2(size, size), () => Skip(-1));
            TooltipUI.Attach(panel.Find("MuPrev")!.gameObject, "Предыдущий трек");

            var play = UIFactory.CreateIconButton("MuPlay", panel, IconFactory.Play,
                new Vector2(0, -6), new Vector2(size, size), TogglePlay);
            _playIcon = play.transform.Find("MuPlay_Icon")?.GetComponent<Image>();
            TooltipUI.Attach(play.gameObject, "Воспроизведение и пауза");

            UIFactory.CreateIconButton("MuNext", panel, IconFactory.TrackNext,
                new Vector2(56, -6), new Vector2(size, size), () => Skip(1));
            TooltipUI.Attach(panel.Find("MuNext")!.gameObject, "Следующий трек");
        }

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
            if (_volumeLabel != null) _volumeLabel.text = $"Громкость: {MusicState.VolumePct} %";
            if (_volumeSlider != null) _volumeSlider.SetValueWithoutNotify(MusicState.VolumePct);
            RefreshPlayIcon();
        }

        private void RefreshPlayIcon()
        {
            bool playing = MusicPlayer.Instance != null && MusicPlayer.Instance.IsPlaying;
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
            if (Input.GetKeyDown(KeyCode.Escape)) { SetVisible(false); return; }
            bool playing = MusicPlayer.Instance != null && MusicPlayer.Instance.IsPlaying;
            if (playing != _shownAsPlaying) Refresh();
        }
    }
}
