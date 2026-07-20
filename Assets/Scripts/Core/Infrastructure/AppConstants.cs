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

        // Радиусная полка: радиус скругления угла по умолчанию.
        public const int RADIAL_CORNER_RADIUS_DEFAULT = 200;

        // Сборный (рамочный) фасад, см. каталог Союз-Фасад стр. 43.
        public const int ASSEMBLED_FRAME_MM = 100;        // ширина рамки A
        public const int ASSEMBLED_GLASS_DEDUCT_MM = 180; // вычет под вкладное стекло (L-180, H-180)
        public const int ASSEMBLED_GLASS_THICKNESS_MM = 4;
        public const int ASSEMBLED_DEFAULT_GROOVES = 1;   // >0 = рисовать выемки (2 сверху, 2 снизу)
        public const int ASSEMBLED_GROOVE_MM = 5;         // выемка на перекладине: 5×5 мм

        // Пазы детали в пласти, обозначение Bazis «Паз (16*4*7)»: смещение от
        // кромки × ширина прорези × глубина. Сквозной идёт во всю длину стороны;
        // глухой не доходит до торцов на GROOVE_BLIND_END_MM с каждой стороны
        // (длина = сторона − 2×7 мм).
        public const int GROOVE_OFFSET_MM = 16;
        public const int GROOVE_WIDTH_MM = 4;
        public const int GROOVE_DEPTH_MM = 7;
        public const int GROOVE_BLIND_END_MM = 7;
        // 4 стороны × 2 типа: дубли (сторона+тип) не имеют смысла, смещение фиксировано.
        public const int GROOVE_MAX_PER_PART = 8;

        public const int WINDOW_FRAME_MM = 80;
        public const int WINDOW_SLOPE_MM = 18;
        public const int WINDOW_DRIP_DEFAULT_MM = 30;
        public const int WINDOW_SILL_DEFAULT_MM = 50;
        public const int WINDOW_SILL_THICKNESS_MM = 40;   // толщина плиты подоконника (не зависит от вылета)
        public const int WINDOW_GLASS_THICKNESS_MM = 4;
        public const int WINDOW_SASH_MM = 50;             // ширина обвязки открывающейся створки
        public const int WINDOW_SASH_DEPTH_MM = 40;       // глубина створки (вдоль толщины стены)

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
