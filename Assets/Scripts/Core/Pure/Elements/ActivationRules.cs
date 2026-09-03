namespace KitchenDesigner.Core
{
    public static class ActivationRules
    {
        public static bool RespondsToHotkey(ActivationKind kind)
            => kind != ActivationKind.None;

        public static bool RespondsToDoubleClick(ActivationKind kind)
            => kind == ActivationKind.LightSwitch;
    }
}
