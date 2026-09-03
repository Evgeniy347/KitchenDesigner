using System;

namespace KitchenDesigner.Core.Audio
{
    public static class MusicState
    {
        public const int DEFAULT_VOLUME_PCT = 60;

        private static int _track;
        private static int _volumePct = DEFAULT_VOLUME_PCT;

        public static event Action? Changed;

        public static int Track
        {
            get => _track;
            set
            {
                int wrapped = MusicPlaylist.Wrap(value);
                if (wrapped == _track) return;
                _track = wrapped;
                Changed?.Invoke();
            }
        }

        public static int VolumePct
        {
            get => _volumePct;
            set
            {
                int clamped = value < 0 ? 0 : value > 100 ? 100 : value;
                if (clamped == _volumePct) return;
                _volumePct = clamped;
                Changed?.Invoke();
            }
        }

        public static float Volume => _volumePct / 100f;

        public static void Reset()
        {
            Track = 0;
            VolumePct = DEFAULT_VOLUME_PCT;
        }
    }
}
