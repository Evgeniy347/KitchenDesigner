using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class LevelNavigation
    {
        public static string AdjacentLevelId(Level[] levels, string currentId, int direction)
        {
            if (levels == null || levels.Length == 0) return currentId ?? "";

            var sorted = new List<Level>(levels);
            sorted.Sort((a, b) => a.floorElevationMm.CompareTo(b.floorElevationMm));

            int idx = sorted.FindIndex(l => l != null && l.id == currentId);
            if (idx < 0) idx = 0;

            int step = direction > 0 ? 1 : (direction < 0 ? -1 : 0);
            int next = idx + step;
            if (next < 0 || next >= sorted.Count) return sorted[idx].id;
            return sorted[next].id;
        }
    }
}
