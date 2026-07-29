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

    /// <summary>Система выдвижения ящика. От неё зависят раскрой видимого короба
    /// и то, как ящик попадает в спецификацию: GTV — цельной строкой (покупной
    /// комплект), Movento — раскладкой на отдельные деревянные детали (боковины,
    /// перед, задник, дно), которые уходят в CSV как самостоятельные позиции.</summary>
    public enum DrawerSystem
    {
        Gtv = 0,
        Movento = 1
    }

    public static class DrawerConstants
    {
        public static readonly int[] ValidLengths = { 250, 300, 350, 400, 450, 500, 550, 600 };

        /// <summary>Типы в порядке отображения — индексы дропдаунов UI.
        /// Значения enum — это высоты в мм, кастовать индекс↔enum напрямую нельзя.</summary>
        public static readonly DrawerType[] Types = { DrawerType.A, DrawerType.B, DrawerType.C, DrawerType.D };

        /// <summary>Верхний ящик двойной пары — всегда низкий внутренний (A).</summary>
        public const DrawerType UPPER_DRAWER_TYPE = DrawerType.A;

        public static int TypeIndex(DrawerType type) => System.Array.IndexOf(Types, type);

        public static DrawerType TypeFromIndex(int index) =>
            index >= 0 && index < Types.Length ? Types[index] : DrawerType.A;

        // Монтажные размеры GTV AXIS PRO (брошюра «Преимущества», стр. 6 и 8).
        // Ширина ящика задаётся как LW — проём корпуса «в свету».
        public const float SLIDE_CLEARANCE_PER_SIDE = 37.5f; // зазор направляющих на сторону
        public const int SIDE_WALL_THICKNESS = 14;           // металлическая боковина
        public const int PANEL_THICKNESS = 16;               // плита дна и задней стенки
        public const int BOTTOM_WIDTH_INSET = 75;            // дно: ширина = LW − 75
        public const int BACK_WIDTH_INSET = 87;              // задник: ширина = LW − 87
        public const int BOTTOM_DEPTH_INSET = 24;            // дно: глубина = NL − 24 (версия 1)
        public const int BACK_REAR_OFFSET = 8;               // задняя грань задника: NL − 8

        // Раскрой деревянного короба Movento (плита 16 мм). Формулы Blum,
        // «Building a MOVENTO drawer»: наружная ширина короба SKW = LW − 42
        // (зазор направляющих 21 мм на сторону), длина боковины = NL − 10,
        // перед и задник встают между боковин: ширина = SKW − 2·16 = LW − 74.
        // Дно приподнято над низом боковин на глубину ниши — в этот просвет уходит
        // скрытая направляющая; перед и задник стоят на дне, их высота = H − 14 − 16.
        public const int MOVENTO_BOARD_THICKNESS = 16;              // ДСП/МДФ боковин, переда, задника, дна
        public const int MOVENTO_SLIDE_CLEARANCE_PER_SIDE = 21;     // зазор направляющих на сторону
        public const int MOVENTO_WIDTH_INSET = 42;                  // короб: наружная ширина = LW − 42
        public const int MOVENTO_FRONT_BACK_INSET = 74;             // перед/задник: ширина = LW − 74
        public const int MOVENTO_SIDE_LENGTH_INSET = 10;            // боковина/дно: длина = NL − 10
        public const int MOVENTO_BOTTOM_NICHE = 14;                 // просвет под дном для скрытой направляющей

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

        public static string GetSystemLabel(DrawerSystem system)
        {
            switch (system)
            {
                case DrawerSystem.Gtv: return "GTV AXIS PRO";
                case DrawerSystem.Movento: return "Blum MOVENTO";
                default: return system.ToString();
            }
        }

        /// <summary>Имя по умолчанию для нового ящика этой системы.</summary>
        public static string GetDefaultName(DrawerSystem system)
        {
            switch (system)
            {
                case DrawerSystem.Movento: return "Ящик Movento";
                default: return "Ящик GTV";
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
