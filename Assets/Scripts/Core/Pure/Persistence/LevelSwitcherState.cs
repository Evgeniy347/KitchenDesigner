using System;

namespace KitchenDesigner.Core
{
    public readonly struct LevelSwitcherState : IEquatable<LevelSwitcherState>
    {
        public const int MinLevelsForArrows = 2;

        public bool ShowArrows { get; }
        public bool CanGoUp { get; }
        public bool CanGoDown { get; }

        private LevelSwitcherState(bool showArrows, bool canGoUp, bool canGoDown)
        {
            ShowArrows = showArrows;
            CanGoUp = canGoUp;
            CanGoDown = canGoDown;
        }

        public static LevelSwitcherState Of(Level[]? levels, string? currentId)
        {
            if (levels == null || levels.Length < MinLevelsForArrows) return new LevelSwitcherState(false, false, false);

            string here = LevelNavigation.AdjacentLevelId(levels, currentId ?? "", 0);
            bool canGoUp = LevelNavigation.AdjacentLevelId(levels, currentId ?? "", +1) != here;
            bool canGoDown = LevelNavigation.AdjacentLevelId(levels, currentId ?? "", -1) != here;
            return new LevelSwitcherState(true, canGoUp, canGoDown);
        }

        public bool Equals(LevelSwitcherState other) =>
            ShowArrows == other.ShowArrows && CanGoUp == other.CanGoUp && CanGoDown == other.CanGoDown;

        public override bool Equals(object? obj) => obj is LevelSwitcherState other && Equals(other);

        public override int GetHashCode() =>
            (ShowArrows ? 1 : 0) | (CanGoUp ? 2 : 0) | (CanGoDown ? 4 : 0);
    }
}
