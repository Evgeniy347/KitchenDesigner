using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class ScrewLegSeat
    {
        public static ScrewLegMargins Of(ScrewLegElement? leg)
        {
            if (leg == null) return default;
            var host = AttachLinks.Parent(leg);
            return host == null
                ? default
                : ScrewLegHosting.Margins(leg.transform.position, leg.transform.up,
                    host.ToGeometry());
        }

        public static int LeftMM(in ScrewLegMargins m) =>
            m.HasHost ? ScrewLegSpec.RoundMM(m.LeftMM) : 0;

        public static int RightMM(in ScrewLegMargins m) =>
            m.HasHost ? ScrewLegSpec.RoundMM(m.SpanAcrossMM) - LeftMM(m) : 0;

        public static int TopMM(in ScrewLegMargins m) =>
            m.HasHost ? ScrewLegSpec.RoundMM(m.TopMM) : 0;

        public static int BottomMM(in ScrewLegMargins m) =>
            m.HasHost ? ScrewLegSpec.RoundMM(m.SpanAlongMM) - TopMM(m) : 0;

        public static void SetLeftMM(ScrewLegElement leg, int mm)
        {
            var m = Of(leg);
            if (m.HasHost) Shift(leg, m.RightAxis, mm - m.LeftMM);
        }

        public static void SetRightMM(ScrewLegElement leg, int mm)
        {
            var m = Of(leg);
            if (m.HasHost) Shift(leg, m.RightAxis, m.RightMM - mm);
        }

        public static void SetTopMM(ScrewLegElement leg, int mm)
        {
            var m = Of(leg);
            if (m.HasHost) Shift(leg, m.UpAxis, m.TopMM - mm);
        }

        public static void SetBottomMM(ScrewLegElement leg, int mm)
        {
            var m = Of(leg);
            if (m.HasHost) Shift(leg, m.UpAxis, mm - m.BottomMM);
        }

        private static void Shift(ScrewLegElement leg, Vector3 axis, float deltaMM)
        {
            if (Mathf.Abs(deltaMM) < ScrewLegSpec.MM_ROUNDING_EPSILON) return;
            leg.transform.position += axis * (deltaMM * AppConstants.MM_TO_UNITS);
        }
    }
}
