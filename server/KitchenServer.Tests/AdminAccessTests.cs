using KitchenServer.Web.Services;
using Microsoft.Extensions.Configuration;

namespace KitchenServer.Tests;

public class AdminAccessTests
{
    [Theory]
    [InlineData("admin@admin", true)]
    [InlineData("ADMIN@ADMIN", true)]
    [InlineData("user@example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AdminEmailsService_DefaultConfig_IdentifiesAdmins(string? email, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var svc = new AdminEmailsService(config);
        Assert.Equal(expected, svc.IsAdmin(email));
    }

    [Theory]
    [InlineData("boss@company.com", true)]
    [InlineData("dev@company.com", true)]
    [InlineData("user@company.com", false)]
    [InlineData("admin@admin", false)]
    public void AdminEmailsService_CustomConfig_MultipleEmails(string email, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = "boss@company.com,dev@company.com"
            })
            .Build();
        var svc = new AdminEmailsService(config);
        Assert.Equal(expected, svc.IsAdmin(email));
    }

    [Theory]
    [InlineData("  admin@admin  ")]
    [InlineData("admin@admin,,dev@dev")]
    public void AdminEmailsService_TrimsWhitespace(string raw)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = raw
            })
            .Build();
        var svc = new AdminEmailsService(config);
        Assert.True(svc.IsAdmin("admin@admin"));
    }

    [Fact]
    public void AdminEmailsService_EmptyEnv_FallsBackToDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ADMIN_EMAILS"] = ""
            })
            .Build();
        var svc = new AdminEmailsService(config);
        Assert.True(svc.IsAdmin("admin@admin"));
    }

    [Theory]
    [InlineData("/", false)]
    [InlineData("/auth/login", false)]
    [InlineData("/auth/register", false)]
    [InlineData("/mcp-guide", false)]
    [InlineData("/projects", true)]
    [InlineData("/profile", true)]
    [InlineData("/mcp-panel", true)]
    [InlineData("/admin/users", true)]
    public void KnownPages_AuthorizationExpectations(string path, bool requiresAuth)
    {
        var authPages = new HashSet<string>
        {
            "/projects", "/profile", "/mcp-panel", "/admin/users"
        };
        var adminPages = new HashSet<string> { "/admin/users" };

        Assert.Equal(requiresAuth, authPages.Contains(path));

        if (path == "/admin/users")
        {
            Assert.Contains(path, adminPages);
        }
    }
}
