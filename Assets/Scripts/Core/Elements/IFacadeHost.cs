namespace KitchenDesigner.Core
{
    public interface IFacadeHost
    {
        string AttachedFacadeName { get; set; }

        FacadeElement? FindAttachedFacade();

        float FacadeMountGapMm { get; }

        void OnAttachedFacadeChanged(FacadeElement? oldFacade, FacadeElement? newFacade);
    }
}
