using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsAboutTab
    {
        private readonly SettingsPage _page;

        public SettingsAboutTab(SettingsPage page) => _page = page;

        internal static (string version, string buildDate)? PinnedBuild { get; set; }

        public static AboutEnvironment CurrentEnvironment() => new AboutEnvironment(
            PinnedBuild?.version ?? BuildInfo.Version, PinnedBuild?.buildDate ?? BuildInfo.BuildDate, Application.platform.ToString(),
            Application.unityVersion, SystemInfo.graphicsDeviceType.ToString(),
            SystemInfo.graphicsDeviceName);

        public void Build()
        {
            var env = CurrentEnvironment();
            var lines = AboutLines.For(env);
            var rows = AboutRows.For(env);

            _page.Rows.Gap(UIStyle.Space3);
            AddLine("AboutProduct", lines[0], UIStyle.FontWindowTitle, UIStyle.Text, UIStyle.ControlH);
            AddRows(rows);
            AddLine("AboutCopyright", lines[lines.Count - 1], UIStyle.FontSmall, UIStyle.TextSecondary,
                UIStyle.ControlHCompact);
        }

        private void AddRows(System.Collections.Generic.IReadOnlyList<AboutRow> rows)
        {
            float captionW = 0f;
            var probe = UIFactory.CreateLabel("AboutProbe", _page.Root, "", UIStyle.FontSmall, Vector2.zero,
                Vector2.zero);
            foreach (var row in rows) captionW = Mathf.Max(captionW, probe.GetPreferredValues(row.Caption).x);
            Object.DestroyImmediate(probe.gameObject);
            captionW = Mathf.Ceil(captionW) + UIStyle.Space4;

            string[] names = { "AboutBuild", "AboutPlatform", "AboutUnity", "AboutGraphicsApi", "AboutGpu" };
            for (int i = 0; i < rows.Count; i++) AddRow(names[i], rows[i], captionW);
        }

        private void AddRow(string name, AboutRow row, float captionW)
        {
            float height = UIStyle.ControlHCompact;
            float width = _page.Rows.Metrics.Width;
            var host = UIFactory.CreateRect("Row_" + name, _page.Root);

            var caption = UIFactory.CreateLabel(name + "_Caption", host, row.Caption, UIStyle.FontSmall,
                Vector2.zero, new Vector2(captionW, height), TextAnchor.MiddleLeft);
            caption.color = UIStyle.TextSecondary;
            caption.raycastTarget = false;
            caption.enableWordWrapping = false;
            Pin(caption.rectTransform, 0f, captionW, width);

            var value = SelectableLabel.Create(name, host, row.Value, UIStyle.FontSmall, Vector2.zero,
                new Vector2(width - captionW, height), UIStyle.Text);
            Plain(value);
            Pin((RectTransform)value.transform, captionW, width - captionW, width);

            _page.Block(host, height, row.Caption + " " + row.Value);
        }

        private void AddLine(string name, string text, int fontSize, Color color, float height)
        {
            float width = _page.Rows.Metrics.Width;
            var host = UIFactory.CreateRect("Row_" + name, _page.Root);
            var line = SelectableLabel.Create(name, host, text, fontSize, Vector2.zero,
                new Vector2(width, height), color);
            Plain(line);
            Pin((RectTransform)line.transform, -InsetOf(line), width + InsetOf(line), width);
            _page.Block(host, height, text);
        }

        private static float InsetOf(TMP_InputField field) => field.textViewport.offsetMin.x;

        private static void Plain(TMP_InputField field)
        {
            field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
            var stroke = field.transform.Find(UIFactory.FieldStrokeNode);
            if (stroke != null) stroke.gameObject.SetActive(false);
        }

        private static void Pin(RectTransform rect, float x, float width, float rowWidth)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(LayoutDirection.StartX(rowWidth, x, width), 0f);
        }
    }
}
