namespace KitchenDesigner.Core.Tools
{
    public static class ToolMode
    {
        public static bool MouseCaptured =>
            Measure.MeasureMode.Active || EyedropperMode.Active;
    }
}
