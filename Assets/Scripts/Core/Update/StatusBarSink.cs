using UnityEngine;
using KitchenDesigner.Core.UI;

namespace KitchenDesigner.Core.Update
{
    public sealed class StatusBarSink : IStatusSink
    {
        public void Show(string message, StatusLevel level, float seconds)
        {
            var bar = StatusBarUI.Instance;
            if (bar == null) return;
            bar.ShowTransient(message, ColorFor(level), seconds);
        }

        private static Color ColorFor(StatusLevel level) => level switch
        {
            StatusLevel.Success => new Color(0.45f, 0.82f, 0.45f, 1f),
            StatusLevel.Error => UIStyle.HighlightError,
            _ => UIStyle.TextSecondary,
        };
    }
}
