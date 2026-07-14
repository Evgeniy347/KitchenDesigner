using Microsoft.Extensions.Configuration;

namespace KitchenServer.Web.Services;

public class AdminEmailsService
{
    private const string DefaultAdminEmail = "admin@admin";
    private const string EnvKey = "ADMIN_EMAILS";

    private readonly HashSet<string> _adminEmails;

    public AdminEmailsService(IConfiguration configuration)
    {
        var raw = configuration[EnvKey];
        var emails = string.IsNullOrWhiteSpace(raw)
            ? new[] { DefaultAdminEmail }
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _adminEmails = new HashSet<string>(emails, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(email) && _adminEmails.Contains(email);
}
