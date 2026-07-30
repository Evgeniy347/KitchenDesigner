namespace KitchenDesigner.Core
{
    /// <summary>Сторона детали, у которой свой зазор.</summary>
    public enum GapSide
    {
        Left,
        Right,
        Top,
        Bottom,
        Front,
        Back,
    }

    /// <summary>Перевод стороны в индекс грани. Порядок граней — контракт
    /// (index/2 = ось: 0=X, 1=Y, 2=Z; чётный индекс = положительное
    /// направление), см. GetFaces. Таблица живёт здесь одна на всех, чтобы
    /// подсветка стороны в окне свойств и границы бокса не разъехались:
    /// перепутанные Front/Back подсветят не ту грань и сдвинут габарит в
    /// другую сторону.</summary>
    public static class GapSides
    {
        public static readonly GapSide[] All =
        {
            GapSide.Left, GapSide.Right, GapSide.Top,
            GapSide.Bottom, GapSide.Front, GapSide.Back,
        };

        public static int FaceIndex(GapSide side)
        {
            switch (side)
            {
                case GapSide.Right: return 0;   // +X
                case GapSide.Left: return 1;    // −X
                case GapSide.Top: return 2;     // +Y
                case GapSide.Bottom: return 3;  // −Y
                case GapSide.Front: return 4;   // +Z
                default: return 5;              // −Z
            }
        }
    }
}
