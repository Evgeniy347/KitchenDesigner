using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsAboutTab
    {
        private const float TitleH = 40f;
        private const float LineH = 28f;
        private const float CopyrightH = 24f;
        private const float LineGap = 4f;
        private const float CopyrightGap = 16f;

        public static AboutEnvironment CurrentEnvironment() => new AboutEnvironment(
            BuildInfo.Version, BuildInfo.BuildDate, Application.platform.ToString(),
            Application.unityVersion, SystemInfo.graphicsDeviceType.ToString(),
            SystemInfo.graphicsDeviceName);

        public void Build(Transform page)
        {
            var lines = AboutLines.For(CurrentEnvironment());
            int last = lines.Count - 1;

            float total = TitleH + (last - 1) * (LineH + LineGap) + CopyrightGap + CopyrightH;
            float y = total * 0.5f;

            for (int i = 0; i <= last; i++)
            {
                bool title = i == 0;
                bool copyright = i == last;
                float h = title ? TitleH : copyright ? CopyrightH : LineH;
                if (copyright) y -= CopyrightGap - LineGap;

                SelectableLabel.Create(NameOf(i), page, lines[i],
                    title ? UIStyle.FontWindowTitle : copyright ? UIStyle.FontSmall : UIStyle.FontBody,
                    new Vector2(0, y - h * 0.5f), new Vector2(SettingsRowFactory.ContentW, h),
                    copyright ? UIStyle.TextSecondary : UIStyle.Text);
                y -= h + LineGap;
            }
        }

        private static string NameOf(int index) => index switch
        {
            0 => "AboutProduct",
            1 => "AboutBuild",
            2 => "AboutPlatform",
            3 => "AboutUnity",
            4 => "AboutGraphicsApi",
            5 => "AboutGpu",
            _ => "AboutCopyright",
        };
    }
}
