using TMPro;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal static class UIRowEnabled
    {
        public static bool IsEnabled(Selectable? control)
        {
            if (control == null) return true;
            if (!control.interactable) return false;
            return control is not TMP_InputField input || !input.readOnly;
        }

        public static void SetRowEnabled(TMP_Text? label, Selectable? control, bool enabled)
        {
            SetControlEnabled(control, enabled);
            SetLabelEnabled(label, enabled);
        }

        public static void SetControlEnabled(Selectable? control, bool enabled)
        {
            if (control == null) return;
            control.interactable = enabled;
            if (control is TMP_InputField input) input.readOnly = !enabled;
            PaintControl(control, enabled);
        }

        public static void SetLabelEnabled(TMP_Text? label, bool enabled)
        {
            if (label == null) return;
            label.color = enabled ? UIStyle.Text : UIStyle.TextDisabled;
        }

        public static void SyncRow(TMP_Text? label, Selectable? control)
        {
            bool enabled = IsEnabled(control);
            SetLabelEnabled(label, enabled);
            PaintControl(control, enabled);
        }

        private static void PaintControl(Selectable? control, bool enabled)
        {
            switch (control)
            {
                case TMP_InputField input:
                    SetLabelEnabled(input.textComponent, enabled);
                    break;
                case TMP_Dropdown dropdown:
                    SetLabelEnabled(dropdown.captionText, enabled);
                    break;
                case Toggle toggle:
                    if (toggle.graphic != null)
                        toggle.graphic.color = enabled ? UIStyle.Accent : UIStyle.TextDisabled;
                    SetLabelEnabled(toggle.GetComponentInChildren<TMP_Text>(true), enabled);
                    break;
            }
        }
    }
}
