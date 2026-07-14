using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using KitchenServer.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace KitchenServer.Web.Endpoints;

/// <summary>
/// Shared logic for acquiring/releasing project locks.
/// Used by both the REST endpoint and the Blazor Editor page.
/// </summary>
public static class ProjectLockHelper
{
    /// <summary>
    /// Acquires the project lock. If the project was locked by another tab,
    /// sends a "lock_taken" notification via WebSocket and takes over.
    /// Returns the new lockGuid on success.
    /// </summary>
    public static async Task<string?> AcquireLockAsync(
        AppDbContext db,
        McpSessionManager sessions,
        Project project)
    {
        var newLockGuid = Guid.NewGuid().ToString("N");

        // Notify the previous lock holder if any.
        var previousLockGuid = project.LockGuid;
        if (!string.IsNullOrEmpty(previousLockGuid))
        {
            var previousSession = sessions.GetSessionByProjectId(project.Id.ToString());
            if (previousSession != null)
            {
                // Mark the old session as lock-lost so the Blazor UI shows a warning.
                previousSession.LockLost = true;
                sessions.RaiseSessionStateChanged(previousSession);

                if (previousSession.BrowserWebSocket is { State: WebSocketState.Open })
                {
                    try
                    {
                        var notification = JsonSerializer.Serialize(new { type = "lock_taken", newLockGuid });
                        var data = Encoding.UTF8.GetBytes(notification);
                        await previousSession.BrowserWebSocket.SendAsync(
                            new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { /* best-effort notification */ }
                }
            }
        }

        project.LockGuid = newLockGuid;
        project.LockAcquiredAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return newLockGuid;
    }

    /// <summary>
    /// Releases the project lock if the caller owns it.
    /// </summary>
    public static async Task<bool> ReleaseLockAsync(
        AppDbContext db,
        Project project,
        string lockGuid)
    {
        if (project.LockGuid != lockGuid)
            return false;

        project.LockGuid = null;
        project.LockAcquiredAt = null;
        await db.SaveChangesAsync();
        return true;
    }
}
