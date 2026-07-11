using KitchenServer.Web.Components;
using KitchenServer.Web.Data;
using KitchenServer.Web.Endpoints;
using KitchenServer.Web.Hubs;
using KitchenServer.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Factory registration also provides a scoped AppDbContext (used by Identity and
// endpoints); Blazor circuits should resolve the factory to avoid sharing one
// context across concurrent UI events.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("kitchendb")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 10;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.AccessDeniedPath = "/auth/login";

    // API clients (Unity WebGL, agents) need status codes, not login-page redirects.
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

// Persist keys so auth cookies and antiforgery tokens survive container restarts.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("KitchenServer");
}

builder.Services.Configure<McpOptions>(builder.Configuration.GetSection(McpOptions.SectionName));
builder.Services.Configure<ProjectStorageOptions>(builder.Configuration.GetSection(ProjectStorageOptions.SectionName));

builder.Services.AddSingleton<ProjectStorageService>();
builder.Services.AddSingleton<McpUrlBuilder>();
builder.Services.AddSingleton<McpSessionManager>();
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<McpSessionManager>());
builder.Services.AddSignalR();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Respect X-Forwarded-* from the nginx front (cookie Secure flag, redirect URIs).
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Dedicated MCP port: agent routes only on it, nothing MCP-only elsewhere.
var mcpPort = app.Configuration.GetValue<int?>("Mcp:Port");
if (mcpPort is int port)
{
    app.Use(async (ctx, next) =>
    {
        if (McpPortGate.ShouldReject(ctx.Connection.LocalPort, ctx.Request.Path, port))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapStaticAssets();
UnityWebGLStaticFiles.Map(app);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/health");

app.MapAuthEndpoints();
app.MapProjectEndpoints();
app.MapMcpEndpoints();
app.MapHub<McpHub>("/hubs/mcp");

// First-run database initialization with retries: the DB container may still be
// starting. Fail hard if it never comes up — a half-alive app is worse.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    const int maxAttempts = 10;
    for (int attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            app.Logger.LogInformation("Database initialized (attempt {Attempt})", attempt);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
            app.Logger.LogWarning("DB init attempt {Attempt}/{Max} failed: {Error}. Retrying in {Delay}s",
                attempt, maxAttempts, ex.Message, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
}

app.Run();
