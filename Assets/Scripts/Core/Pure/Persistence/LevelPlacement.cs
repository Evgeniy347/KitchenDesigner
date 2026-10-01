namespace KitchenDesigner.Core
{
    public static class LevelPlacement
    {
        public static Level NextAbove(Level[] levels, int defaultHeightMm)
        {
            var top = TopOf(levels);
            int elevation = top != null ? top.floorElevationMm + top.heightMm : 0;
            string id = NextId(levels);
            string name = Loc.F("level.defaultName", CountAtOrAbove(levels, 0) + 1);
            return new Level(id, name, elevation, defaultHeightMm);
        }

        private static Level? TopOf(Level[] levels)
        {
            Level? top = null;
            if (levels == null) return null;
            foreach (var level in levels)
                if (level != null && (top == null || level.floorElevationMm > top.floorElevationMm))
                    top = level;
            return top;
        }

        private static int CountAtOrAbove(Level[] levels, int elevationMm)
        {
            int count = 0;
            if (levels == null) return count;
            foreach (var level in levels)
                if (level != null && level.floorElevationMm >= elevationMm) count++;
            return count;
        }

        private static string NextId(Level[] levels)
        {
            int max = 0;
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    if (level == null) continue;
                    if (int.TryParse(level.id, out var n) && n > max) max = n;
                }
            }
            return (max + 1).ToString();
        }
    }
}
