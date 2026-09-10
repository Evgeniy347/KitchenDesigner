using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class OvenBody
    {
        public const int FACADE_WIDTH_MM = 594;
        public const int FACADE_HEIGHT_MM = 595;
        public const float FACADE_THICKNESS_MM = 19.5f;

        public const int BODY_WIDTH_MM = 560;
        public const int BODY_DEPTH_MM = 548;
        public const int BODY_HEIGHT_MM = 570;

        public const int FACADE_TOP_OVERHANG_MM = 25;

        public const int FACADE_BOTTOM_OVERHANG_MM =
            FACADE_HEIGHT_MM - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM;

        public const int BODY_WALL_MM = 20;

        public const int CONTROL_PANEL_HEIGHT_MM = 96;
        public const int GLASS_HEIGHT_MM = FACADE_HEIGHT_MM - CONTROL_PANEL_HEIGHT_MM;
        public const int DOOR_FRAME_MM = 15;
        public const float OVERLAY_THICKNESS_MM = 2f;

        public const float FACADE_SLAB_THICKNESS_MM =
            FACADE_THICKNESS_MM - OVERLAY_THICKNESS_MM;

        public const int HANDLE_TOP_MM = CONTROL_PANEL_HEIGHT_MM;
        public const int HANDLE_HEIGHT_MM = 28;
        public const int HANDLE_SIDE_INSET_MM = 12;
        public const int HANDLE_PROTRUSION_MM = 50;

        public const float TOTAL_DEPTH_MM = BODY_DEPTH_MM + FACADE_THICKNESS_MM;
        public const int DEPTH_MM = 568;

        public const int IdxBodyBottom = 0;
        public const int IdxBodyTop = 1;
        public const int IdxBodyLeft = 2;
        public const int IdxBodyRight = 3;
        public const int IdxBodyBack = 4;
        public const int IdxFacade = 5;
        public const int IdxGlass = 6;
        public const int IdxPanel = 7;
        public const int IdxHandle = 8;
        public const int BodyPartCount = 5;
        public const int DoorPartCount = 4;
        public const int PartCount = BodyPartCount + DoorPartCount;

        public const float BODY_CENTER_Y_MM =
            FACADE_HEIGHT_MM * 0.5f - FACADE_TOP_OVERHANG_MM - BODY_HEIGHT_MM * 0.5f;

        public const float BODY_CENTER_Z_MM =
            TOTAL_DEPTH_MM * 0.5f - FACADE_THICKNESS_MM - BODY_DEPTH_MM * 0.5f;

        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, DEPTH_MM);

        public static Vector3 HingeLocalMM => new Vector3(
            0f, -FACADE_HEIGHT_MM * 0.5f, TOTAL_DEPTH_MM * 0.5f - FACADE_THICKNESS_MM);

        public static string PartName(int idx) => idx switch
        {
            IdxBodyBottom => "BodyBottom",
            IdxBodyTop => "BodyTop",
            IdxBodyLeft => "BodyLeft",
            IdxBodyRight => "BodyRight",
            IdxBodyBack => "BodyBack",
            IdxFacade => "Facade",
            IdxGlass => "Glass",
            IdxPanel => "ControlPanel",
            _ => "Handle",
        };

        public static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM()
        {
            float halfH = FACADE_HEIGHT_MM * 0.5f;
            float cy = BODY_CENTER_Y_MM;
            float cz = BODY_CENTER_Z_MM;
            float t = BODY_WALL_MM;

            float top = halfH - FACADE_TOP_OVERHANG_MM;
            float bottom = top - BODY_HEIGHT_MM;
            float back = cz - BODY_DEPTH_MM * 0.5f;
            float sideX = (BODY_WIDTH_MM - t) * 0.5f;
            float innerH = BODY_HEIGHT_MM - 2 * t;
            float innerW = BODY_WIDTH_MM - 2 * t;

            return new[]
            {
                (new Vector3(0f, bottom + t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, BODY_DEPTH_MM)),
                (new Vector3(0f, top - t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, BODY_DEPTH_MM)),
                (new Vector3(-sideX, cy, cz), new Vector3(t, innerH, BODY_DEPTH_MM)),
                (new Vector3(sideX, cy, cz), new Vector3(t, innerH, BODY_DEPTH_MM)),
                (new Vector3(0f, cy, back + t * 0.5f), new Vector3(innerW, innerH, t)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM()
        {
            float halfH = FACADE_HEIGHT_MM * 0.5f;
            float halfD = TOTAL_DEPTH_MM * 0.5f;

            float overlayZ = halfD - OVERLAY_THICKNESS_MM * 0.5f;
            float slabZ = halfD - OVERLAY_THICKNESS_MM - FACADE_SLAB_THICKNESS_MM * 0.5f;
            float doorTopY = halfH - CONTROL_PANEL_HEIGHT_MM;

            return new[]
            {
                (new Vector3(0f, 0f, slabZ),
                 new Vector3(FACADE_WIDTH_MM, FACADE_HEIGHT_MM, FACADE_SLAB_THICKNESS_MM)),
                (new Vector3(0f, (doorTopY - halfH) * 0.5f, overlayZ),
                 new Vector3(FACADE_WIDTH_MM - 2 * DOOR_FRAME_MM,
                     GLASS_HEIGHT_MM - 2 * DOOR_FRAME_MM, OVERLAY_THICKNESS_MM)),
                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f, overlayZ),
                 new Vector3(FACADE_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM)),
                (new Vector3(0f, halfH - HANDLE_TOP_MM - HANDLE_HEIGHT_MM * 0.5f,
                     halfD + HANDLE_PROTRUSION_MM * 0.5f),
                 new Vector3(FACADE_WIDTH_MM - 2 * HANDLE_SIDE_INSET_MM,
                     HANDLE_HEIGHT_MM, HANDLE_PROTRUSION_MM)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] ClosedPartsMM()
        {
            var body = BodyPartsMM();
            var door = DoorPartsMM();
            var all = new (Vector3 centerMM, Vector3 sizeMM)[body.Length + door.Length];
            for (int i = 0; i < body.Length; i++) all[i] = body[i];
            for (int i = 0; i < door.Length; i++) all[body.Length + i] = door[i];
            return all;
        }
    }
}
