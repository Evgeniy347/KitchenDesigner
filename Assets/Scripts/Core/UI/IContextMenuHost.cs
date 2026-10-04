namespace KitchenDesigner.Core.UI
{
    internal interface IContextMenuHost
    {
        KitchenElement? Target { get; }

        InspectorRows Rows { get; }

        ContextMenuFieldTracker Fields { get; }

        ElementFacet TargetFacets { get; }

        DimensionFields SizeFields { get; }

        void Relayout();
    }
}
