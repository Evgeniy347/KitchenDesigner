using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core.UI
{
    internal sealed class InterfaceReopenState
    {
        private readonly List<ToolbarPanel> _visiblePanels;
        private readonly KitchenElement? _contextTarget;
        private readonly int _settingsTab;

        private InterfaceReopenState(List<ToolbarPanel> visiblePanels, KitchenElement? contextTarget,
            int settingsTab)
        {
            _visiblePanels = visiblePanels;
            _contextTarget = contextTarget;
            _settingsTab = settingsTab;
        }

        internal static InterfaceReopenState Capture(UIManager ui) => new InterfaceReopenState(
            ui.VisiblePanels().ToList(),
            ui.ContextMenu != null ? ui.ContextMenu.OpenTarget : null,
            ui.SettingsPanel != null && ui.SettingsPanel.IsVisible ? ui.SettingsPanel.CurrentTab : -1);

        internal void Restore(UIManager ui)
        {
            foreach (var panel in _visiblePanels)
                if (panel != ToolbarPanel.Settings && !ui.IsPanelVisible(panel)) ui.TogglePanel(panel);

            if (_settingsTab >= 0 && ui.SettingsPanel != null) ui.SettingsPanel.OpenTab(_settingsTab);

            if (_contextTarget != null) ui.OpenContextMenu(_contextTarget);
        }
    }
}
