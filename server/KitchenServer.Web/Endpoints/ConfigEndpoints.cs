using KitchenServer.Web.Services;
using Microsoft.Extensions.Options;

namespace KitchenServer.Web.Endpoints;

public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config", (IOptions<ServerSaveOptions> opts) =>
        {
            return Results.Ok(new
            {
                serverSaveEnabled = opts.Value.Enabled
            });
        }).AllowAnonymous();
    }
}
