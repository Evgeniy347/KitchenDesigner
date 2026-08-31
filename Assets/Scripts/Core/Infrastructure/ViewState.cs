namespace KitchenDesigner.Core
{
    public readonly struct ViewState
    {
        public readonly bool WallsEnabled;
        public readonly bool WallOutline;
        public readonly bool LowerNearWalls;
        public readonly bool HideOpeningsOnLoweredWalls;
        public readonly bool ObjectsVisible;
        public readonly bool EdgeOutline;
        public readonly bool HideLightSources;

        private readonly int _locked;

        public ViewState(bool wallsEnabled, bool wallOutline, bool lowerNearWalls,
            bool hideOpeningsOnLoweredWalls, bool objectsVisible, bool edgeOutline,
            bool hideLightSources, int locked)
        {
            WallsEnabled = wallsEnabled;
            WallOutline = wallOutline;
            LowerNearWalls = lowerNearWalls;
            HideOpeningsOnLoweredWalls = hideOpeningsOnLoweredWalls;
            ObjectsVisible = objectsVisible;
            EdgeOutline = edgeOutline;
            HideLightSources = hideLightSources;
            _locked = locked;
        }

        public bool Get(ViewField field) => field switch
        {
            ViewField.Walls => WallsEnabled,
            ViewField.WallOutline => WallOutline,
            ViewField.LowerNearWalls => LowerNearWalls,
            ViewField.HideOpeningsOnLoweredWalls => HideOpeningsOnLoweredWalls,
            ViewField.Objects => ObjectsVisible,
            ViewField.ObjectOutline => EdgeOutline,
            _ => HideLightSources,
        };

        public bool IsLocked(ViewField field) => (_locked & (1 << (int)field)) != 0;

        public int VisibilityHash =>
            (WallsEnabled ? 1 : 0) | (LowerNearWalls ? 2 : 0)
            | (HideOpeningsOnLoweredWalls ? 4 : 0) | (ObjectsVisible ? 8 : 0)
            | (EdgeOutline ? 16 : 0) | (WallOutline ? 32 : 0) | (HideLightSources ? 64 : 0);
    }
}
