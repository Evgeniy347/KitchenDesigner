namespace KitchenDesigner.Core
{
    public static class SceneGesture
    {
        public static bool InProgress => ElementMover.IsDragging || ResizeHandleManager.IsResizing;
    }
}
