using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class FaceCache
    {
        private static Dictionary<KitchenElement, Face[]>? _cache;

        public static bool Enabled => _cache != null;

        public static void Set(Dictionary<KitchenElement, Face[]> cache)
        {
            _cache = cache;
        }

        public static void Clear()
        {
            _cache = null;
        }

        public static Face[] GetFaces(KitchenElement element)
        {
            if (_cache != null && _cache.TryGetValue(element, out var faces))
                return faces;
            return element.GetFaces();
        }
    }
}
