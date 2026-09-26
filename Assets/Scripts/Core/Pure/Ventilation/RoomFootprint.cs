namespace KitchenDesigner.Core.Ventilation
{
    public readonly struct RoomFootprint
    {
        public readonly string ElementId;
        public readonly float MinXMm;
        public readonly float MaxXMm;
        public readonly float MinZMm;
        public readonly float MaxZMm;
        public readonly float HeightMm;

        public RoomFootprint(string elementId, float minXMm, float maxXMm, float minZMm,
            float maxZMm, float heightMm)
        {
            ElementId = elementId;
            MinXMm = minXMm;
            MaxXMm = maxXMm;
            MinZMm = minZMm;
            MaxZMm = maxZMm;
            HeightMm = heightMm;
        }

        public bool Contains(float xMm, float zMm) =>
            xMm >= MinXMm && xMm <= MaxXMm && zMm >= MinZMm && zMm <= MaxZMm;

        public double VolumeM3 =>
            (double)(MaxXMm - MinXMm) * (MaxZMm - MinZMm) * HeightMm * 1e-9;
    }
}
