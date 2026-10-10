using System;

namespace KitchenDesigner.Core.Update
{
    public interface IUpdateApplier
    {
        void ApplyAndRelaunch(string installerPath);
    }

    public interface IUpdateDialog
    {
        void ShowUpdateAvailable(string version, Action onUpdate, Action onCancel);
        void Hide();
    }
}
