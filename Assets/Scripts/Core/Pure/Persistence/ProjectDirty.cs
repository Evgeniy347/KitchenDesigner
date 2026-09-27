namespace KitchenDesigner.Core
{
    public static class ProjectDirty
    {
        private static string? _settingsStamp;

        public static int Version { get; private set; }

        public static void Mark() => Version++;

        public static void NoteSettings(string stamp)
        {
            if (_settingsStamp != null && stamp != _settingsStamp) Mark();
            _settingsStamp = stamp;
        }
    }
}
