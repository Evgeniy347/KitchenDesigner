using UnityEngine;

namespace KitchenDesigner.Core.Audio
{
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer? Instance { get; private set; }

        private MusicOutput? _output;
        private bool _wantsPlayback;
        private int _loadedTrack = -1;

        public bool IsPlaying => _wantsPlayback;

        public string CurrentTrackName => MusicPlaylist.DisplayName(MusicState.Track);

        private void Awake()
        {
            Instance = this;
            EnsureAudioListener();
            _output = new MusicOutput(gameObject);
            _output.Volume = MusicState.Volume;
            MusicState.Changed += ApplyState;
        }

        private void Start() => Play();

        private void OnDestroy()
        {
            MusicState.Changed -= ApplyState;
            if (Instance == this) Instance = null;
        }

        private void EnsureAudioListener()
        {
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
        }

        public void TogglePlay()
        {
            if (_wantsPlayback) Pause();
            else Play();
        }

        public void Play()
        {
            if (_output == null) return;
            bool resuming = _loadedTrack == MusicState.Track && _output.HasClip;
            LoadCurrentTrack();
            if (!_output.HasClip) return;
            _wantsPlayback = true;
            if (resuming) _output.Resume();
            _output.Start();
        }

        public void Pause()
        {
            _wantsPlayback = false;
            if (_output != null) _output.Pause();
        }

        public void Skip(int delta)
        {
            MusicState.Track = MusicState.Track + delta;
            if (_wantsPlayback) Play();
        }

        private void ApplyState()
        {
            if (_output == null) return;
            _output.Volume = MusicState.Volume;
            if (_loadedTrack != MusicState.Track && _wantsPlayback) Play();
        }

        private void LoadCurrentTrack()
        {
            if (_output == null || _loadedTrack == MusicState.Track) return;
            string path = MusicPlaylist.ResourcePath(MusicState.Track);
            if (!_output.LoadClip(path))
            {
                Debug.LogWarning($"[Music] Трек не найден: {path}");
                return;
            }
            _loadedTrack = MusicState.Track;
        }

        private void Update()
        {
            if (!_wantsPlayback || _output == null || !_output.HasClip) return;
            if (_output.IsRunning) return;
            Skip(1);
        }
    }
}
