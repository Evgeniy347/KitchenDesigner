namespace KitchenDesigner.Core
{
    public static class LevelSwitch
    {
        public static void Up() => To(LevelNavigation.AdjacentLevelId(
            LevelRegistry.Snapshot(), LevelRegistry.CurrentId, +1));

        public static void Down() => To(LevelNavigation.AdjacentLevelId(
            LevelRegistry.Snapshot(), LevelRegistry.CurrentId, -1));

        public static void To(string levelId)
        {
            if (string.IsNullOrEmpty(levelId) || levelId == LevelRegistry.CurrentId) return;

            var target = Find(levelId);
            if (target == null) return;

            int fromMm = LevelRegistry.Current.floorElevationMm;
            LevelRegistry.CurrentId = levelId;

            if (CameraController.Instance != null)
                CameraController.Instance.FollowLevelChange(fromMm, target.floorElevationMm);

            SelectionManager.Instance?.DeselectAll();
            SceneVisibilityManager.Invalidate();
        }

        private static Level? Find(string id)
        {
            foreach (var level in LevelRegistry.Snapshot())
                if (level != null && level.id == id) return level;
            return null;
        }
    }
}
