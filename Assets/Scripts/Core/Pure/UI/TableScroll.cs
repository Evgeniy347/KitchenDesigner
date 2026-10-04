using System;

namespace KitchenDesigner.Core.UI
{
    public static class TableScroll
    {
        public static float OffsetToReveal(float offset, float rowTop, float rowHeight, float viewportHeight,
            float contentHeight)
        {
            float max = Math.Max(0f, contentHeight - viewportHeight);
            if (viewportHeight <= 0f) return Math.Min(Math.Max(offset, 0f), max);

            float target = offset;
            if (rowTop < target) target = rowTop;
            else if (rowTop + rowHeight > target + viewportHeight) target = rowTop + rowHeight - viewportHeight;
            return Math.Min(Math.Max(target, 0f), max);
        }

        public static int RowsPerPage(float viewportHeight, float rowHeight) =>
            rowHeight <= 0f ? 1 : Math.Max(1, (int)Math.Floor(viewportHeight / rowHeight));
    }
}
