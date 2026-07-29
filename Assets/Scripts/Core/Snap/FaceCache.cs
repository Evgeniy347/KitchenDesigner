using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    /// <summary>Прозрачный кэш граней для чистых функций прилипания.
    /// Позволяет длительным тестам (1 мм sweep) не пересчитывать грани
    /// неподвижных соседей тысячи раз. Включается/выключается явно из теста;
    /// в рантайме выключен, чтобы не хранить устаревшее состояние.</summary>
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
