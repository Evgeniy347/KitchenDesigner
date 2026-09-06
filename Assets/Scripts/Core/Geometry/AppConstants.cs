using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class AppConstants
    {
        public const string DEFAULT_MATERIAL_ID = "default";
        public const int SAVE_FORMAT_VERSION = 1;
        public const int DEFAULT_GRID_STEP = 1;
        public const int BOARD_THICKNESS_DEFAULT = 18;
        public const int BASE_PLATE_SIZE = 3000;
        public const float MM_TO_UNITS = 0.001f;

        public const int RADIAL_CORNER_RADIUS_DEFAULT = 200;

        public const int CHAIR_SEAT_HEIGHT_DEFAULT = 450;

        public const float OPENING_ANIM_DURATION_SECONDS = 0.4f;
        public const float OPENING_KEEP_AWAKE_MARGIN_SECONDS = 0.2f;

        public const int ASSEMBLED_FRAME_MM = 100;
        public const int ASSEMBLED_GLASS_DEDUCT_MM = 180;
        public const int GLASS_THICKNESS_MM = 4;
        public const int ASSEMBLED_DEFAULT_GROOVES = 1;
        public const int ASSEMBLED_GROOVE_MM = 5;

        public const int GROOVE_OFFSET_MM = 16;
        public const int GROOVE_WIDTH_MM = 4;
        public const int GROOVE_DEPTH_MM = 7;
        public const int GROOVE_BLIND_END_MM = 7;
        public const int GROOVE_MAX_PER_PART = 8;

        public const int EDGE_MAX_SIDE_MM = 50;
        public const float EDGE_THICKNESS_DEFAULT_MM = 1.0f;
        public const float EDGE_THICKNESS_MIN_MM = 0.1f;
        public const float EDGE_THICKNESS_MAX_MM = 5.0f;

        public const int WINDOW_FRAME_MM = 80;
        public const int WINDOW_SLOPE_MM = 18;
        public const int WINDOW_DRIP_DEFAULT_MM = 30;
        public const int WINDOW_SILL_DEFAULT_MM = 50;
        public const int WINDOW_SILL_THICKNESS_MM = 40;
        public const int WINDOW_SASH_MM = 50;
        public const int WINDOW_SASH_DEPTH_MM = 40;

        public static readonly Vector3Int[] PRESET_DIMENSIONS_MM = new Vector3Int[]
        {
            new Vector3Int(800, 400, 18),
            new Vector3Int(600, 400, 18),
            new Vector3Int(400, 400, 18),
            new Vector3Int(1200, 600, 18),
            new Vector3Int(600, 600, 18),
        };
    }
}
