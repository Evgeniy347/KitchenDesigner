using Microsoft.AspNetCore.Identity;

namespace KitchenServer.Web.Services;

public class ImpersonationService
{
    private const string ImpersonatorCookie = "impersonator_id";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly AdminEmailsService _adminEmails;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ImpersonationService(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        AdminEmailsService adminEmails,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _adminEmails = adminEmails;
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetImpersonatorId()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        return ctx.Request.Cookies[ImpersonatorCookie];
    }

    public bool IsImpersonating => GetImpersonatorId() != null;

    public async Task<bool> StartImpersonation(string targetUserId)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx?.User?.Identity == null || !ctx.User.Identity.IsAuthenticated)
            return false;

        var currentEmail = ctx.User.Identity.Name;
        if (!_adminEmails.IsAdmin(currentEmail))
            return false;

        var currentUserId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
            return false;

        var targetUser = await _userManager.FindByIdAsync(targetUserId);
        if (targetUser == null)
            return false;

        await _signInManager.SignInAsync(targetUser, isPersistent: false);

        ctx.Response.Cookies.Append(
            ImpersonatorCookie, currentUserId,
            new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });

        return true;
    }

    public async Task StopImpersonation()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx == null) return;

        var impersonatorId = ctx.Request.Cookies[ImpersonatorCookie];
        if (string.IsNullOrEmpty(impersonatorId))
            return;

        ctx.Response.Cookies.Delete(ImpersonatorCookie);

        var impersonator = await _userManager.FindByIdAsync(impersonatorId);
        if (impersonator != null)
            await _signInManager.SignInAsync(impersonator, isPersistent: false);
    }
}
