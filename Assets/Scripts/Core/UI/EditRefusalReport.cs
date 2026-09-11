using KitchenDesigner.Core.Update;

namespace KitchenDesigner.Core.UI
{
    public static class EditRefusalReport
    {
        public static void Show(string refusal)
        {
            if (string.IsNullOrEmpty(refusal)) return;
            StatusBarUI.Instance?.ShowTransient(refusal, StatusLevel.Error);
        }
    }
}
