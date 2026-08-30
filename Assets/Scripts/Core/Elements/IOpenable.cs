namespace KitchenDesigner.Core
{
    public interface IOpenable
    {
        bool IsOpen { get; }

        bool IsClosedPose { get; }

        string OpenActionLabel { get; }

        void ToggleOpen();

        void CycleOpenState();

        void ForceClose();
    }
}
