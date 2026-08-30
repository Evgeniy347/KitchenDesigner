using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum DoorMode
    {
        HingeFrontLeft, HingeFrontRight, HingeFrontTop, HingeFrontBottom,
        HingeBackLeft, HingeBackRight, HingeBackTop, HingeBackBottom,
        HingeEdgeTopLeft, HingeEdgeTopRight, HingeEdgeBottomLeft, HingeEdgeBottomRight,
        DrawerOut, DrawerIn, DrawerRight, DrawerLeft, DrawerUp, DrawerDown
    }

    public enum HingeKinematics
    {
        CupHinge,
        EdgePivot
    }

    public static class FacadeDoor
    {
        public const float CupMaxAngleDeg = 110f;

        public const float EdgeMaxAngleDeg = 90f;

        public const float DrawerSlideMeters = 0.4f;

        public static float MaxAngle(HingeKinematics kind) =>
            kind == HingeKinematics.CupHinge ? CupMaxAngleDeg : EdgeMaxAngleDeg;

        private const float CupDepthMM = 12.5f;
        private const float CupBackWallMM = 3.5f;
        private const float CupSideMinMM = 3f;
        private const float CupSideMaxMM = 7f;

        public static Vector3 HingePivotOffset(Vector3 halfExtents)
        {
            float thicknessMM = 2f * Mathf.Abs(halfExtents.z) / AppConstants.MM_TO_UNITS;

            float sideMM = Mathf.Clamp(thicknessMM * 0.25f, CupSideMinMM, CupSideMaxMM);
            float depthMM = Mathf.Min(CupDepthMM, Mathf.Max(0f, thicknessMM - CupBackWallMM));

            float side = sideMM * AppConstants.MM_TO_UNITS;
            float depth = depthMM * AppConstants.MM_TO_UNITS;

            return new Vector3(
                Mathf.Min(side, 2f * Mathf.Abs(halfExtents.x)),
                Mathf.Min(side, 2f * Mathf.Abs(halfExtents.y)),
                Mathf.Min(depth, 2f * Mathf.Abs(halfExtents.z)));
        }

        private readonly struct Variant
        {
            public readonly bool isDrawer;
            public readonly bool allowsCupHinge;
            public readonly Vector3 pivotSigns;
            public readonly Vector3 axisOrSlide;
            public readonly string symbol;
            public readonly string displayName;
            public Variant(bool isDrawer, bool allowsCupHinge, Vector3 pivotSigns, Vector3 axisOrSlide, string symbol, string displayName)
            {
                this.isDrawer = isDrawer; this.allowsCupHinge = allowsCupHinge;
                this.pivotSigns = pivotSigns; this.axisOrSlide = axisOrSlide;
                this.symbol = symbol; this.displayName = displayName;
            }
        }

        private static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

        private static readonly Variant[] Modes =
        {
            new Variant(false, true, new Vector3(-1f, 0f, -1f),  Y, "<", "Дверь: слева"),
            new Variant(false, true, new Vector3( 1f, 0f, -1f), -Y, ">", "Дверь: справа"),
            new Variant(false, true, new Vector3( 0f, 1f, -1f),  X, "^", "Дверь: сверху"),
            new Variant(false, true, new Vector3( 0f,-1f, -1f), -X, "v", "Дверь: снизу"),
            new Variant(false, true, new Vector3(-1f, 0f,  1f), -Y, "[", "Сзади: слева"),
            new Variant(false, true, new Vector3( 1f, 0f,  1f),  Y, "]", "Сзади: справа"),
            new Variant(false, true, new Vector3( 0f, 1f,  1f), -X, "{", "Сзади: сверху"),
            new Variant(false, true, new Vector3( 0f,-1f,  1f),  X, "}", "Сзади: снизу"),
            new Variant(false, false, new Vector3(-1f, 1f,  0f),  Z, "(", "Угол: верх-лево"),
            new Variant(false, false, new Vector3( 1f, 1f,  0f),  Z, ")", "Угол: верх-право"),
            new Variant(false, false, new Vector3(-1f,-1f,  0f),  Z, "\\", "Угол: низ-лево"),
            new Variant(false, false, new Vector3( 1f,-1f,  0f),  Z, "/", "Угол: низ-право"),
            new Variant(true, false, Vector3.zero,  Z, "O", "Ящик: вперёд"),
            new Variant(true, false, Vector3.zero, -Z, "X", "Ящик: назад"),
            new Variant(true, false, Vector3.zero, -X, "R", "Ящик: вправо"),
            new Variant(true, false, Vector3.zero,  X, "L", "Ящик: влево"),
            new Variant(true, false, Vector3.zero, -Y, "U", "Ящик: вверх"),
            new Variant(true, false, Vector3.zero,  Y, "D", "Ящик: вниз"),
        };

        public static int Count => Modes.Length;

        public static DoorMode Next(DoorMode mode) => (DoorMode)(((int)mode + 1) % Modes.Length);

        public static string Symbol(DoorMode mode) => Modes[(int)mode].symbol;

        private static readonly string[] WireNames =
        {
            "front_left", "front_right", "front_top", "front_bottom",
            "back_left", "back_right", "back_top", "back_bottom",
            "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right",
            "drawer_out", "drawer_in", "drawer_right", "drawer_left", "drawer_up", "drawer_down"
        };

        public static string WireName(DoorMode mode) => WireNames[(int)mode];

        public static string Label(DoorMode mode) => Modes[(int)mode].symbol + " " + Modes[(int)mode].displayName;

        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 0.5f * (1f - Mathf.Cos(Mathf.PI * t));
        }

        public static void Pose(
            Vector3 closedPos, Quaternion closedRot, Vector3 halfExtents,
            DoorMode mode, float progress,
            out Vector3 pos, out Quaternion rot,
            HingeKinematics kind = HingeKinematics.CupHinge)
        {
            var v = Modes[(int)mode];
            float e = Ease(progress);

            if (v.isDrawer)
            {
                rot = closedRot;
                pos = closedPos + closedRot * (v.axisOrSlide * (DrawerSlideMeters * e));
                return;
            }

            var pivotLocal = PivotLocal(v, halfExtents, kind);
            float angle = -HingeAngle(v, kind) * e;
            var pivotWorld = closedPos + closedRot * pivotLocal;
            var axisWorld = closedRot * v.axisOrSlide;
            var delta = Quaternion.AngleAxis(angle, axisWorld);

            rot = delta * closedRot;
            pos = pivotWorld + delta * (closedPos - pivotWorld);
        }

        public static bool Hinge(DoorMode mode, Vector3 halfExtents,
            out Vector3 pivotLocal, out Vector3 axisLocal,
            HingeKinematics kind = HingeKinematics.CupHinge)
        {
            var v = Modes[(int)mode];
            axisLocal = v.axisOrSlide;
            if (v.isDrawer) { pivotLocal = Vector3.zero; return false; }
            pivotLocal = PivotLocal(v, halfExtents, kind);
            return true;
        }

        private static Vector3 PivotLocal(Variant v, Vector3 halfExtents, HingeKinematics kind)
        {
            var pivot = Vector3.Scale(v.pivotSigns, halfExtents);
            if (!UsesCup(v, kind)) return pivot;
            return pivot - Vector3.Scale(v.pivotSigns, HingePivotOffset(halfExtents));
        }

        private static float HingeAngle(Variant v, HingeKinematics kind) =>
            UsesCup(v, kind) ? CupMaxAngleDeg : EdgeMaxAngleDeg;

        private static bool UsesCup(Variant v, HingeKinematics kind) =>
            v.allowsCupHinge && kind == HingeKinematics.CupHinge;
    }
}
