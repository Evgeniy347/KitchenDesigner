namespace KitchenDesigner.Core
{
    public static class LevelVisibility
    {
        public static LevelVisibilityDecision Decide(
            int elementLevelElevationMm, int currentLevelElevationMm, NeighbourLevelsMode mode)
        {
            if (elementLevelElevationMm == currentLevelElevationMm)
                return LevelVisibilityDecision.Visible;

            if (elementLevelElevationMm > currentLevelElevationMm)
                return LevelVisibilityDecision.Hidden;

            switch (mode)
            {
                case NeighbourLevelsMode.Dim: return LevelVisibilityDecision.Dimmed;
                case NeighbourLevelsMode.Hide: return LevelVisibilityDecision.Hidden;
                default: return LevelVisibilityDecision.Visible;
            }
        }
    }
}
