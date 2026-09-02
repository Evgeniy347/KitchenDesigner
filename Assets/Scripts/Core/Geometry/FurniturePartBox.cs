using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct FurniturePartBox
    {
        public readonly string Name;
        public readonly Vector3 CentreMM;
        public readonly float ProfileWidthMM;
        public readonly float ProfileDepthMM;
        public readonly float ThicknessMM;
        public readonly float RadiusMM;
        public readonly FurniturePartOrientation Orientation;
        public readonly FurniturePartShape Shape;

        public FurniturePartBox(string name, Vector3 centreMM, float profileWidthMM,
            float profileDepthMM, float thicknessMM, float radiusMM,
            FurniturePartOrientation orientation, FurniturePartShape shape = FurniturePartShape.Extruded)
        {
            Name = name;
            CentreMM = centreMM;
            ProfileWidthMM = profileWidthMM;
            ProfileDepthMM = profileDepthMM;
            ThicknessMM = thicknessMM;
            RadiusMM = radiusMM;
            Orientation = orientation;
            Shape = shape;
        }

        public Vector3 LocalSizeMM => new Vector3(ProfileWidthMM, ThicknessMM, ProfileDepthMM);

        public Vector3 SizeMM => Orientation switch
        {
            FurniturePartOrientation.Frontal =>
                new Vector3(ProfileWidthMM, ProfileDepthMM, ThicknessMM),
            FurniturePartOrientation.Side =>
                new Vector3(ThicknessMM, ProfileDepthMM, ProfileWidthMM),
            _ => LocalSizeMM,
        };
    }
}
