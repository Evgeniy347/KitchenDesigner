namespace KitchenDesigner.Core.UI
{
    internal interface IContextMenuHost
    {
        KitchenElement? Target { get; }

        ContextMenuLayout Layout { get; }

        void Relayout();
    }
}
