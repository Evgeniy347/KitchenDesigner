using System.IO;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsMcpTab
    {
        public const string GuideFileName = "MCP-CONNECT.md";

        public const string GuideUrl =
            "https://github.com/Evgeniy347/KitchenDesigner/blob/develop/Assets/StreamingAssets/MCP-CONNECT.md";

        public const string RepoUrl = "https://github.com/Evgeniy347/KitchenDesigner";

        public const string ServerName = "unity-kitchen";

        private readonly SettingsRowFactory _rows;

        private TMPro.TextMeshProUGUI? _status;

        public SettingsMcpTab(SettingsRowFactory rows)
        {
            _rows = rows;
        }

        public static string GuidePath =>
            Path.Combine(Application.streamingAssetsPath, GuideFileName);

        public static bool GuideShipped => File.Exists(GuidePath);

        public static string GuideText()
        {
            try
            {
                if (GuideShipped) return File.ReadAllText(GuidePath);
            }
            catch (IOException)
            {
            }
            return Loc.T("settings.mcp.guideMissing") + GuideUrl;
        }

        public static int ActivePort() => McpBridgeStatus.Port;

        public static bool BridgeRunning() => McpBridgeStatus.Running;

        public static string StatusText() =>
            (BridgeRunning() ? Loc.T("settings.mcp.bridgeRunning") : Loc.T("settings.mcp.bridgeStopped")) + Loc.F("settings.mcp.port", ActivePort());

        public static string AgentPrompt() => AgentPrompt(ActivePort());

        public static string Url(int port) => "http://127.0.0.1:" + port + McpBridgeStatus.Path;

        public static string AgentPrompt(int port) =>
            Loc.F("settings.mcp.agentPrompt", Url(port), ServerName, ServerName, Url(port), Url(port), ServerName, Url(port));

        public static string ConfigSnippet() => ConfigSnippet(ActivePort());

        public static string ConfigSnippet(int port) =>
            "{\n"
            + "  \"mcpServers\": {\n"
            + $"    \"{ServerName}\": {{ \"url\": \"{Url(port)}\" }}\n"
            + "  }\n"
            + "}";

        public void Build(Transform page, float topY)
        {
            float y = topY;

            _rows.AddHeader(page, ref y, Loc.T("settings.mcp.section.about"));
            AddParagraph(page, ref y, "McpAbout",
                Loc.T("settings.mcp.aboutText"), 76f);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.mcp.section.status"));
            _status = AddParagraph(page, ref y, "McpStatus", StatusText(), 26f);
            AddParagraph(page, ref y, "McpStatusHint",
                Loc.T("settings.mcp.statusHint"), 44f);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, Loc.T("settings.mcp.section.connection"));
            AddParagraph(page, ref y, "McpConnectHint",
                Loc.T("settings.mcp.connectHint"), 44f);
            AddWideButton(page, ref y, "McpCopyPrompt", Loc.T("settings.mcp.copyPrompt"),
                () => Copy(AgentPrompt()));
            AddWideButton(page, ref y, "McpCopyConfig", Loc.T("settings.mcp.copyConfig"),
                () => Copy(ConfigSnippet()));

            y -= SettingsRowFactory.GapPx * 3f;
            _rows.AddHeader(page, ref y, Loc.T("settings.mcp.section.readYourself"));
            AddParagraph(page, ref y, "McpGuidePath",
                GuideFileName + Loc.T("settings.mcp.guideLocal"), 26f);
            AddWideButton(page, ref y, "McpOpenGuide", Loc.T("settings.mcp.openGuide"), OpenGuide);
        }

        public void Refresh()
        {
            if (_status != null) _status.text = StatusText();
        }

        private static void OpenGuide() =>
            Application.OpenURL(GuideShipped ? "file:///" + GuidePath.Replace('\\', '/') : GuideUrl);

        private static void Copy(string text) => GUIUtility.systemCopyBuffer = text;

        private static TMPro.TextMeshProUGUI AddParagraph(Transform page, ref float y,
            string name, string text, float height)
        {
            var label = UIFactory.CreateLabel(name, page, text, UIStyle.FontSmall,
                new Vector2(0, y - height * 0.5f + SettingsRowFactory.RowH * 0.5f),
                new Vector2(SettingsRowFactory.ContentW, height), TextAnchor.UpperLeft);
            y -= height + SettingsRowFactory.GapPx;
            return label;
        }

        private static void AddWideButton(Transform page, ref float y, string name, string caption,
            System.Action onClick)
        {
            UIFactory.CreateButton(name, page, caption,
                new Vector2(0, y - SettingsRowFactory.RowH * 0.5f),
                new Vector2(SettingsRowFactory.ContentW * 0.75f, SettingsRowFactory.RowH),
                onClick);
            y -= SettingsRowFactory.RowStep;
        }
    }
}
