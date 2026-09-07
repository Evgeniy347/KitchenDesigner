using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct PanelBody
    {
        public const int DEFAULT_GAP_MM = 1;

        public const string DISPLAY_TYPE_NAME = "ДВП/ХДФ";

        public const bool SUPPORTS_GAPS = true;

        public const CutoutNeighbourRole CUTOUT_ROLE = CutoutNeighbourRole.AlignsCutout;

        public readonly Vector3Int DimensionsMM;
        public readonly BoxGaps Gaps;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public PanelBody(Vector3Int dimensionsMM, BoxGaps gaps, Vector3 position, Quaternion rotation)
        {
            DimensionsMM = dimensionsMM;
            Gaps = gaps;
            Position = position;
            Rotation = rotation;
        }

        public static PanelBody OfSize(Vector3Int dimensionsMM) =>
            new PanelBody(dimensionsMM, UniformGaps(DEFAULT_GAP_MM),
                Vector3.zero, Quaternion.identity);

        public static BoxGaps UniformGaps(int gapMM)
        {
            int g = Mathf.Max(0, gapMM);
            return new BoxGaps(g, g, g, g, g, g);
        }

        public PanelBody WithGaps(BoxGaps gaps) =>
            new PanelBody(DimensionsMM, gaps, Position, Rotation);

        public PanelBody At(Vector3 position, Quaternion rotation) =>
            new PanelBody(DimensionsMM, Gaps, position, rotation);

        public Vector3 PhysicalScale => new Vector3(
            DimensionsMM.x * AppConstants.MM_TO_UNITS,
            DimensionsMM.y * AppConstants.MM_TO_UNITS,
            DimensionsMM.z * AppConstants.MM_TO_UNITS);

        public int GapMM =>
            Gaps.Left + Gaps.Right + Gaps.Top + Gaps.Bottom + Gaps.Front + Gaps.Back;

        public Vector3[] Vertices() =>
            GappedBox.Vertices(PhysicalScale, Gaps, Position, Rotation);

        public Face[] Faces() =>
            GappedBox.Faces(PhysicalScale, Gaps, Position, Rotation);
    }
}
