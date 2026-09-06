using TMPro;

namespace KitchenDesigner.Core.UI
{
    internal static class DimensionFieldValidation
    {
        public static TMP_InputField.OnValidateInput Char(bool allowDecimal = false) =>
            (text, index, ch) => ExpressionParser.IsValidDimensionChar(ch, allowDecimal) ? ch : '\0';
    }
}
