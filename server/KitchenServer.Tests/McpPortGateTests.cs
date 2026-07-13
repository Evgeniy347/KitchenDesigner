using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

public class McpPortGateTests
{
    private const int HttpPort = 8080;
    private const int McpPort = 8081;

    [Theory]
    [InlineData("/mcp")]
    [InlineData("/mcp/message")]
    [InlineData("/health")]
    [InlineData("/alive")]
    public void McpPort_AllowsAgentRoutes(string path)
    {
        Assert.False(McpPortGate.ShouldReject(McpPort, path, McpPort));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/auth/login")]
    [InlineData("/api/projects")]
    [InlineData("/api/mcp/ws")]       // browser channel lives on the HTTP port, not here
    [InlineData("/unity/index.html")]
    [InlineData("/_blazor")]
    public void McpPort_RejectsEverythingElse(string path)
    {
        Assert.True(McpPortGate.ShouldReject(McpPort, path, McpPort));
    }

    [Theory]
    [InlineData("/mcp")]
    [InlineData("/mcp/message")]
    public void HttpPort_RejectsAgentMcpEndpoint(string path)
    {
        Assert.True(McpPortGate.ShouldReject(HttpPort, path, McpPort));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/auth/login")]
    [InlineData("/api/projects")]
    [InlineData("/api/mcp/ws")]       // browser/desktop client channel stays on HTTP
    [InlineData("/unity/index.html")]
    public void HttpPort_AllowsRegularRoutes(string path)
    {
        Assert.False(McpPortGate.ShouldReject(HttpPort, path, McpPort));
    }
}
