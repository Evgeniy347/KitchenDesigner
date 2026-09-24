using System;

namespace KitchenDesigner.Core
{
    public static class LevelResolution
    {
        public const string DefaultLevelId = "1";
        public const string DefaultLevelName = "1 этаж";

        public static Level[] EffectiveLevels(Level[]? levels, int defaultFloorHeightMm)
        {
            if (levels != null && levels.Length > 0) return levels;
            return new[] { new Level(DefaultLevelId, DefaultLevelName, 0, defaultFloorHeightMm) };
        }

        public static Level ResolveElementLevel(string? levelId, Level[] effectiveLevels)
        {
            if (!string.IsNullOrEmpty(levelId))
            {
                foreach (var level in effectiveLevels)
                    if (level != null && string.Equals(level.id, levelId, StringComparison.Ordinal))
                        return level;
            }
            return effectiveLevels[0];
        }
    }
}
