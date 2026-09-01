namespace KitchenDesigner.Core.UI
{
    internal static class ElementFacets
    {
        public static ElementFacet Of(KitchenElement element)
        {
            var facets = ElementFacet.None;
            if (element is FacadeElement) facets |= ElementFacet.Facade;
            if (element is AssembledFacadeElement) facets |= ElementFacet.Assembled;
            if (element is RadialShelfElement) facets |= ElementFacet.Radial;
            if (element is DrawerElement) facets |= ElementFacet.Drawer;
            if (element is TableElement || element is RadiusTableElement) facets |= ElementFacet.Table;
            if (element is StoolElement) facets |= ElementFacet.Stool;
            if (element is ChairElement) facets |= ElementFacet.Chair;
            if (element is SofaElement) facets |= ElementFacet.Sofa;
            if (element is PillarElement) facets |= ElementFacet.Pillar;
            if (element is WindowElement || element is DoorElement) facets |= ElementFacet.Window;
            if (element is DoorElement) facets |= ElementFacet.Door;
            if (element is LightSourceElement) facets |= ElementFacet.Light;
            if (element is OvenElement) facets |= ElementFacet.Oven;
            if (element is DishwasherElement) facets |= ElementFacet.Dishwasher;
            if (element.SupportsGrooves) facets |= ElementFacet.Part;
            return facets;
        }

        public static bool Has(this ElementFacet facets, ElementFacet facet) =>
            (facets & facet) != ElementFacet.None;
    }
}
