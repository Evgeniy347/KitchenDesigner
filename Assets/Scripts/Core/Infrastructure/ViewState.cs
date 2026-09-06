namespace KitchenDesigner.Core
{
    public readonly struct ViewState
    {
        public readonly bool WallsEnabled;
        public readonly bool WallOutline;
        public readonly bool LowerNearWalls;
        public readonly bool LowerAllWalls;
        public readonly bool HideOpeningsOnLoweredWalls;
        public readonly bool ObjectsVisible;
        public readonly bool EdgeOutline;
        public readonly bool HideLightSources;

        private readonly int _locked;

        public ViewState(bool wallsEnabled, bool wallOutline, bool lowerNearWalls,
            bool lowerAllWalls, bool hideOpeningsOnLoweredWalls, bool objectsVisible,
            bool edgeOutline, bool hideLightSources, int locked)
        {
            WallsEnabled = wallsEnabled;
            WallOutline = wallOutline;
            LowerNearWalls = lowerNearWalls;
            LowerAllWalls = lowerAllWalls;
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
            ViewField.LowerAllWalls => LowerAllWalls,
            ViewField.HideOpeningsOnLoweredWalls => HideOpeningsOnLoweredWalls,
            ViewField.Objects => ObjectsVisible,
            ViewField.ObjectOutline => EdgeOutline,
            _ => HideLightSources,
        };

        public bool IsLocked(ViewField field) => (_locked & (1 << (int)field)) != 0;
    }
}
