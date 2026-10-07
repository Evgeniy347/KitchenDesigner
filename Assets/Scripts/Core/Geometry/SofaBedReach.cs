using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class SofaBedReach
    {
        public readonly OrientedBox Box;
        public readonly Vector3 Min;
        public readonly Vector3 Max;
        public readonly float TravelMM;

        private SofaBedReach(OrientedBox box, float travelMM)
        {
            Box = box;
            TravelMM = travelMM;
            var radius = new Vector3(box.RadiusAlong(Vector3.right),
                box.RadiusAlong(Vector3.up), box.RadiusAlong(Vector3.forward));
            Min = box.Center - radius;
            Max = box.Center + radius;
        }

        public static float TravelOfMM(Vector3Int dimensionsMM, int seatHeightMM)
            => SofaUnfold.SeatSlideTravelMM(dimensionsMM.z, seatHeightMM);

        public static Cuboid LocalBoxMM(Vector3Int dimensionsMM, int seatHeightMM)
        {
            float frontZ = dimensionsMM.z * 0.5f;
            float floorY = SofaLayout.FloorYMM(dimensionsMM);
            return Cuboid.Between(
                new Vector3(-dimensionsMM.x * 0.5f, floorY, frontZ),
                new Vector3(dimensionsMM.x * 0.5f, floorY + seatHeightMM,
                    frontZ + TravelOfMM(dimensionsMM, seatHeightMM)));
        }

        public static SofaBedReach Of(Vector3Int dimensionsMM, int seatHeightMM,
            Vector3 centreUnits, Quaternion rotation)
        {
            var local = LocalBoxMM(dimensionsMM, seatHeightMM);
            var centre = centreUnits + rotation * (local.CentreMM * AppConstants.MM_TO_UNITS);
            var half = local.SizeMM * (0.5f * AppConstants.MM_TO_UNITS);
            return new SofaBedReach(new OrientedBox(centre, rotation, half),
                TravelOfMM(dimensionsMM, seatHeightMM));
        }

        public bool ReachesBounds(in Vector3 min, in Vector3 max) =>
            Tolerance.IntervalsOverlap(Min.x, Max.x, min.x, max.x, Tolerance.ContactUnits)
            && Tolerance.IntervalsOverlap(Min.y, Max.y, min.y, max.y, Tolerance.ContactUnits)
            && Tolerance.IntervalsOverlap(Min.z, Max.z, min.z, max.z, Tolerance.ContactUnits);

        public bool IsBlockedBy(in ElementGeometry other)
        {
            if (!ReachesBounds(other.Min, other.Max)) return false;
            var obstacle = new OrientedBox((other.Min + other.Max) * 0.5f,
                Quaternion.identity, (other.Max - other.Min) * 0.5f);
            return BoxOverlap.Overlap(Box, obstacle, Tolerance.ContactUnits);
        }
    }
}
