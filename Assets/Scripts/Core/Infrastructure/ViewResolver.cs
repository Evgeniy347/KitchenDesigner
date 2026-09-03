namespace KitchenDesigner.Core
{
    public static class ViewResolver
    {
        private static readonly ViewPreset Fallback = new ViewPreset();

        private const int RoomLocked =
            (1 << (int)ViewField.Walls)
            | (1 << (int)ViewField.LowerNearWalls)
            | (1 << (int)ViewField.HideOpeningsOnLoweredWalls);

        private const int PhotoLocked =
            RoomLocked
            | (1 << (int)ViewField.Objects)
            | (1 << (int)ViewField.HideLightSources)
            | (1 << (int)ViewField.WallOutline)
            | (1 << (int)ViewField.ObjectOutline);

        public static ViewPreset PresetFor(EditMode mode, KitchenSettings? s)
        {
            if (s == null) return Fallback;
            return mode == EditMode.Room ? s.RoomView : s.NormalView;
        }

        public static ViewState Current => Resolve(EditModeManager.Mode, KitchenSettings.Instance);

        public static ViewState Resolve(EditMode mode) => Resolve(mode, KitchenSettings.Instance);

        public static ViewState Resolve(EditMode mode, KitchenSettings? s)
        {
            var p = PresetFor(mode, s);
            switch (mode)
            {
                case EditMode.Room:
                    return new ViewState(
                        wallsEnabled: true,
                        wallOutline: p.wallOutline,
                        lowerNearWalls: false,
                        hideOpeningsOnLoweredWalls: false,
                        objectsVisible: p.objectsVisible,
                        edgeOutline: p.edgeOutline,
                        hideLightSources: p.hideLightSources,
                        locked: RoomLocked);

                case EditMode.Photo:
                    return new ViewState(
                        wallsEnabled: true,
                        wallOutline: false,
                        lowerNearWalls: false,
                        hideOpeningsOnLoweredWalls: false,
                        objectsVisible: true,
                        edgeOutline: false,
                        hideLightSources: false,
                        locked: PhotoLocked);

                default:
                    return new ViewState(
                        wallsEnabled: p.wallsEnabled,
                        wallOutline: p.wallOutline,
                        lowerNearWalls: p.lowerNearWalls,
                        hideOpeningsOnLoweredWalls: p.hideOpeningsOnLoweredWalls,
                        objectsVisible: p.objectsVisible,
                        edgeOutline: p.edgeOutline,
                        hideLightSources: p.hideLightSources,
                        locked: 0);
            }
        }

        public static bool IsEditable(EditMode currentMode, EditMode presetMode, ViewField field)
        {
            if (currentMode == EditMode.Photo) return false;
            return !Resolve(presetMode).IsLocked(field);
        }

        public static ViewField? ParentOf(ViewField field) => field switch
        {
            ViewField.WallOutline => ViewField.Walls,
            ViewField.LowerNearWalls => ViewField.Walls,
            ViewField.HideOpeningsOnLoweredWalls => ViewField.LowerNearWalls,
            ViewField.ObjectOutline => ViewField.Objects,
            _ => null,
        };
    }
}
