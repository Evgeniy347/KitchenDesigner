using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    public sealed class StatusBarSink : IStatusSink
    {
        public void Show(string message, StatusLevel level, float seconds)
        {
            var bar = StatusBarUI.Instance;
            if (bar == null) return;
            bar.ShowTransient(message, level, seconds);
        }
    }
}
