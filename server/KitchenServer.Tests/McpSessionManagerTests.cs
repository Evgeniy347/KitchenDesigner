using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

/// <summary>
/// Tests for the reworked <see cref="McpSessionManager"/>: tabs are keyed by their
/// ephemeral <see cref="McpSession.TabKey"/>, agents bind to a tab by object identity.
/// </summary>
public class McpSessionManagerTests
{
    [Fact]
    public void CreateSession_IsFoundByItsKey()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1", "proj-1");

        Assert.Same(session, mgr.GetByKey(session.TabKey));
        Assert.Equal("user1", session.UserId);
        Assert.Equal("proj-1", session.ProjectId);
    }

    [Fact]
    public void EachCreateSession_GetsAUniqueKey()
    {
        var mgr = new McpSessionManager();
        var a = mgr.CreateSession("user1");
        var b = mgr.CreateSession("user1");

        Assert.NotEqual(a.TabKey, b.TabKey);
    }

    [Fact]
    public void GetByKey_UnknownOrEmpty_ReturnsNull()
    {
        var mgr = new McpSessionManager();
        Assert.Null(mgr.GetByKey("nope"));
        Assert.Null(mgr.GetByKey(""));
    }

    [Fact]
    public void BindAgent_ThenGetBoundSession_ReturnsIt()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");
        var agent = new object();

        var sessionId = mgr.BindAgent(agent, session);

        Assert.NotNull(sessionId);
        Assert.Same(session, mgr.GetBoundSession(agent));
        Assert.Same(session, mgr.GetBoundSession(new object(), sessionId));
        Assert.True(session.AgentBound);
    }

    [Fact]
    public void UnbindAgent_ClearsBinding()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");
        var agent = new object();
        mgr.BindAgent(agent, session);

        mgr.UnbindAgent(agent);

        Assert.Null(mgr.GetBoundSession(agent));
        Assert.False(session.AgentBound);
    }

    [Fact]
    public void GetBoundSession_PrefersSessionIdOverUnboundServer()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");
        var agent = new object();
        var sessionId = mgr.BindAgent(agent, session);

        var differentServer = new object();
        Assert.Null(mgr.GetBoundSession(differentServer));
        Assert.Same(session, mgr.GetBoundSession(differentServer, sessionId));
    }

    [Fact]
    public void CloseSession_RemovesTabAndUnbindsAgents()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1", "proj-1");
        var agent = new object();
        var sessionId = mgr.BindAgent(agent, session);

        Assert.True(mgr.CloseSession(session.TabKey));

        Assert.Null(mgr.GetByKey(session.TabKey));
        Assert.Null(mgr.GetBoundSession(agent));
        Assert.Null(mgr.GetBoundSession(agent, sessionId));
        Assert.Null(mgr.GetSessionByProjectId("proj-1"));
    }

    [Fact]
    public void CloseSession_UnknownKey_ReturnsFalse()
    {
        var mgr = new McpSessionManager();
        Assert.False(mgr.CloseSession("missing"));
    }

    [Fact]
    public void SessionStateChanged_FiresOnBind()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");
        McpSession? notified = null;
        mgr.SessionStateChanged += s => notified = s;

        mgr.BindAgent(new object(), session);

        Assert.Same(session, notified);
    }
}
