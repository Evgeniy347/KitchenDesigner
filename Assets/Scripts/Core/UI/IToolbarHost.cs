namespace KitchenDesigner.Core.UI
{
    public enum ToolbarPanel
    {
        Specification,
        Hierarchy,
        Errors,
        ProjectInstructions,
        Settings,
        DayNight,
        Music,
        LoadProject,
        Levels,
    }

    public interface IToolbarHost
    {
        void TogglePanel(ToolbarPanel panel);
        bool IsPanelVisible(ToolbarPanel panel);
        void SaveCurrent();
        void NewProject();
        void SaveAs();
        void LoadDialog();
    }
}
