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
    }

    public interface IToolbarHost
    {
        void TogglePanel(ToolbarPanel panel);
        bool IsPanelVisible(ToolbarPanel panel);
        void SaveCurrent();
        void SaveAs();
        void LoadDialog();
    }
}
