namespace KitchenDesigner.Core.UI
{
    internal interface IContextMenuHost
    {
        KitchenElement? Target { get; }

        ContextMenuLayout Layout { get; }

        ContextMenuRowFactory Rows { get; }

        ContextMenuFieldTracker Fields { get; }

        bool TargetIsTable { get; }

        bool TargetIsDoor { get; }

        DimensionFields SizeFields { get; }

        void Relayout();
    }
}
