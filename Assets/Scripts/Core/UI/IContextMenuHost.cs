namespace KitchenDesigner.Core.UI
{
    internal interface IContextMenuHost
    {
        KitchenElement? Target { get; }

        ContextMenuLayout Layout { get; }

        ContextMenuRowFactory Rows { get; }

        ContextMenuFieldTracker Fields { get; }

        bool TargetIsTable { get; }

        void Relayout();
    }
}
