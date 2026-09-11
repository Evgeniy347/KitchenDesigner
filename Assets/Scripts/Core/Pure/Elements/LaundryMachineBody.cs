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

        public const int FRONT_THICKNESS_MM = 24;
        public const int CONTROL_PANEL_HEIGHT_MM = 90;

        public const int HATCH_MARGIN_MM = 60;
        public const int HATCH_RIM_WIDTH_MM = 25;
        public const int HATCH_BACK_INSET_MM = 4;
        public const int HATCH_RIM_FRONT_GAP_MM = 4;
        public const int HATCH_GLASS_BACK_INSET_MM = 8;
        public const int FRONT_FACE_SETBACK_MM = 8;

        public const int MIN_WIDTH_MM = 2 * HATCH_MARGIN_MM + 2 * HATCH_RIM_WIDTH_MM + 2;
        public const int MIN_HEIGHT_MM = CONTROL_PANEL_HEIGHT_MM + MIN_WIDTH_MM;
        public const int MIN_DEPTH_MM = FRONT_THICKNESS_MM + 1;

        public const int IdxShell = 0;
        public const int IdxControlPanel = 1;
        public const int IdxFrontPanel = 2;
        public const int IdxHatchRim = 3;
        public const int IdxHatchGlass = 4;
        public const int BodyPartCount = 3;
        public const int DoorPartCount = 2;
        public const int PartCount = BodyPartCount + DoorPartCount;

        public const string WasherName = "Стиральная машина";
        public const string DryerName = "Сушильная машина";

        public const string WASHER_TYPE_ID = "washing_machine";
        public const string DRYER_TYPE_ID = "dryer";

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

        public static Vector3Int DefaultDimensionsMM =>
            new Vector3Int(DEFAULT_WIDTH_MM, DEFAULT_HEIGHT_MM, DEFAULT_DEPTH_MM);

        public static Vector3Int ClampMM(Vector3Int dimensionsMM) => new Vector3Int(
            Mathf.Max(dimensionsMM.x, MIN_WIDTH_MM),
            Mathf.Max(dimensionsMM.y, MIN_HEIGHT_MM),
            Mathf.Max(dimensionsMM.z, MIN_DEPTH_MM));

        public static bool IsRound(int partIndex) =>
            partIndex == IdxHatchRim || partIndex == IdxHatchGlass;

        public static string PartName(int idx) => idx switch
        {
            IdxShell => "Shell",
            IdxControlPanel => "ControlPanel",
            IdxFrontPanel => "FrontPanel",
            IdxHatchRim => "HatchRim",
            _ => "HatchGlass",
        };

        public static float HatchDiameterMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float across = Mathf.Min(d.x, d.y - CONTROL_PANEL_HEIGHT_MM);
            return across - 2 * HATCH_MARGIN_MM;
        }

        public static float GlassDiameterMM(Vector3Int dimensionsMM) =>
            HatchDiameterMM(dimensionsMM) - 2 * HATCH_RIM_WIDTH_MM;

        public const float HatchCenterYMM = -CONTROL_PANEL_HEIGHT_MM * 0.5f;

        public static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float halfH = d.y * 0.5f;
            float halfD = d.z * 0.5f;
            float shellDepth = d.z - FRONT_THICKNESS_MM;
            float faceDepth = FRONT_THICKNESS_MM - FRONT_FACE_SETBACK_MM;
            float faceCenterZ = halfD - FRONT_FACE_SETBACK_MM - faceDepth * 0.5f;
            float frontPanelHeight = d.y - CONTROL_PANEL_HEIGHT_MM;

            return new[]
            {
                (new Vector3(0f, 0f, -FRONT_THICKNESS_MM * 0.5f),
                 new Vector3(d.x, d.y, shellDepth)),

                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f, faceCenterZ),
                 new Vector3(d.x, CONTROL_PANEL_HEIGHT_MM, faceDepth)),

                (new Vector3(0f, -CONTROL_PANEL_HEIGHT_MM * 0.5f, faceCenterZ),
                 new Vector3(d.x, frontPanelHeight, faceDepth)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float halfD = d.z * 0.5f;
            float hatchY = HatchCenterYMM;
            float rimDiameter = HatchDiameterMM(d);
            float glassDiameter = GlassDiameterMM(d);

            float rimBack = halfD - FRONT_THICKNESS_MM + HATCH_BACK_INSET_MM;
            float rimFront = halfD - HATCH_RIM_FRONT_GAP_MM;
            float glassBack = halfD - FRONT_THICKNESS_MM + HATCH_GLASS_BACK_INSET_MM;

            return new[]
            {
                (new Vector3(0f, hatchY, (rimBack + rimFront) * 0.5f),
                 new Vector3(rimDiameter, rimDiameter, rimFront - rimBack)),

                (new Vector3(0f, hatchY, (glassBack + halfD) * 0.5f),
                 new Vector3(glassDiameter, glassDiameter, halfD - glassBack)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] ClosedPartsMM(Vector3Int dimensionsMM)
        {
            var body = BodyPartsMM(dimensionsMM);
            var door = DoorPartsMM(dimensionsMM);
            var all = new (Vector3 centerMM, Vector3 sizeMM)[body.Length + door.Length];
            for (int i = 0; i < body.Length; i++) all[i] = body[i];
            for (int i = 0; i < door.Length; i++) all[body.Length + i] = door[i];
            return all;
        }

        public static Vector3 HingeLocalMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            return new Vector3(
                -HatchDiameterMM(d) * 0.5f,
                HatchCenterYMM,
                d.z * 0.5f - FRONT_THICKNESS_MM + HATCH_BACK_INSET_MM);
        }
    }
}
