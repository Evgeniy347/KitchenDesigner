namespace KitchenDesigner.Core.UI
{
    public static class DropdownListMath
    {
        public static (float X, float Y) ShiftInto(
            float lowX, float lowY, float highX, float highY,
            float screenMinX, float screenMinY, float screenMaxX, float screenMaxY)
        {
            float x = 0f;
            float y = 0f;
            if (highX > screenMaxX) x = screenMaxX - highX;
            if (lowX + x < screenMinX) x = screenMinX - lowX;
            if (highY > screenMaxY) y = screenMaxY - highY;
            if (lowY + y < screenMinY) y = screenMinY - lowY;
            return (x, y);
        }

        public static float ScrollOffset(float itemCentreFromTop, float viewHeight, float contentHeight)
        {
            float room = contentHeight - viewHeight;
            if (room <= 0f) return 0f;
            float offset = itemCentreFromTop - viewHeight * 0.5f;
            return offset < 0f ? 0f : offset > room ? room : offset;
        }

        public static bool OpensAbove(float popupBottom, float screenBottom, float roomAbove, float popupHeight) =>
            popupBottom < screenBottom && roomAbove >= popupHeight;
    }
}
