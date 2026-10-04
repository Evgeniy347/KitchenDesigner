using System;
using KitchenDesigner.Core.Update;

namespace KitchenDesigner.Core.UI
{
    public sealed class ProjectFileActions
    {
        public const string QuickSaveName = "quicksave";

        public void SaveCurrent()
        {
            if (DemoMode.Current.IsActive)
            {
                SaveAs();
                return;
            }
            if (SaveLoadManager.HasLastPath)
            {
                if (SaveLoadManager.SaveToLastPath())
                    ShowSaved(System.IO.Path.GetFileName(SaveLoadManager.LastPath));
            }
            else if (SaveLoadManager.SaveProject(QuickSaveName))
            {
                SaveLoadManager.AdoptCurrentPath(SaveLoadManager.PathForName(QuickSaveName));
                ShowSaved(QuickSaveName);
            }
        }

        public void SaveAs()
        {
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : SuggestedNameForANewFile();
            string? path = NativeFileDialog.SaveDialog(Loc.T("file.dialog.saveProject"),
                suggested, SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.SaveToPath(path))
                ShowSaved(System.IO.Path.GetFileName(path));
        }

        public void NewProjectDialog(Action<bool>? onDone = null)
        {
            string? path = NativeFileDialog.SaveDialog(Loc.T("file.dialog.newProject"),
                SuggestedNameForANewFile(), SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path))
            {
                onDone?.Invoke(false);
                return;
            }
            bool ok = SaveLoadManager.CreateEmptyProjectAt(path!);
            if (ok) ShowSaved(System.IO.Path.GetFileName(path));
            onDone?.Invoke(ok);
        }

        public void LoadDialog(Action<bool>? onDone = null)
        {
            string? path = NativeFileDialog.OpenDialog(Loc.T("file.dialog.openProject"),
                SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path))
            {
                onDone?.Invoke(false);
                return;
            }
            OpenExisting(path!, onDone);
        }

        public void OpenExisting(string path, Action<bool>? onDone = null)
        {
            NewerVersionPrompt.Confirm(ProjectFileVersion.Of(path), BuildInfo.Version,
                () => onDone?.Invoke(Open(path)));
        }

        private static bool Open(string path)
        {
            if (!SaveLoadManager.LoadFromPath(path)) return false;
            Toast(Loc.T("toast.loaded") + System.IO.Path.GetFileName(path));
            PhotoLookMigrationNotice.ShowIfPending();
            return true;
        }

        private static string SuggestedNameForANewFile() =>
            DemoMode.Current.IsActive ? DemoModeStrings.SuggestedFileName : "kitchen.kdproj";

        private static void Toast(string msg) =>
            ToastNotification.ShowIfAvailable(msg, level: StatusLevel.Success);

        private static void ShowSaved(string name) =>
            StatusBarUI.Instance?.ShowTransient(Loc.T("status.saved") + name,
                StatusLevel.Success, 3f);
    }
}
