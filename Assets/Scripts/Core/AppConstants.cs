using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class AppConstants
    {
        public const int SAVE_FORMAT_VERSION = 1;
        public const int DEFAULT_GRID_STEP = 1;
        public const float SNAP_THRESHOLD = 50f;
        public const int BOARD_THICKNESS_DEFAULT = 18;
        public const int BASE_PLATE_SIZE = 3000;
        public const float MM_TO_UNITS = 0.001f;

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
