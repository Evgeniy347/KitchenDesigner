namespace KitchenDesigner.Core
{
    public static class SceneRevision
    {
        public static int Version { get; private set; }

        public static void Bump() => Version++;

        public static bool Changed(ref int seen)
        {
            if (seen == Version) return false;
            seen = Version;
            return true;
        }

        public static void Reset() => Version = 0;
    }
}
