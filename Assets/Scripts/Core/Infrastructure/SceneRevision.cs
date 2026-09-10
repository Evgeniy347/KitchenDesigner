namespace KitchenDesigner.Core
{
    public static class SceneRevision
    {
        public static int Version { get; private set; }

        public static void Bump()
        {
            Version++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bumps++;
#endif
        }

        public static bool Changed(ref int seen)
        {
            if (seen == Version) return false;
            seen = Version;
            return true;
        }

        public static void Reset()
        {
            Version = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bumps = 0;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _bumps;

        public static int TakeBumps()
        {
            int n = _bumps;
            _bumps = 0;
            return n;
        }
#endif
    }
}
