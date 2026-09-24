namespace KitchenDesigner.Core
{
    public static class SceneScanCounter
    {
        public const int MostScansRemembered = SceneScanLog.MostPositionsRemembered;

        public static long Scans => SceneScanLog.Position;

        public static long Shares => SceneScanLog.SharesPosition;

        public static void NoteShare() => SceneScanLog.NoteSharePosition();

        public static void Note(string where) => SceneScanLog.NotePosition(where);

        public static string Since(long mark) => SceneScanLog.SincePosition(mark);
    }
}
