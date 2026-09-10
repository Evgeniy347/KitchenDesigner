namespace KitchenDesigner.Core
{
    public enum EscapeOwner
    {
        None,
        ElementDrag,
        LightPick,
        Measure,
        Eyedropper,
        ConfirmDelete,
        ContextMenu,
        GroupMenu,
        CatalogTileSelection,
        CatalogCollapse,
        DayNightPanel,
        MusicPanel,
    }

    public struct EscapeClaims
    {
        public bool Dragging;
        public bool LightPicking;
        public bool Measuring;
        public bool Eyedropping;
        public bool ConfirmArmed;
        public bool ContextMenuOpen;
        public bool GroupMenuOpen;
        public bool CatalogTileSelected;
        public bool CatalogCollapsible;
        public bool DayNightOpen;
        public bool MusicOpen;
    }

    public static class EscapeOwnership
    {
        public static EscapeOwner Resolve(EscapeClaims claims)
        {
            if (claims.Dragging) return EscapeOwner.ElementDrag;
            if (claims.LightPicking) return EscapeOwner.LightPick;
            if (claims.Measuring) return EscapeOwner.Measure;
            if (claims.Eyedropping) return EscapeOwner.Eyedropper;
            if (claims.ConfirmArmed) return EscapeOwner.ConfirmDelete;
            if (claims.ContextMenuOpen) return EscapeOwner.ContextMenu;
            if (claims.GroupMenuOpen) return EscapeOwner.GroupMenu;
            if (claims.CatalogTileSelected) return EscapeOwner.CatalogTileSelection;
            if (claims.CatalogCollapsible) return EscapeOwner.CatalogCollapse;
            if (claims.DayNightOpen) return EscapeOwner.DayNightPanel;
            if (claims.MusicOpen) return EscapeOwner.MusicPanel;
            return EscapeOwner.None;
        }
    }
}
