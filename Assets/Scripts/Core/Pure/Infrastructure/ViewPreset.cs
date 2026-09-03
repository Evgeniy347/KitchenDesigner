using System;

namespace KitchenDesigner.Core
{
    [Serializable]
    public class ViewPreset
    {
        public bool wallsEnabled = true;
        public bool wallOutline = true;
        public bool lowerNearWalls = true;
        public bool lowerAllWalls = false;
        public bool hideOpeningsOnLoweredWalls = false;
        public bool objectsVisible = true;
        public bool edgeOutline = true;
        public bool hideLightSources = false;

        public static ViewPreset PhotoDefaults() => new ViewPreset
        {
            wallsEnabled = true,
            wallOutline = false,
            lowerNearWalls = false,
            lowerAllWalls = false,
            hideOpeningsOnLoweredWalls = false,
            objectsVisible = true,
            edgeOutline = false,
            hideLightSources = false,
        };

        public bool Get(ViewField field) => field switch
        {
            ViewField.Walls => wallsEnabled,
            ViewField.WallOutline => wallOutline,
            ViewField.LowerNearWalls => lowerNearWalls,
            ViewField.LowerAllWalls => lowerAllWalls,
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
                case ViewField.LowerAllWalls: lowerAllWalls = value; break;
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

        public void ResetToPhotoDefaults() => CopyFrom(PhotoDefaults());
    }
}
