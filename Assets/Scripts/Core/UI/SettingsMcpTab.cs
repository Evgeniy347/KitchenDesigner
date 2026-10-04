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

        private readonly SettingsPage _page;

        private TMPro.TextMeshProUGUI? _status;

        public SettingsMcpTab(SettingsPage page)
        {
            _page = page;
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

        public void Build()
        {
            _page.Section(Loc.T("settings.mcp.section.about"));
            _page.Note("McpAbout", Loc.T("settings.mcp.aboutText"));

            _page.Section(Loc.T("settings.mcp.section.status"));
            _status = _page.Note("McpStatus", StatusText());
            _page.Note("McpStatusHint", Loc.T("settings.mcp.statusHint"));

            _page.Section(Loc.T("settings.mcp.section.connection"));
            _page.Note("McpConnectHint", Loc.T("settings.mcp.connectHint"));
            AddButton("McpCopyPrompt", Loc.T("settings.mcp.copyPrompt"), () => Copy(AgentPrompt()));
            AddButton("McpCopyConfig", Loc.T("settings.mcp.copyConfig"), () => Copy(ConfigSnippet()));

            _page.Section(Loc.T("settings.mcp.section.readYourself"));
            _page.Note("McpGuidePath", GuideFileName + Loc.T("settings.mcp.guideLocal"));
            AddButton("McpOpenGuide", Loc.T("settings.mcp.openGuide"), OpenGuide);
        }

        public void Refresh()
        {
            if (_status != null) _status.text = StatusText();
        }

        private static void OpenGuide() =>
            Application.OpenURL(GuideShipped ? "file:///" + GuidePath.Replace('\\', '/') : GuideUrl);

        private static void Copy(string text) => GUIUtility.systemCopyBuffer = text;

        private void AddButton(string name, string caption, System.Action onClick)
        {
            var host = UIFactory.CreateRect("Row_" + name, _page.Root);
            var button = UIFactory.CreateButton(name, host, caption, Vector2.zero,
                new Vector2(WindowFooter.MinButtonW, UIStyle.ControlH), onClick);
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            float width = WindowFooter.WidthFor(label, caption);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(LayoutDirection.IsRtl ? 1f : 0f, 0.5f);
            rect.sizeDelta = new Vector2(width, UIStyle.ControlH);
            rect.anchoredPosition = Vector2.zero;
            _page.Block(host, UIStyle.ControlH, caption);
        }
    }
}
