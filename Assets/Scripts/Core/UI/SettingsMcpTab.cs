using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsMcpTab
    {
        public const string GuideUrl =
            "https://github.com/Evgeniy347/KitchenDesigner/blob/develop/docs/MCP-CONNECT.md";

        public const string RepoUrl = "https://github.com/Evgeniy347/KitchenDesigner";

        public const string ServerName = "unity-kitchen";

        private readonly SettingsRowFactory _rows;

        private TMPro.TextMeshProUGUI? _status;

        public SettingsMcpTab(SettingsRowFactory rows)
        {
            _rows = rows;
        }

        public static int ActivePort() => McpBridgeStatus.Port;

        public static bool BridgeRunning() => McpBridgeStatus.Running;

        public static string StatusText() =>
            (BridgeRunning() ? "Мост работает" : "Мост остановлен") + $" · порт {ActivePort()}";

        public static string AgentPrompt() => AgentPrompt(ActivePort());

        public static string AgentPrompt(int port) =>
            "Подключи меня к Kitchen Designer по MCP.\n"
            + "\n"
            + $"Инструкция: {GuideUrl}\n"
            + "Прочитай её и сделай всё сам. Коротко, что требуется:\n"
            + "\n"
            + $"1. Kitchen Designer уже запущен и слушает TCP 127.0.0.1:{port}.\n"
            + $"2. Собери мост: склонируй {RepoUrl}, затем в папке mcp-server\n"
            + "   выполни npm install и npm run build (нужен Node.js 20 или новее).\n"
            + $"3. Пропиши мне MCP-сервер с именем {ServerName}:\n"
            + "   command = node\n"
            + "   args    = [\"<полный путь>/mcp-server/dist/index.js\"]\n"
            + $"   env     = {{ \"UNITY_MCP_PORT\": \"{port}\" }}\n"
            + "   Файл конфигурации зависит от того, какой ты агент — он назван в инструкции.\n"
            + "4. Перезапустись и вызови инструмент ping. Ответ ok означает, что связь есть.\n"
            + "\n"
            + "Учти: позиции в этом API задаются в метрах, размеры — в миллиметрах.";

        public static string ConfigSnippet() => ConfigSnippet(ActivePort());

        public static string ConfigSnippet(int port) =>
            "{\n"
            + "  \"mcpServers\": {\n"
            + $"    \"{ServerName}\": {{\n"
            + "      \"command\": \"node\",\n"
            + "      \"args\": [\"<полный путь>/mcp-server/dist/index.js\"],\n"
            + $"      \"env\": {{ \"UNITY_MCP_PORT\": \"{port}\" }}\n"
            + "    }\n"
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
                "Скопируйте текст ниже и отправьте его своему агенту — он прочитает "
                + "инструкцию и настроится сам.", 44f);
            AddWideButton(page, ref y, "McpCopyPrompt", "Скопировать инструкцию для агента",
                () => Copy(AgentPrompt()));
            AddWideButton(page, ref y, "McpCopyConfig", "Скопировать конфиг mcp.json",
                () => Copy(ConfigSnippet()));

            y -= SettingsRowFactory.GapPx * 3f;
            _rows.AddHeader(page, ref y, "Подробная инструкция");
            AddParagraph(page, ref y, "McpGuideUrl", GuideUrl, 44f);
            AddWideButton(page, ref y, "McpOpenGuide", "Открыть инструкцию на GitHub",
                () => Application.OpenURL(GuideUrl));
        }

        public void Refresh()
        {
            if (_status != null) _status.text = StatusText();
        }

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
