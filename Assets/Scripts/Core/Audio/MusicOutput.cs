using UnityEngine;

namespace KitchenDesigner.Core.Audio
{
    public class MusicOutput
    {
        private readonly AudioSource _source;

        public MusicOutput(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.mute = AudioOutputPolicy.Silent;
        }

        public bool HasClip => _source.clip != null;

        public bool IsRunning => _source.isPlaying;

        public float Volume
        {
            set => _source.volume = value;
        }

        public bool LoadClip(string resourcePath)
        {
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null) return false;
            _source.clip = clip;
            return true;
        }

        public void Start()
        {
            _source.mute = AudioOutputPolicy.Silent;
            if (!_source.isPlaying) _source.Play();
        }

        public void Resume()
        {
            _source.mute = AudioOutputPolicy.Silent;
            _source.UnPause();
        }

        public void Pause() => _source.Pause();
    }
}
