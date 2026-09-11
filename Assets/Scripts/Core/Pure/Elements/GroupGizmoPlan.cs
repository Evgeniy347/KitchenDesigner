namespace KitchenDesigner.Core
{
    public readonly struct GroupGizmoPlan
    {
        public readonly bool GroupOutline;
        public readonly bool GroupMoveArrows;
        public readonly bool PerElementResizeHandles;
        public readonly bool PerElementMoveHandles;

        private GroupGizmoPlan(bool groupOutline, bool groupMoveArrows,
            bool perElementResizeHandles, bool perElementMoveHandles)
        {
            GroupOutline = groupOutline;
            GroupMoveArrows = groupMoveArrows;
            PerElementResizeHandles = perElementResizeHandles;
            PerElementMoveHandles = perElementMoveHandles;
        }

        public static GroupGizmoPlan For(int selectedCount, bool resizeMode)
        {
            if (selectedCount >= 2)
                return new GroupGizmoPlan(true, true, false, false);
            if (selectedCount == 1)
                return new GroupGizmoPlan(false, false, resizeMode, !resizeMode);
            return new GroupGizmoPlan(false, false, false, false);
        }
    }
}
