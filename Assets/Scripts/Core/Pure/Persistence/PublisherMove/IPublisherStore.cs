namespace KitchenDesigner.Core
{
    internal interface IPublisherStore
    {
        string Describe { get; }
        bool OldHoldsData();
        bool NewHoldsData();
        void Move();
    }
}
