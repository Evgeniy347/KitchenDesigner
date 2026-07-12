using KitchenServer.Web.Services;

namespace KitchenServer.Tests;

/// <summary>
/// Tests for <see cref="McpSessionManager.GetSessionByProjectId"/>.
/// </summary>
public class McpSessionManagerLockTests
{
    [Fact]
    public void GetSessionByProjectId_ExistingProject_ReturnsSession()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1", "proj-abc");

        var found = mgr.GetSessionByProjectId("proj-abc");

        Assert.Same(session, found);
    }

    [Fact]
    public void GetSessionByProjectId_NoMatch_ReturnsNull()
    {
        var mgr = new McpSessionManager();
        mgr.CreateSession("user1", "proj-abc");

        Assert.Null(mgr.GetSessionByProjectId("proj-xyz"));
    }

    [Fact]
    public void GetSessionByProjectId_NullProjectId_Ignored()
    {
        var mgr = new McpSessionManager();
        mgr.CreateSession("user1"); // no projectId

        Assert.Null(mgr.GetSessionByProjectId("proj-abc"));
    }

    [Fact]
    public void GetSessionByProjectId_MultipleSessions_AllSameProject_AnyReturned()
    {
        var mgr = new McpSessionManager();
        var first = mgr.CreateSession("user1", "proj-shared");
        mgr.CreateSession("user2", "proj-shared");

        var found = mgr.GetSessionByProjectId("proj-shared");

        Assert.NotNull(found);
        Assert.Equal("proj-shared", found!.ProjectId);
    }

    [Fact]
    public void GetSessionByProjectId_AfterClose_ReturnsNull()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1", "proj-abc");

        mgr.CloseSession(session.SessionId);

        Assert.Null(mgr.GetSessionByProjectId("proj-abc"));
    }

    [Fact]
    public void GetSessionByProjectId_ProjectIdCanBeUpdated()
    {
        var mgr = new McpSessionManager();
        var session = mgr.CreateSession("user1");

        Assert.Null(mgr.GetSessionByProjectId("proj-new"));

        session.ProjectId = "proj-new";

        Assert.Same(session, mgr.GetSessionByProjectId("proj-new"));
    }
}
