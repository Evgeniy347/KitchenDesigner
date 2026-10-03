using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class NativeNameLabel
    {
        public static void Apply(TMP_Text label, string? language)
        {
            var script = ScriptFallbackFonts.For(language);
            if (script == ScriptFallbackFonts.Script.None) return;

            var font = OsFontFallback.FontFor(language);
            if (font != null) label.font = font;

            if (script == ScriptFallbackFonts.Script.Arabic && !label.TryGetComponent<RightToLeftLabel>(out _))
                RightToLeftLabel.AttachKeepingAlignment(label);

            label.ForceMeshUpdate();
        }
    }
}
