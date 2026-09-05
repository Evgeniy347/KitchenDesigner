using UnityEngine;

namespace KitchenDesigner.Core.Audio
{
    public class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer? Instance { get; private set; }

        private AudioSource? _source;
        private bool _wantsPlayback;
        private int _loadedTrack = -1;

        public bool IsPlaying => _wantsPlayback;

        public string CurrentTrackName => MusicPlaylist.DisplayName(MusicState.Track);

        private void Awake()
        {
            Instance = this;
            EnsureAudioListener();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.volume = MusicState.Volume;
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
            if (_source == null) return;
            bool resuming = _loadedTrack == MusicState.Track && _source.clip != null;
            LoadCurrentTrack();
            if (_source.clip == null) return;
            _wantsPlayback = true;
            if (resuming) _source.UnPause();
            if (!_source.isPlaying) _source.Play();
        }

        public void Pause()
        {
            _wantsPlayback = false;
            if (_source != null) _source.Pause();
        }

        public void Skip(int delta)
        {
            MusicState.Track = MusicState.Track + delta;
            if (_wantsPlayback) Play();
        }

        private void ApplyState()
        {
            if (_source == null) return;
            _source.volume = MusicState.Volume;
            if (_loadedTrack != MusicState.Track && _wantsPlayback) Play();
        }

        private void LoadCurrentTrack()
        {
            if (_source == null || _loadedTrack == MusicState.Track) return;
            var clip = Resources.Load<AudioClip>(MusicPlaylist.ResourcePath(MusicState.Track));
            if (clip == null)
            {
                Debug.LogWarning($"[Music] Трек не найден: {MusicPlaylist.ResourcePath(MusicState.Track)}");
                return;
            }
            _source.clip = clip;
            _loadedTrack = MusicState.Track;
        }

        private void Update()
        {
            if (!_wantsPlayback || _source == null || _source.clip == null) return;
            if (_source.isPlaying) return;
            Skip(1);
        }
    }
}
