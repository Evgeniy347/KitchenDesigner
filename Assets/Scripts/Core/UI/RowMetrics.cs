namespace KitchenDesigner.Core.UI
{
    public readonly struct RowMetrics
    {
        private RowMetrics(float labelW, float gap, float valueW, float controlH, float rowStep, float numberW)
        {
            LabelW = labelW;
            Gap = gap;
            ValueW = valueW;
            ControlH = controlH;
            RowStep = rowStep;
            NumberW = numberW;
        }

        public float LabelW { get; }

        public float Gap { get; }

        public float ValueW { get; }

        public float ControlH { get; }

        public float RowStep { get; }

        public float NumberW { get; }

        public int LabelFont => UIStyle.FontBody;

        public float ValueX => LabelW + Gap;

        public float Width => LabelW + Gap + ValueW;

        public float RowGap => RowStep - ControlH;

        public static RowMetrics For(RowDensity density) => density switch
        {
            RowDensity.Compact => new RowMetrics(UIStyle.InspectorLabelW, UIStyle.InspectorColumnGap,
                UIStyle.InspectorValueW, UIStyle.ControlHCompact, UIStyle.RowStep, UIStyle.InspectorValueW),
            RowDensity.Tool => new RowMetrics(UIStyle.ToolLabelW, UIStyle.InspectorColumnGap,
                UIStyle.ToolPanelW - 2f * UIStyle.ToolPanelPad - UIStyle.ToolLabelW - UIStyle.InspectorColumnGap,
                UIStyle.ControlHCompact, UIStyle.RowStep, UIStyle.ToolFieldW),
            _ => new RowMetrics(UIStyle.SettingsLabelW, UIStyle.SettingsColumnGap, UIStyle.SettingsControlW,
                UIStyle.ControlH, UIStyle.ControlH + UIStyle.Space1, UIStyle.NumberFieldW),
        };
    }
}
