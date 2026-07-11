using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace KitchenServer.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // Logout is a state change: POST with antiforgery, never GET.
        app.MapPost("/auth/logout/perform", async (
            HttpContext ctx,
            IAntiforgery antiforgery,
            SignInManager<IdentityUser> signInManager) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Invalid antiforgery token");
            }

            await signInManager.SignOutAsync();
            return Results.Redirect("/auth/login");
        });
    }
}
