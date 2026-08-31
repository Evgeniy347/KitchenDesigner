namespace KitchenDesigner.Core
{
    public struct ElementCreatedEvent
    {
        public KitchenElement element;
    }

    public struct ElementMovedEvent
    {
        public KitchenElement element;
        public Vector3Serializer previousPosition;
    }

    public struct ProjectLoadedEvent { }

    public struct SelectionChangedEvent
    {
        public KitchenElement previous;
        public KitchenElement current;
    }

    public struct PartRemovedEvent
    {
        public KitchenElement element;
    }
}
