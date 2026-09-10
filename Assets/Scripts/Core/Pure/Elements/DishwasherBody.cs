using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class DishwasherBody
    {
        public const string MODEL = "Bosch SMV25EX02E";

        public const int BODY_WIDTH_MM = 598;
        public const int BODY_DEPTH_MM = 550;

        public const int HEIGHT_MIN_MM = 815;
        public const int HEIGHT_MAX_MM = 875;

        public const int BODY_HEIGHT_MM = HEIGHT_MIN_MM;

        public const int NICHE_WIDTH_MM = 600;
        public const int NICHE_MIN_DEPTH_MM = 550;

        public const int FACADE_WIDTH_MM = NICHE_WIDTH_MM;
        public const int FACADE_MIN_HEIGHT_MM = 655;
        public const int FACADE_MAX_HEIGHT_MM = 725;
        public const int FACADE_NOMINAL_HEIGHT_MM = 720;

        public const int PLINTH_MIN_MM = 90;
        public const int PLINTH_MAX_MM = 220;
        public const int PLINTH_NICHE_MM = 89;
        public const int PLINTH_SETBACK_MM = 53;

        public const int FEET_PROTRUSION_MM = 100;
        public const int FEET_ADJUST_MM = HEIGHT_MAX_MM - HEIGHT_MIN_MM;

        public const int BASE_HEIGHT_MM = BODY_HEIGHT_MM - FACADE_MAX_HEIGHT_MM;
        public const int BASE_SETBACK_MM = FEET_PROTRUSION_MM;
        public const int BASE_DEPTH_MM = BODY_DEPTH_MM - BASE_SETBACK_MM;

        public const int TANK_HEIGHT_MM = BODY_HEIGHT_MM - BASE_HEIGHT_MM;

        public const float FACADE_MOUNT_GAP_MM = 5f;

        public const int CONTROL_PANEL_HEIGHT_MM = 14;
        public const float OVERLAY_THICKNESS_MM = 2f;

        public const int BODY_WALL_MM = 20;
        public const int DOOR_THICKNESS_MM = 20;
        public const float DOOR_SLAB_THICKNESS_MM = DOOR_THICKNESS_MM - OVERLAY_THICKNESS_MM;

        public const float DOOR_OPEN_ANGLE_DEG = 90f;

        public const float TANK_CENTER_Y_MM = BASE_HEIGHT_MM * 0.5f;

        public const float TankBottomMM = -BODY_HEIGHT_MM * 0.5f + BASE_HEIGHT_MM;

        public const int IdxBodyBottom = 0;
        public const int IdxBodyTop = 1;
        public const int IdxBodyLeft = 2;
        public const int IdxBodyRight = 3;
        public const int IdxBodyBack = 4;
        public const int IdxBase = 5;
        public const int IdxDoor = 6;
        public const int IdxPanel = 7;
        public const int BodyPartCount = 6;
        public const int DoorPartCount = 2;
        public const int ChildCount = BodyPartCount + DoorPartCount;

        public static Vector3Int ModelDimensionsMM =>
            new Vector3Int(BODY_WIDTH_MM, BODY_HEIGHT_MM, BODY_DEPTH_MM);

        public static int PlinthForFacade(int facadeHeightMM) => BODY_HEIGHT_MM - facadeHeightMM;

        public static bool IsFacadeHeightValid(int facadeHeightMM) =>
            facadeHeightMM >= FACADE_MIN_HEIGHT_MM && facadeHeightMM <= FACADE_MAX_HEIGHT_MM;

        public static string ChildName(int idx) => idx switch
        {
            IdxBodyBottom => "BodyBottom",
            IdxBodyTop => "BodyTop",
            IdxBodyLeft => "BodyLeft",
            IdxBodyRight => "BodyRight",
            IdxBodyBack => "BodyBack",
            IdxBase => "Base",
            IdxDoor => "Door",
            _ => "ControlPanel",
        };

        public static (Vector3 centerMM, Vector3 sizeMM)[] BodyPartsMM()
        {
            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;
            float t = BODY_WALL_MM;

            float cy = TANK_CENTER_Y_MM;
            float bottom = TankBottomMM;
            float top = halfH;
            float back = -halfD;
            float depth = BODY_DEPTH_MM - DOOR_THICKNESS_MM;
            float cz = back + depth * 0.5f;

            float sideX = (BODY_WIDTH_MM - t) * 0.5f;
            float innerH = TANK_HEIGHT_MM - 2 * t;
            float innerW = BODY_WIDTH_MM - 2 * t;

            return new[]
            {
                (new Vector3(0f, bottom + t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, depth)),
                (new Vector3(0f, top - t * 0.5f, cz), new Vector3(BODY_WIDTH_MM, t, depth)),
                (new Vector3(-sideX, cy, cz), new Vector3(t, innerH, depth)),
                (new Vector3(sideX, cy, cz), new Vector3(t, innerH, depth)),
                (new Vector3(0f, cy, back + t * 0.5f), new Vector3(innerW, innerH, t)),
                (new Vector3(0f, -halfH + BASE_HEIGHT_MM * 0.5f, back + BASE_DEPTH_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, BASE_HEIGHT_MM, BASE_DEPTH_MM)),
            };
        }

        public static (Vector3 centerMM, Vector3 sizeMM)[] DoorPartsMM()
        {
            float halfH = BODY_HEIGHT_MM * 0.5f;
            float halfD = BODY_DEPTH_MM * 0.5f;

            return new[]
            {
                (new Vector3(0f, TANK_CENTER_Y_MM,
                     halfD - OVERLAY_THICKNESS_MM - DOOR_SLAB_THICKNESS_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, TANK_HEIGHT_MM, DOOR_SLAB_THICKNESS_MM)),
                (new Vector3(0f, halfH - CONTROL_PANEL_HEIGHT_MM * 0.5f,
                     halfD - OVERLAY_THICKNESS_MM * 0.5f),
                 new Vector3(BODY_WIDTH_MM, CONTROL_PANEL_HEIGHT_MM, OVERLAY_THICKNESS_MM)),
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

        public static Vector3 HingeLocalMM =>
            new Vector3(0f, TankBottomMM, BODY_DEPTH_MM * 0.5f - DOOR_THICKNESS_MM);
    }
}
