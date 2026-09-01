namespace KitchenDesigner.Core
{
    public readonly struct BoxGaps
    {
        public readonly int Left;
        public readonly int Right;
        public readonly int Top;
        public readonly int Bottom;
        public readonly int Front;
        public readonly int Back;

        public BoxGaps(int left, int right, int top, int bottom)
            : this(left, right, top, bottom, 0, 0)
        {
        }

        public BoxGaps(int left, int right, int top, int bottom, int front, int back)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
            Front = front;
            Back = back;
        }

        public static BoxGaps None => new BoxGaps(0, 0, 0, 0, 0, 0);

        public int Of(GapSide side)
        {
            switch (side)
            {
                case GapSide.Left: return Left;
                case GapSide.Right: return Right;
                case GapSide.Top: return Top;
                case GapSide.Bottom: return Bottom;
                case GapSide.Front: return Front;
                default: return Back;
            }
        }

        public int NonZeroCount
        {
            get
            {
                int n = 0;
                if (Left != 0) n++;
                if (Right != 0) n++;
                if (Top != 0) n++;
                if (Bottom != 0) n++;
                if (Front != 0) n++;
                if (Back != 0) n++;
                return n;
            }
        }
    }
}
