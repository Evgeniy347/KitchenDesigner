using Microsoft.Extensions.Options;

namespace KitchenServer.Web.Services;

/// <summary>
/// Builds the WebSocket URLs advertised to MCP agents and shown in the UI.
/// Never hardcodes host/port: uses Mcp:PublicUrl when set, otherwise derives
/// from the caller-visible base URL plus the dedicated MCP port.
/// </summary>
public class McpUrlBuilder
{
    private readonly McpOptions _options;

    public McpUrlBuilder(IOptions<McpOptions> options) => _options = options.Value;

    /// <param name="requestBaseUrl">Caller-visible base, e.g. "http://host:8080/" (request or NavigationManager.BaseUri).</param>
    public string HubUrl(string requestBaseUrl, string accessKey) =>
        $"{WsBase(requestBaseUrl, useMcpPort: true)}/hubs/mcp?access_key={Uri.EscapeDataString(accessKey)}";

    public string BrowserWsUrl(string requestBaseUrl, string accessKey) =>
        $"{WsBase(requestBaseUrl, useMcpPort: false)}/api/mcp/ws?key={Uri.EscapeDataString(accessKey)}";

    private string WsBase(string requestBaseUrl, bool useMcpPort)
    {
        if (!string.IsNullOrEmpty(_options.PublicUrl) && useMcpPort)
            return _options.PublicUrl.TrimEnd('/');

        var uri = new Uri(requestBaseUrl);
        var scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        var port = useMcpPort && _options.Port is int mcpPort ? mcpPort : uri.Port;
        return $"{scheme}://{uri.Host}:{port}";
    }
}
