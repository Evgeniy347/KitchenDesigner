using UnityEngine;

namespace KitchenDesigner.Core
{
    public readonly struct ScrewLegOffCentre
    {
        public readonly string Axis;
        public readonly float SpanMM;
        public readonly float OffsetMM;
        public readonly float WallMM;

        public ScrewLegOffCentre(string axis, float spanMM, float offsetMM, float wallMM)
        {
            Axis = axis;
            SpanMM = spanMM;
            OffsetMM = offsetMM;
            WallMM = wallMM;
        }
    }

    public static class ScrewLegCentring
    {
        public const string AxisX = "X";
        public const string AxisZ = "Z";

        public static bool TryFindOffCentre(ScrewLegElement leg, KitchenElement host,
            out ScrewLegOffCentre worst)
        {
            worst = default;
            if (leg == null || host == null) return false;

            var legBox = ElementAabb.Of(leg);
            var hostBox = ElementAabb.Of(host);

            float threadMM = leg.ThreadDiameterMM;

            bool found = false;
            if (Check(AxisX, legBox.minX, legBox.maxX, hostBox.minX, hostBox.maxX, threadMM, out var onX))
            {
                worst = onX;
                found = true;
            }
            if (Check(AxisZ, legBox.minZ, legBox.maxZ, hostBox.minZ, hostBox.maxZ, threadMM, out var onZ)
                && (!found || onZ.SpanMM < worst.SpanMM))
            {
                worst = onZ;
                found = true;
            }
            return found;
        }

        private static bool Check(string axis, float legMin, float legMax,
            float hostMin, float hostMax, float threadMM, out ScrewLegOffCentre offCentre)
        {
            offCentre = default;

            float spanMM = (hostMax - hostMin) / AppConstants.MM_TO_UNITS;
            if (!ScrewLegSpec.NeedsCentring(spanMM)) return false;

            float offsetMM = ((legMin + legMax) - (hostMin + hostMax))
                * 0.5f / AppConstants.MM_TO_UNITS;
            if (ScrewLegSpec.InsertHolds(offsetMM, spanMM, threadMM)) return false;

            offCentre = new ScrewLegOffCentre(axis, spanMM, Mathf.Abs(offsetMM),
                ScrewLegSpec.InsertWallMM(offsetMM, spanMM, threadMM));
            return true;
        }
    }
}
