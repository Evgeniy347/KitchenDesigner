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
            return "Инструкция не найдена рядом с программой. Она же лежит здесь: " + GuideUrl;
        }

        public static int ActivePort() => McpBridgeStatus.Port;

        public static bool BridgeRunning() => McpBridgeStatus.Running;

        public static string StatusText() =>
            (BridgeRunning() ? "Мост работает" : "Мост остановлен") + $" · порт {ActivePort()}";

        public static string AgentPrompt() => AgentPrompt(ActivePort());

        public static string Url(int port) => "http://127.0.0.1:" + port + McpBridgeStatus.Path;

        public static string AgentPrompt(int port) =>
            "Подключи меня к Kitchen Designer по MCP.\n"
            + "\n"
            + "Сервер уже запущен на этом компьютере и ждёт подключения:\n"
            + $"  {Url(port)}\n"
            + "Транспорт — Streamable HTTP, авторизации нет, слушает только localhost.\n"
            + "\n"
            + $"Добавь его в свою конфигурацию под именем {ServerName}. Например:\n"
            + $"  Claude Code:      claude mcp add --transport http {ServerName} {Url(port)}\n"
            + $"  Cursor / VS Code: запись с \"url\": \"{Url(port)}\"\n"
            + $"  Codex:            [mcp_servers.{ServerName}]  url = \"{Url(port)}\"\n"
            + "\n"
            + "Когда подключишься, вызови инструмент ping и скажи, что он ответил, затем "
            + "прочитай get_project_instructions. Если сервер недоступен — программа "
            + "Kitchen Designer закрыта, попроси меня открыть её.";

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

            _rows.AddHeader(page, ref y, "Что это даёт");
            AddParagraph(page, ref y, "McpAbout",
                "Программа отдаёт открытый проект ИИ-агенту по протоколу MCP. Агент "
                + "расставляет короба, выравнивает фасады, ищет пересечения и собирает "
                + "спецификацию прямо в вашей сцене — правки видно сразу, отмена работает "
                + "как обычно.", 76f);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Состояние");
            _status = AddParagraph(page, ref y, "McpStatus", StatusText(), 26f);
            AddParagraph(page, ref y, "McpStatusHint",
                "Мост включается вместе с программой. Пока она открыта, агенту есть куда "
                + "подключаться.", 44f);

            y -= SettingsRowFactory.GapPx;
            _rows.AddHeader(page, ref y, "Подключение");
            AddParagraph(page, ref y, "McpConnectHint",
                "Кнопка кладёт в буфер короткий текст с адресом сервера. Вставьте его "
                + "агенту — он подключится сам.", 44f);
            AddWideButton(page, ref y, "McpCopyPrompt", "Скопировать инструкцию для агента",
                () => Copy(AgentPrompt()));
            AddWideButton(page, ref y, "McpCopyConfig", "Скопировать конфиг mcp.json",
                () => Copy(ConfigSnippet()));

            y -= SettingsRowFactory.GapPx * 3f;
            _rows.AddHeader(page, ref y, "Прочитать самому");
            AddParagraph(page, ref y, "McpGuidePath",
                GuideFileName + " лежит рядом с программой, интернет не нужен", 26f);
            AddWideButton(page, ref y, "McpOpenGuide", "Открыть инструкцию", OpenGuide);
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
