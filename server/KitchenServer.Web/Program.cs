using KitchenServer.Web.Components;
using KitchenServer.Web.Data;
using KitchenServer.Web.Endpoints;
using KitchenServer.Web.Services;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
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
builder.Services.Configure<ServerSaveOptions>(builder.Configuration.GetSection(ServerSaveOptions.SectionName));

builder.Services.AddSingleton<ProjectStorageService>();
builder.Services.AddSingleton<McpSessionManager>();
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<McpSessionManager>());
builder.Services.AddHttpContextAccessor();

// Real MCP over Streamable HTTP (port 8081). tools/list + tools/call come from the
// shared C# contract; auth-first gate + relay to the browser tab live in McpToolHandlers.
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new Implementation { Name = "unity-kitchen", Version = "2.0.0" };
    options.ServerInstructions = McpToolHandlers.Instructions;
    options.Capabilities = new ServerCapabilities { Tools = new ToolsCapability() };
    options.Handlers.ListToolsHandler = McpToolHandlers.ListToolsAsync;
    options.Handlers.CallToolHandler = McpToolHandlers.CallToolAsync;
})
.WithHttpTransport();

// Stateless mode: opencode does not send Mcp-Session-Id, so session binding relies on
// the remote IP address as a fallback (single-user / home NAT scenario).
builder.Services.AddOptions<HttpServerTransportOptions>()
    .Configure(options =>
    {
        options.Stateless = true;
    });

// Per-circuit bridge: editor page → nav-bar island (see EditorNavState).
builder.Services.AddScoped<EditorNavState>();

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
app.MapConfigEndpoints();
app.MapProjectEndpoints();
app.MapMcpEndpoints();
app.MapMcp("/mcp");

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

            // EnsureCreated() won't add columns to an existing schema.
            // Run ALTER TABLE for new columns — safe to repeat (IF NOT EXISTS).
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "LockGuid" text NULL;
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "LockAcquiredAt" timestamp with time zone NULL;
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ProjectGroupId" uuid NULL;
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Version" integer NOT NULL DEFAULT 1;
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "IsLatest" boolean NOT NULL DEFAULT true;
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT false;
                    ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "DeletedAt" timestamp with time zone NULL;
                    """);
                app.Logger.LogInformation("Project versioning columns ensured");
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Failed to add versioning columns (may already exist)");
            }

            // Backfill ProjectGroupId for existing rows.
            try
            {
                await db.Database.ExecuteSqlRawAsync("""
                    UPDATE "Projects" SET "ProjectGroupId" = "Id" WHERE "ProjectGroupId" IS NULL;
                    """);
                app.Logger.LogInformation("ProjectGroupId backfill completed");
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "ProjectGroupId backfill failed (may already be done)");
            }

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
