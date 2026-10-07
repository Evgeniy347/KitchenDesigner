namespace KitchenDesigner.Core
{
    public interface IOpenable
    {
        bool IsOpen { get; }

        bool IsClosedPose { get; }

        string OpenActionLabel { get; }

        void SetOpen(bool open);

        void ToggleOpen();

        void CycleOpenState();

        void ForceClose();
    }
}
