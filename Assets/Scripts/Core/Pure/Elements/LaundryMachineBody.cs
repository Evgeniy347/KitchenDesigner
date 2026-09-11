using UnityEngine;

namespace KitchenDesigner.Core
{
    public enum LaundryMachineKind
    {
        Washer = 0,
        Dryer = 1
    }

    public static class LaundryMachineBody
    {
        public const int DEFAULT_WIDTH_MM = 600;
        public const int DEFAULT_HEIGHT_MM = 850;
        public const int DEFAULT_DEPTH_MM = 600;

        public const int WASHER_HATCH_DIAMETER_MM = 320;
        public const int DRYER_HATCH_DIAMETER_MM = 360;

        public const int HATCH_RIM_WIDTH_MM = 25;
        public const int HATCH_MARGIN_MM = 60;
        public const int HATCH_THICKNESS_MM = 18;
        public const int GLASS_THICKNESS_MM = 10;
        public const int GLASS_PROUD_MM = 6;

        public const int FRONT_FACE_SETBACK_MM = HATCH_THICKNESS_MM + GLASS_PROUD_MM;
        public const int CONTROL_PANEL_HEIGHT_MM = 90;
        public const int CONTROL_PANEL_THICKNESS_MM = 20;

        public const int DRUM_DEPTH_MM = 300;
        public const int DRUM_BACK_WALL_MM = 20;
        public const int DRUM_BACK_INSET_MM = 4;
        public const int DRUM_BACK_THICKNESS_MM = 4;
        public const int DRUM_BACK_GAP_MM = 1;

        public const int MIN_HATCH_DIAMETER_MM = 2 * HATCH_RIM_WIDTH_MM + 2;

        public const int MIN_WIDTH_MM = MIN_HATCH_DIAMETER_MM + 2 * HATCH_MARGIN_MM;
        public const int MIN_HEIGHT_MM = CONTROL_PANEL_HEIGHT_MM + MIN_WIDTH_MM;
        public const int MIN_DEPTH_MM = 60;

        public const int IdxShell = 0;
        public const int IdxControlPanel = 1;
        public const int IdxDrumBack = 2;
        public const int IdxHatchRim = 3;
        public const int IdxHatchGlass = 4;
        public const int BodyPartCount = 3;
        public const int DoorPartCount = 2;
        public const int PartCount = BodyPartCount + DoorPartCount;

        public const string WasherName = "Стиральная машина";
        public const string DryerName = "Сушильная машина";

        public const string WASHER_TYPE_ID = "washing_machine";
        public const string DRYER_TYPE_ID = "dryer";

        public const float HatchCenterYMM = -CONTROL_PANEL_HEIGHT_MM * 0.5f;

        public static string TypeId(LaundryMachineKind kind) =>
            kind == LaundryMachineKind.Dryer ? DRYER_TYPE_ID : WASHER_TYPE_ID;

        public static bool TryKindOf(string typeId, out LaundryMachineKind kind)
        {
            kind = LaundryMachineKind.Washer;
            if (typeId == WASHER_TYPE_ID) return true;
            if (typeId != DRYER_TYPE_ID) return false;
            kind = LaundryMachineKind.Dryer;
            return true;
        }

        public static string NameOf(LaundryMachineKind kind) =>
            kind == LaundryMachineKind.Dryer ? DryerName : WasherName;

        public static int NominalHatchDiameterMM(LaundryMachineKind kind) =>
            kind == LaundryMachineKind.Dryer ? DRYER_HATCH_DIAMETER_MM : WASHER_HATCH_DIAMETER_MM;

        public static Vector3Int DefaultDimensionsMM =>
            new Vector3Int(DEFAULT_WIDTH_MM, DEFAULT_HEIGHT_MM, DEFAULT_DEPTH_MM);

        public static Vector3Int ClampMM(Vector3Int dimensionsMM) => new Vector3Int(
            Mathf.Max(dimensionsMM.x, MIN_WIDTH_MM),
            Mathf.Max(dimensionsMM.y, MIN_HEIGHT_MM),
            Mathf.Max(dimensionsMM.z, MIN_DEPTH_MM));

        public static bool IsRound(int partIndex) =>
            partIndex == IdxDrumBack || partIndex == IdxHatchRim || partIndex == IdxHatchGlass;

        public static bool IsBored(int partIndex) => partIndex == IdxShell;

        public static string PartName(int idx) => idx switch
        {
            IdxShell => "Shell",
            IdxControlPanel => "ControlPanel",
            IdxDrumBack => "DrumBack",
            IdxHatchRim => "HatchRim",
            _ => "HatchGlass",
        };

        public static float HatchDiameterMM(LaundryMachineKind kind, Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float roomAcross = Mathf.Min(d.x, d.y - CONTROL_PANEL_HEIGHT_MM) - 2 * HATCH_MARGIN_MM;
            return Mathf.Min(NominalHatchDiameterMM(kind), roomAcross);
        }

        public static float GlassDiameterMM(LaundryMachineKind kind, Vector3Int dimensionsMM) =>
            HatchDiameterMM(kind, dimensionsMM) - 2 * HATCH_RIM_WIDTH_MM;

        public static float DrumDiameterMM(LaundryMachineKind kind, Vector3Int dimensionsMM) =>
            GlassDiameterMM(kind, dimensionsMM);

        public static float DrumDepthMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float shellDepth = d.z - FRONT_FACE_SETBACK_MM;
            return Mathf.Min(DRUM_DEPTH_MM, shellDepth - DRUM_BACK_WALL_MM);
        }

        public static Vector3 ShellSizeMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            return new Vector3(d.x, d.y, d.z - FRONT_FACE_SETBACK_MM);
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM(
            LaundryMachineKind kind, Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float halfH = d.y * 0.5f;
            float halfD = d.z * 0.5f;
            float frontFaceZ = halfD - FRONT_FACE_SETBACK_MM;

            float drumBackZ = frontFaceZ - DrumDepthMM(d) + DRUM_BACK_GAP_MM;
            float drumBackDiameter = DrumDiameterMM(kind, d) - 2 * DRUM_BACK_INSET_MM;

            return new[]
            {
                (new Vector3(0f, 0f, -FRONT_FACE_SETBACK_MM * 0.5f), ShellSizeMM(d)),

                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f,
                     frontFaceZ + CONTROL_PANEL_THICKNESS_MM * 0.5f),
                 new Vector3(d.x, CONTROL_PANEL_HEIGHT_MM, CONTROL_PANEL_THICKNESS_MM)),

                (new Vector3(0f, HatchCenterYMM, drumBackZ + DRUM_BACK_THICKNESS_MM * 0.5f),
                 new Vector3(drumBackDiameter, drumBackDiameter, DRUM_BACK_THICKNESS_MM)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM(
            LaundryMachineKind kind, Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float halfD = d.z * 0.5f;
            float frontFaceZ = halfD - FRONT_FACE_SETBACK_MM;
            float hatchDiameter = HatchDiameterMM(kind, d);
            float glassDiameter = GlassDiameterMM(kind, d);
            float hatchFrontZ = frontFaceZ + HATCH_THICKNESS_MM;
            float glassBackZ = hatchFrontZ + GLASS_PROUD_MM - GLASS_THICKNESS_MM;

            return new[]
            {
                (new Vector3(0f, HatchCenterYMM, frontFaceZ + HATCH_THICKNESS_MM * 0.5f),
                 new Vector3(hatchDiameter, hatchDiameter, HATCH_THICKNESS_MM)),

                (new Vector3(0f, HatchCenterYMM, glassBackZ + GLASS_THICKNESS_MM * 0.5f),
                 new Vector3(glassDiameter, glassDiameter, GLASS_THICKNESS_MM)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] ClosedPartsMM(
            LaundryMachineKind kind, Vector3Int dimensionsMM)
        {
            var body = BodyPartsMM(kind, dimensionsMM);
            var door = DoorPartsMM(kind, dimensionsMM);
            var all = new (Vector3 centerMM, Vector3 sizeMM)[body.Length + door.Length];
            for (int i = 0; i < body.Length; i++) all[i] = body[i];
            for (int i = 0; i < door.Length; i++) all[body.Length + i] = door[i];
            return all;
        }

        public static Vector3 HingeLocalMM(LaundryMachineKind kind, Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            return new Vector3(
                -HatchDiameterMM(kind, d) * 0.5f,
                HatchCenterYMM,
                d.z * 0.5f - FRONT_FACE_SETBACK_MM);
        }
    }
}
