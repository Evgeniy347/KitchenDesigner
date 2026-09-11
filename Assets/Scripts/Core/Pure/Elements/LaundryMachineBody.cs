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
        public const float OVERLAY_THICKNESS_MM = 2f;
        public const int CONTROL_PANEL_HEIGHT_MM = 90;
        public const int PORTHOLE_MARGIN_MM = 70;

        public const int MIN_WIDTH_MM = 2 * PORTHOLE_MARGIN_MM + 1;
        public const int MIN_HEIGHT_MM = CONTROL_PANEL_HEIGHT_MM + 2 * PORTHOLE_MARGIN_MM + 1;
        public const int MIN_DEPTH_MM = FRONT_THICKNESS_MM + 1;

        public const int IdxShell = 0;
        public const int IdxControlPanel = 1;
        public const int IdxDoor = 2;
        public const int IdxPorthole = 3;
        public const int BodyPartCount = 2;
        public const int DoorPartCount = 2;
        public const int PartCount = BodyPartCount + DoorPartCount;

        public const string WasherName = "Стиральная машина";
        public const string DryerName = "Сушильная машина";

        public static string NameOf(LaundryMachineKind kind) =>
            kind == LaundryMachineKind.Dryer ? DryerName : WasherName;

        public static Vector3Int DefaultDimensionsMM =>
            new Vector3Int(DEFAULT_WIDTH_MM, DEFAULT_HEIGHT_MM, DEFAULT_DEPTH_MM);

        public static Vector3Int ClampMM(Vector3Int dimensionsMM) => new Vector3Int(
            Mathf.Max(dimensionsMM.x, MIN_WIDTH_MM),
            Mathf.Max(dimensionsMM.y, MIN_HEIGHT_MM),
            Mathf.Max(dimensionsMM.z, MIN_DEPTH_MM));

        public static string PartName(int idx) => idx switch
        {
            IdxShell => "Shell",
            IdxControlPanel => "ControlPanel",
            IdxDoor => "Door",
            _ => "Porthole",
        };

        public static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float halfH = d.y * 0.5f;
            float halfD = d.z * 0.5f;
            float shellDepth = d.z - FRONT_THICKNESS_MM;

            return new[]
            {
                (new Vector3(0f, 0f, -FRONT_THICKNESS_MM * 0.5f),
                 new Vector3(d.x, d.y, shellDepth)),

                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f,
                     halfD - FRONT_THICKNESS_MM * 0.5f),
                 new Vector3(d.x, CONTROL_PANEL_HEIGHT_MM, FRONT_THICKNESS_MM)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM(Vector3Int dimensionsMM)
        {
            var d = ClampMM(dimensionsMM);
            float halfD = d.z * 0.5f;
            float doorHeight = d.y - CONTROL_PANEL_HEIGHT_MM;
            float doorCenterY = DoorCenterYMM;
            float slabThickness = FRONT_THICKNESS_MM - OVERLAY_THICKNESS_MM;

            return new[]
            {
                (new Vector3(0f, doorCenterY,
                     halfD - OVERLAY_THICKNESS_MM - slabThickness * 0.5f),
                 new Vector3(d.x, doorHeight, slabThickness)),

                (new Vector3(0f, doorCenterY, halfD - OVERLAY_THICKNESS_MM * 0.5f),
                 new Vector3(d.x - 2 * PORTHOLE_MARGIN_MM,
                     doorHeight - 2 * PORTHOLE_MARGIN_MM, OVERLAY_THICKNESS_MM)),
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
            return new Vector3(-d.x * 0.5f, DoorCenterYMM, d.z * 0.5f - FRONT_THICKNESS_MM);
        }

        private const float DoorCenterYMM =
            -CONTROL_PANEL_HEIGHT_MM * 0.5f;
    }
}
