using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SofaUnfold
    {
        public const float SecondsPerStage = 1f;
        public const float BackrestFlatAngleDeg = 90f;
        public const float SlideClearanceMM = 20f;
        public const float ExtraPullMM = 150f;

        public static SofaStage Next(SofaStage stage)
            => stage == SofaStage.Bed ? SofaStage.Folded : stage + 1;

        public static SofaStage StageFrom(int value)
            => (SofaStage)Mathf.Clamp(value, (int)SofaStage.Folded, (int)SofaStage.Bed);

        public static float ProgressOf(SofaStage stage) => (float)stage;

        public static float Ease(float fraction)
            => 0.5f * (1f - Mathf.Cos(Mathf.PI * Mathf.Clamp01(fraction)));

        public static float PanelReachMM(int seatHeightMM)
            => SofaLayout.OverallHeightMM - seatHeightMM + SofaLayout.BackrestThicknessMM;

        public static float BelowHingeMM(int seatHeightMM)
            => seatHeightMM - SofaLayout.BackrestThicknessMM - SofaLayout.BackrestBottomMM;

        public static float SeatSlideTravelMM(int sofaDepthMM, int seatHeightMM)
            => Mathf.Max(SofaBoxLayout.RearSetbackMM + SofaBoxLayout.DepthMM(sofaDepthMM),
                   PanelReachMM(seatHeightMM))
               + SlideClearanceMM;

        public static float BackrestShiftMM(int seatHeightMM, float angleDeg)
        {
            float radians = angleDeg * Mathf.Deg2Rad;
            float behind = BelowHingeMM(seatHeightMM) * Mathf.Sin(radians)
                + SofaLayout.BackrestThicknessMM * Mathf.Cos(radians)
                - SofaLayout.BackrestThicknessMM;
            return Mathf.Max(0f, behind);
        }

        public static SofaPose PoseAt(float progress, int sofaDepthMM, int seatHeightMM)
        {
            float angle = BackrestFlatAngleDeg * Ease(progress - 1f);
            float slide = (SeatSlideTravelMM(sofaDepthMM, seatHeightMM) + ExtraPullMM)
                * Ease(progress) - ExtraPullMM * Ease(progress - 1f);
            return new SofaPose(slide, angle, BackrestShiftMM(seatHeightMM, angle),
                progress <= 0f);
        }

        public static Vector3 HingeMM(Vector3Int dimensionsMM, int seatHeightMM)
            => new Vector3(0f, SofaBoxLayout.TopYMM(dimensionsMM, seatHeightMM),
                SofaLayout.BackrestFrontZMM(dimensionsMM));

        public static Vector3 BackrestCentreMM(Vector3Int dimensionsMM, int seatHeightMM,
            float angleDeg)
        {
            var hinge = HingeMM(dimensionsMM, seatHeightMM);
            var offset = SofaLayout.Backrest(dimensionsMM).CentreMM - hinge;
            float radians = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return hinge + new Vector3(offset.x,
                offset.y * cos - offset.z * sin,
                offset.y * sin + offset.z * cos + BackrestShiftMM(seatHeightMM, angleDeg));
        }
    }
}
