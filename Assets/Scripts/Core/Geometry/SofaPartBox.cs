using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct SofaPartBox
    {
        public readonly string Name;
        public readonly Vector3 CentreMM;
        public readonly float ProfileWidthMM;
        public readonly float ProfileDepthMM;
        public readonly float ThicknessMM;
        public readonly float RadiusMM;
        public readonly SofaPartOrientation Orientation;
        public readonly SofaPartShape Shape;

        public SofaPartBox(string name, Vector3 centreMM, float profileWidthMM,
            float profileDepthMM, float thicknessMM, float radiusMM,
            SofaPartOrientation orientation, SofaPartShape shape = SofaPartShape.Extruded)
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
            SofaPartOrientation.Frontal =>
                new Vector3(ProfileWidthMM, ProfileDepthMM, ThicknessMM),
            SofaPartOrientation.Side =>
                new Vector3(ThicknessMM, ProfileDepthMM, ProfileWidthMM),
            _ => LocalSizeMM,
        };
    }
}
