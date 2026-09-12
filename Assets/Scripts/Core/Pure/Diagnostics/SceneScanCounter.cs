using System.Threading;

namespace KitchenDesigner.Core
{
    public static class SceneScanCounter
    {
        private static long _scans;

        public static long Scans => Interlocked.Read(ref _scans);

        public static void Note() => Interlocked.Increment(ref _scans);
    }
}
