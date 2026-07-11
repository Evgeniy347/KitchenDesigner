using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;
using KitchenServer.Web.Components;
using KitchenServer.Web.Data;
using KitchenServer.Web.Endpoints;
using KitchenServer.Web.Hubs;
using KitchenServer.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("kitchendb")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/login";
});

builder.Services.AddSingleton<ProjectStorageService>();
builder.Services.AddSingleton<McpSessionManager>();
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<McpSessionManager>());
builder.Services.AddSignalR();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.MapProjectEndpoints();
app.MapMcpEndpoints();
app.MapHub<McpHub>("/hubs/mcp");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    for (int retry = 0; retry < 10; retry++)
    {
        try
        {
            db.Database.EnsureCreated();
            app.Logger.LogInformation("Database initialized");
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("DB init attempt {Attempt} failed: {Error}", retry + 1, ex.Message);
            if (retry == 9) throw;
            Thread.Sleep(2000);
        }
    }
}

app.Run();
