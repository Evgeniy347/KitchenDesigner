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
                SaveLoadManager.LastPath = SaveLoadManager.PathForName(QuickSaveName);
                ShowSaved(QuickSaveName);
            }
        }

        public void SaveAs()
        {
            string suggested = SaveLoadManager.HasLastPath
                ? System.IO.Path.GetFileName(SaveLoadManager.LastPath)
                : SuggestedNameForANewFile();
            string? path = NativeFileDialog.SaveDialog("Сохранить проект кухни",
                suggested, SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.SaveToPath(path))
                ShowSaved(System.IO.Path.GetFileName(path));
        }

        public void LoadDialog()
        {
            string? path = NativeFileDialog.OpenDialog("Открыть проект кухни",
                SaveLoadManager.LastDirectory);
            if (string.IsNullOrEmpty(path)) return;
            if (SaveLoadManager.LoadFromPath(path))
                Toast("Загружено: " + System.IO.Path.GetFileName(path));
        }

        private static string SuggestedNameForANewFile() =>
            DemoMode.Current.IsActive ? DemoModeStrings.SuggestedFileName : "kitchen.json";

        private static void Toast(string msg) => ToastNotification.ShowIfAvailable(msg);

        private static void ShowSaved(string name) =>
            StatusBarUI.Instance?.ShowTransient("Сохранено: " + name, UIStyle.HighlightOk, 3f);
    }
}
