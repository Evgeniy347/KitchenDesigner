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

        public float VolumeM3 =>
            (MaxXMm - MinXMm) * 1e-3f * (MaxZMm - MinZMm) * 1e-3f * (HeightMm * 1e-3f);
    }
}
