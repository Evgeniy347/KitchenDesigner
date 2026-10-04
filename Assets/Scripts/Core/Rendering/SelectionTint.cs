using UnityEngine;

namespace KitchenDesigner.Core
{
    public static class SelectionTint
    {
        public static Material Of(Material own, bool multi)
        {
            var ownColour = ValidityTint.BaseColorOf(own);
            var tinted = new Material(own) { name = ElementTint.SelectionName };
            var baseColour = SelectionTintMath.Base(ownColour, multi);
            if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", baseColour);
            if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", baseColour);
            tinted.EnableKeyword("_EMISSION");
            tinted.SetColor("_EmissionColor", SelectionTintMath.Emission(ownColour, multi));
            return tinted;
        }
    }
}
