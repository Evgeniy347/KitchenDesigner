using Microsoft.AspNetCore.Antiforgery;
using KitchenServer.Web.Services;

namespace KitchenServer.Web.Endpoints;

public static class ImpersonationEndpoints
{
    public static void MapImpersonationEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/impersonate/{userId}", async (
            string userId,
            HttpContext ctx,
            IAntiforgery antiforgery,
            ImpersonationService impersonation) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Invalid antiforgery token");
            }

            var ok = await impersonation.StartImpersonation(userId);
            return ok ? Results.Redirect("/") : Results.Forbid();
        });

        app.MapPost("/admin/stop-impersonation", async (
            HttpContext ctx,
            IAntiforgery antiforgery,
            ImpersonationService impersonation) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Invalid antiforgery token");
            }

            await impersonation.StopImpersonation();
            return Results.Redirect("/");
        });
    }
}
