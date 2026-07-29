namespace KitchenDesigner.Core
{
    /// <summary>Тип паза: сквозной идёт во всю длину стороны, глухой не доходит
    /// до торцов на AppConstants.GROOVE_BLIND_END_MM с каждой стороны.</summary>
    public enum GrooveKind
    {
        Through = 0,
        Blind = 1,
    }

    /// <summary>Сторона детали, вдоль кромки которой идёт паз. Смещение паза
    /// (GROOVE_OFFSET_MM) отсчитывается от ЭТОЙ кромки внутрь пласти.</summary>
    public enum GrooveSide
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>Один паз детали: тип + сторона. Размеры (16×4×7) фиксированы
    /// константами — в каталоге раскроя это готовая позиция «Паз (16*4*7)»,
    /// а не произвольная фрезеровка.</summary>
    [System.Serializable]
    public struct GrooveSpec : System.IEquatable<GrooveSpec>
    {
        public GrooveKind kind;
        public GrooveSide side;

        public GrooveSpec(GrooveKind kind, GrooveSide side)
        {
            this.kind = kind;
            this.side = side;
        }

        public static string KindLabel(GrooveKind kind)
            => kind == GrooveKind.Blind ? "Глухой" : "Сквозной";

        public static string SideLabel(GrooveSide side) => side switch
        {
            GrooveSide.Top => "Верх",
            GrooveSide.Bottom => "Низ",
            GrooveSide.Left => "Лево",
            _ => "Право",
        };

        /// <summary>Обозначение как в каталоге раскроя: «Сквозной 16*4*7».</summary>
        public static string Designation(GrooveKind kind)
            => $"{KindLabel(kind)} {AppConstants.GROOVE_OFFSET_MM}*" +
               $"{AppConstants.GROOVE_WIDTH_MM}*{AppConstants.GROOVE_DEPTH_MM}";

        /// <summary>Строка для спецификации/CSV: «Сквозной 16*4*7:Верх».</summary>
        public override string ToString() => $"{Designation(kind)}:{SideLabel(side)}";

        public bool Equals(GrooveSpec other) => kind == other.kind && side == other.side;

        public override bool Equals(object? obj) => obj is GrooveSpec other && Equals(other);

        public override int GetHashCode() => ((int)kind * 4) + (int)side;
    }
}
