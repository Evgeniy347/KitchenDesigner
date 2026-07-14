namespace KitchenDesigner.Core
{
    public enum DrawerType
    {
        A = 86,
        B = 120,
        C = 168,
        D = 200
    }

    public enum DrawerColor
    {
        Anthracite = 0,
        White = 1,
        Black = 2
    }

    public enum DoubleDrawerState
    {
        Closed = 0,
        BothOpen = 1,
        LowerOnly = 2
    }

    public static class DrawerConstants
    {
        public static readonly int[] ValidLengths = { 250, 300, 350, 400, 450, 500, 550, 600 };

        // Монтажные размеры GTV AXIS PRO (брошюра «Преимущества», стр. 6 и 8).
        // Ширина ящика задаётся как LW — проём корпуса «в свету».
        public const float SLIDE_CLEARANCE_PER_SIDE = 37.5f; // зазор направляющих на сторону
        public const int SIDE_WALL_THICKNESS = 14;           // металлическая боковина
        public const int PANEL_THICKNESS = 16;               // плита дна и задней стенки
        public const int BOTTOM_WIDTH_INSET = 75;            // дно: ширина = LW − 75
        public const int BACK_WIDTH_INSET = 87;              // задник: ширина = LW − 87
        public const int BOTTOM_DEPTH_INSET = 24;            // дно: глубина = NL − 24 (версия 1)
        public const int BACK_REAR_OFFSET = 8;               // задняя грань задника: NL − 8

        public const float DRAWER_SLIDE_METERS = 0.4f;
        public const float DRAWER_ANIM_DURATION = 0.4f;

        public static bool IsValidLength(int len)
        {
            return System.Array.IndexOf(ValidLengths, len) >= 0;
        }

        public static int GetTypeHeight(DrawerType type)
        {
            return (int)type;
        }

        /// <summary>Высота задней стенки (версия 1: задник до низа, дно упирается в него).</summary>
        public static int GetBackHeight(DrawerType type)
        {
            switch (type)
            {
                case DrawerType.A: return 84;
                case DrawerType.B: return 116;
                case DrawerType.C: return 167;
                case DrawerType.D: return 199;
                default: return GetTypeHeight(type) - 2;
            }
        }

        /// <summary>Минимальная высота проёма корпуса под ящик — габарит контура/снэпа.</summary>
        public static int GetMinOpeningHeight(DrawerType type)
        {
            switch (type)
            {
                case DrawerType.A: return 115;
                case DrawerType.B: return 147;
                case DrawerType.C: return 198;
                case DrawerType.D: return 230;
                default: return GetTypeHeight(type) + 30;
            }
        }

        /// <summary>Верх боковины над низом проёма (ящик приподнят направляющими).</summary>
        public static int GetMountedTopHeight(DrawerType type)
        {
            switch (type)
            {
                case DrawerType.A: return 107;
                case DrawerType.B: return 139;
                case DrawerType.C: return 190;
                case DrawerType.D: return 222;
                default: return GetTypeHeight(type) + 21;
            }
        }

        /// <summary>Подъём низа короба над низом проёма (направляющие).</summary>
        public static int GetBottomLift(DrawerType type) =>
            GetMountedTopHeight(type) - GetTypeHeight(type);

        public static string GetTypeLabel(DrawerType type)
        {
            switch (type)
            {
                case DrawerType.A: return "Низкий (A)";
                case DrawerType.B: return "Средний (B)";
                case DrawerType.C: return "Высокий (C)";
                case DrawerType.D: return "Очень высокий (D)";
                default: return type.ToString();
            }
        }

        public static string GetColorName(DrawerColor color)
        {
            switch (color)
            {
                case DrawerColor.Anthracite: return "Антрацит";
                case DrawerColor.White: return "Белый";
                case DrawerColor.Black: return "Чёрный";
                default: return color.ToString();
            }
        }

        public static string GetColorMaterialId(DrawerColor color)
        {
            switch (color)
            {
                case DrawerColor.Anthracite: return "gtv_anthracite";
                case DrawerColor.White: return "gtv_white";
                case DrawerColor.Black: return "gtv_black";
                default: return color.ToString().ToLowerInvariant();
            }
        }

        public static string GetCycleButtonLabel(DoubleDrawerState state)
        {
            switch (state)
            {
                case DoubleDrawerState.Closed: return "Открыть оба ящика";
                case DoubleDrawerState.BothOpen: return "Закрыть верхний ящик";
                case DoubleDrawerState.LowerOnly: return "Закрыть всё";
                default: return state.ToString();
            }
        }

        public static DoubleDrawerState NextCycleState(DoubleDrawerState current)
        {
            switch (current)
            {
                case DoubleDrawerState.Closed: return DoubleDrawerState.BothOpen;
                case DoubleDrawerState.BothOpen: return DoubleDrawerState.LowerOnly;
                case DoubleDrawerState.LowerOnly: return DoubleDrawerState.Closed;
                default: return DoubleDrawerState.Closed;
            }
        }
    }
}
