using System;

namespace KitchenDesigner.Core
{
    public enum ViewField
    {
        Walls,
        WallOutline,
        LowerNearWalls,
        HideOpeningsOnLoweredWalls,
        Objects,
        ObjectOutline,
        HideLightSources,
    }

    [Serializable]
    public class ViewPreset
    {
        public bool wallsEnabled = true;
        public bool wallOutline = true;
        public bool lowerNearWalls = true;
        public bool hideOpeningsOnLoweredWalls = false;
        public bool objectsVisible = true;
        public bool edgeOutline = true;
        public bool hideLightSources = false;

        public bool Get(ViewField field) => field switch
        {
            ViewField.Walls => wallsEnabled,
            ViewField.WallOutline => wallOutline,
            ViewField.LowerNearWalls => lowerNearWalls,
            ViewField.HideOpeningsOnLoweredWalls => hideOpeningsOnLoweredWalls,
            ViewField.Objects => objectsVisible,
            ViewField.ObjectOutline => edgeOutline,
            _ => hideLightSources,
        };

        public void Set(ViewField field, bool value)
        {
            switch (field)
            {
                case ViewField.Walls: wallsEnabled = value; break;
                case ViewField.WallOutline: wallOutline = value; break;
                case ViewField.LowerNearWalls: lowerNearWalls = value; break;
                case ViewField.HideOpeningsOnLoweredWalls: hideOpeningsOnLoweredWalls = value; break;
                case ViewField.Objects: objectsVisible = value; break;
                case ViewField.ObjectOutline: edgeOutline = value; break;
                default: hideLightSources = value; break;
            }
        }

        public static readonly ViewField[] AllFields = (ViewField[])Enum.GetValues(typeof(ViewField));

        public void CopyFrom(ViewPreset? other)
        {
            if (other == null) return;
            foreach (var field in AllFields) Set(field, other.Get(field));
        }

        public ViewPreset Clone()
        {
            var copy = new ViewPreset();
            copy.CopyFrom(this);
            return copy;
        }

        public void ResetToDefaults() => CopyFrom(new ViewPreset());
    }

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
            | (1 << (int)ViewField.HideLightSources);

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
                        wallOutline: p.wallOutline,
                        lowerNearWalls: false,
                        hideOpeningsOnLoweredWalls: false,
                        objectsVisible: true,
                        edgeOutline: p.edgeOutline,
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
