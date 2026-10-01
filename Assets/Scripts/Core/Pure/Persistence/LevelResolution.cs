using System;

namespace KitchenDesigner.Core
{
    public static class LevelResolution
    {
        public const string DefaultLevelId = "1";
        public static string DefaultLevelName => Loc.T("level.firstFloorName");

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

        public static Level TopLevel(Level[] effectiveLevels)
        {
            var top = effectiveLevels[0];
            for (int i = 1; i < effectiveLevels.Length; i++)
                if (effectiveLevels[i].floorElevationMm > top.floorElevationMm) top = effectiveLevels[i];
            return top;
        }

        public static Level BottomLevel(Level[] effectiveLevels)
        {
            var bottom = effectiveLevels[0];
            for (int i = 1; i < effectiveLevels.Length; i++)
                if (effectiveLevels[i].floorElevationMm < bottom.floorElevationMm) bottom = effectiveLevels[i];
            return bottom;
        }
    }
}
