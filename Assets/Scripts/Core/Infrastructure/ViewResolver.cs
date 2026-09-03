namespace KitchenDesigner.Core
{
    public static class ViewResolver
    {
        private static readonly ViewPreset Fallback = new ViewPreset();

        private const int RoomLocked =
            (1 << (int)ViewField.Walls)
            | (1 << (int)ViewField.LowerNearWalls)
            | (1 << (int)ViewField.LowerAllWalls)
            | (1 << (int)ViewField.HideOpeningsOnLoweredWalls);

        public static ViewPreset PresetFor(EditMode mode, KitchenSettings? s)
        {
            if (s == null) return Fallback;
            return mode switch
            {
                EditMode.Room => s.RoomView,
                EditMode.Photo => s.PhotoView,
                _ => s.NormalView,
            };
        }

        public static ViewState Current => Resolve(EditModeManager.Mode, KitchenSettings.Instance);

        public static ViewState Resolve(EditMode mode) => Resolve(mode, KitchenSettings.Instance);

        public static ViewState Resolve(EditMode mode, KitchenSettings? s)
        {
            var p = PresetFor(mode, s);
            if (mode == EditMode.Room)
                return new ViewState(
                    wallsEnabled: true,
                    wallOutline: p.wallOutline,
                    lowerNearWalls: false,
                    lowerAllWalls: false,
                    hideOpeningsOnLoweredWalls: false,
                    objectsVisible: p.objectsVisible,
                    edgeOutline: p.edgeOutline,
                    hideLightSources: p.hideLightSources,
                    locked: RoomLocked);

            return new ViewState(
                wallsEnabled: p.wallsEnabled,
                wallOutline: p.wallOutline,
                lowerNearWalls: p.lowerNearWalls,
                lowerAllWalls: p.lowerAllWalls,
                hideOpeningsOnLoweredWalls: p.hideOpeningsOnLoweredWalls,
                objectsVisible: p.objectsVisible,
                edgeOutline: p.edgeOutline,
                hideLightSources: p.hideLightSources,
                locked: 0);
        }

        public static bool IsEditable(EditMode presetMode, ViewField field) =>
            !Resolve(presetMode).IsLocked(field);

        public static ViewField? ParentOf(ViewField field) => field switch
        {
            ViewField.WallOutline => ViewField.Walls,
            ViewField.LowerNearWalls => ViewField.Walls,
            ViewField.LowerAllWalls => ViewField.LowerNearWalls,
            ViewField.HideOpeningsOnLoweredWalls => ViewField.LowerNearWalls,
            ViewField.ObjectOutline => ViewField.Objects,
            _ => null,
        };
    }
}
