namespace KitchenDesigner.Core.Audio
{
    public static class MusicPlaylist
    {
        public const int TRACK_COUNT = 5;
        public const string RESOURCE_FOLDER = "Music";

        public static int Wrap(int track) => ((track % TRACK_COUNT) + TRACK_COUNT) % TRACK_COUNT;

        public static string ResourcePath(int track) =>
            RESOURCE_FOLDER + "/track-" + (Wrap(track) + 1).ToString("00");

        public static string DisplayName(int track) => Loc.T("window.music.trackPrefix") + (Wrap(track) + 1);
    }
}
