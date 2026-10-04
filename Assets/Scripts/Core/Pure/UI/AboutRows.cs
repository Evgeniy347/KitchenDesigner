using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public readonly struct AboutRow
    {
        public AboutRow(string caption, string value)
        {
            Caption = caption;
            Value = value;
        }

        public string Caption { get; }
        public string Value { get; }
    }

    public static class AboutRows
    {
        public static IReadOnlyList<AboutRow> For(AboutEnvironment environment) => new[]
        {
            Row(Loc.T("settings.about.build"), environment.BuildDate),
            Row(Loc.T("settings.about.platform"), environment.Platform),
            Row(Loc.T("settings.about.unity"), environment.UnityVersion),
            Row(Loc.T("settings.about.graphics"), environment.GraphicsApi),
            Row(Loc.T("settings.about.gpu"), environment.Gpu),
        };

        private static AboutRow Row(string template, string value)
        {
            int slot = template.IndexOf("{0}", System.StringComparison.Ordinal);
            string caption = slot < 0 ? template : template.Substring(0, slot).TrimEnd(' ', ':', '：');
            return new AboutRow(caption, value);
        }
    }
}
