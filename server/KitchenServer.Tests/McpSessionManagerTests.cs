using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

public class McpSessionManagerTests
{
    [Fact]
    public void CreateSession_IsRetrievableByKeyAndId()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1", "proj1");

        Assert.Same(session, mgr.ValidateAccess(session.AccessKey));
        Assert.Same(session, mgr.GetSession(session.SessionId));
        Assert.Equal("user1", session.UserId);
        Assert.Equal("proj1", session.ProjectId);
    }

    [Fact]
    public void ValidateAccess_UnknownKey_ReturnsNull()
    {
        var mgr = new McpSessionManager();
        Assert.Null(mgr.ValidateAccess("nope"));
    }

    [Fact]
    public void CloseSession_RevokesAccess()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");

        Assert.True(mgr.CloseSession(session.SessionId));
        Assert.Null(mgr.ValidateAccess(session.AccessKey));
        Assert.Null(mgr.GetSession(session.SessionId));
        Assert.False(mgr.CloseSession(session.SessionId));
    }

    [Fact]
    public void GetUserSessions_FiltersByUser()
    {
        var mgr = new McpSessionManager();
        mgr.CreateSession("user1");
        mgr.CreateSession("user1");
        mgr.CreateSession("user2");

        Assert.Equal(2, mgr.GetUserSessions("user1").Count);
        Assert.Single(mgr.GetUserSessions("user2"));
        Assert.Empty(mgr.GetUserSessions("user3"));
    }

    [Fact]
    public void RegisterMCPConnection_MapsConnectionToSession()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");

        mgr.RegisterMCPConnection(session.SessionId, "conn-1");

        Assert.Same(session, mgr.GetSessionByMCPConnection("conn-1"));
        Assert.Equal("conn-1", session.MCPConnectionId);

        mgr.RemoveConnection("conn-1");
        Assert.Null(mgr.GetSessionByMCPConnection("conn-1"));
        Assert.Null(session.MCPConnectionId);
    }

    [Fact]
    public void CleanupExpiredSessions_RemovesOnlyExpired()
    {
        var mgr = new McpSessionManager();
        var fresh = mgr.CreateSession("user1");
        var stale = mgr.CreateSession("user1");
        stale.SessionTtl = TimeSpan.FromMilliseconds(-1); // already expired

        mgr.CleanupExpiredSessions();

        Assert.NotNull(mgr.GetSession(fresh.SessionId));
        Assert.Null(mgr.GetSession(stale.SessionId));
        Assert.Null(mgr.ValidateAccess(stale.AccessKey));
    }

    [Fact]
    public void Touch_ProlongsSession()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");
        var before = session.LastActivity;

        Thread.Sleep(15);
        mgr.Touch(session.SessionId);

        Assert.True(session.LastActivity > before);
    }

    [Fact]
    public void IsActive_FalseWithoutConnections()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");

        Assert.False(session.IsActive);

        mgr.RegisterMCPConnection(session.SessionId, "conn-1");
        Assert.True(session.IsActive);
    }
}
