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

        public const float SLIDE_CLEARANCE_PER_SIDE = 37f;
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
