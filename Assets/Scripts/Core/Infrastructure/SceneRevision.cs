using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KitchenDesigner.Core
{
    public static class SceneRevision
    {
        public static int Version { get; private set; }

        public static void Bump([CallerMemberName] string? source = null)
        {
            Version++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _bumps++;
            _bumpsBySource.TryGetValue(source ?? "?", out int n);
            _bumpsBySource[source ?? "?"] = n + 1;
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
            _bumpsBySource.Clear();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static int _bumps;
        private static readonly Dictionary<string, int> _bumpsBySource = new Dictionary<string, int>();

        public static int TakeBumps()
        {
            int n = _bumps;
            _bumps = 0;
            return n;
        }

        public static IReadOnlyDictionary<string, int> BumpsBySource => _bumpsBySource;
#endif
    }
}
