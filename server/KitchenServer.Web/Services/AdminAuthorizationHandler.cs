using Microsoft.AspNetCore.Authorization;

namespace KitchenServer.Web.Services;

public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
{
    private readonly AdminEmailsService _adminEmails;

    public AdminAuthorizationHandler(AdminEmailsService adminEmails) =>
        _adminEmails = adminEmails;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        if (_adminEmails.IsAdmin(context.User.Identity?.Name))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
