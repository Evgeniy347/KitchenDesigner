using System.Text;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsAboutTab
    {
        public const string ProductName = "Kitchen Designer";
        public const string Copyright = "Copyright (c) 2025 Evgeniy347";

        public static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{ProductName} {BuildInfo.Version}");
            sb.AppendLine($"Сборка: {BuildInfo.BuildDate}");
            sb.AppendLine($"Платформа: {Application.platform}");
            sb.AppendLine($"Unity: {Application.unityVersion}");
            sb.AppendLine($"Графика: {SystemInfo.graphicsDeviceType}");
            sb.Append(Copyright);
            return sb.ToString();
        }

        public void Build(Transform page, float topY)
        {
            float y = topY;

            AddLine(page, "AboutProduct", ProductName, UIStyle.FontWindowTitle, ref y, 34f);
            AddLine(page, "AboutVersion", $"Версия: {BuildInfo.Version}", 18, ref y, 32f);
            AddLine(page, "AboutDate", $"Сборка: {BuildInfo.BuildDate}", UIStyle.FontBody, ref y, 28f);

            y -= SettingsRowFactory.GapPx;
            UIFactory.CreateButton("AboutCopy", page, "Скопировать сведения о сборке",
                new Vector2(0, y - SettingsRowFactory.RowH * 0.5f),
                new Vector2(SettingsRowFactory.ContentW * 0.6f, SettingsRowFactory.RowH),
                () => GUIUtility.systemCopyBuffer = BuildReport());
            y -= SettingsRowFactory.RowStep;

            y -= SettingsRowFactory.GapPx;
            var copyright = AddLine(page, "AboutCopyright", Copyright, UIStyle.FontSmall, ref y, 24f);
            copyright.color = UIStyle.TextSecondary;
        }

        private static TMPro.TextMeshProUGUI AddLine(Transform page, string name, string text,
            int fontSize, ref float y, float height)
        {
            var label = UIFactory.CreateLabel(name, page, text, fontSize,
                new Vector2(0, y), new Vector2(SettingsRowFactory.ContentW, height),
                TextAnchor.MiddleCenter);
            y -= height + 4f;
            return label;
        }
    }
}
