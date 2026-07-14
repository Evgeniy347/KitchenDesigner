using System.Security.Claims;
using KitchenServer.Web.Data;
using KitchenServer.Web.Data.Entities;
using KitchenServer.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace KitchenServer.Web.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var projects = await db.Projects
                .Where(p => p.UserId == userId && p.IsLatest && !p.IsDeleted && !p.IsArchived)
                .OrderBy(p => p.IsExample)
                .ThenByDescending(p => p.UpdatedAt)
                .Select(p => new { Id = p.ProjectGroupId, p.Name, p.CreatedAt, p.UpdatedAt, p.Version })
                .ToListAsync();

            return Results.Ok(projects);
        });

        group.MapPost("/", async (CreateProjectRequest req, AppDbContext db, ProjectStorageService storage, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "Name is required" });

            var projectGroupId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var project = new Project
            {
                Id = Guid.NewGuid(),
                ProjectGroupId = projectGroupId,
                UserId = userId,
                Name = req.Name.Trim(),
                JsonData = "{}",
                Version = 1,
                IsLatest = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Projects.Add(project);
            await storage.Save(projectGroupId, userId, "{}");
            await db.SaveChangesAsync();

            return Results.Created($"/api/projects/{project.ProjectGroupId}",
                new { Id = project.ProjectGroupId, project.Name, project.CreatedAt, project.UpdatedAt, project.Version });
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ProjectStorageService storage,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectGroupId == id && p.UserId == userId && p.IsLatest && !p.IsDeleted);
            if (project is null)
                return Results.NotFound();

            var json = await storage.Load(id, userId) ?? project.JsonData;

            return Results.Ok(new
            {
                Id = project.ProjectGroupId,
                project.Name,
                project.CreatedAt,
                project.UpdatedAt,
                project.Version,
                JsonData = json
            });
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProjectRequest req,
            AppDbContext db,
            ProjectStorageService storage,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var current = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectGroupId == id && p.UserId == userId && p.IsLatest && !p.IsDeleted);
            if (current is null)
                return Results.NotFound();

            // Validate project lock if one is held.
            if (!string.IsNullOrEmpty(current.LockGuid) && req.LockGuid != current.LockGuid)
                return Results.Conflict(new { error = "Project is locked by another tab. Open it there or refresh." });

            var now = DateTime.UtcNow;

            if (req.JsonData is not null)
            {
                try
                {
                    await storage.Save(id, userId, req.JsonData);
                }
                catch (ProjectStorageException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }

            var newVersion = new Project
            {
                Id = Guid.NewGuid(),
                ProjectGroupId = current.ProjectGroupId,
                UserId = current.UserId,
                Name = req.Name ?? current.Name,
                JsonData = req.JsonData ?? current.JsonData,
                Version = current.Version + 1,
                IsLatest = true,
                CreatedAt = current.CreatedAt,
                UpdatedAt = now
            };

            current.IsLatest = false;
            current.UpdatedAt = now;

            db.Projects.Add(newVersion);
            await db.SaveChangesAsync();

            return Results.Ok(new { Id = newVersion.ProjectGroupId, newVersion.Name, newVersion.UpdatedAt, newVersion.Version });
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var versions = await db.Projects
                .Where(p => p.ProjectGroupId == id && p.UserId == userId && !p.IsDeleted)
                .ToListAsync();

            if (versions.Count == 0)
                return Results.NotFound();

            foreach (var v in versions)
            {
                v.IsDeleted = true;
                v.IsLatest = false;
                v.DeletedAt = now;
            }

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapPost("/{id:guid}/duplicate", async (
            Guid id,
            AppDbContext db,
            ProjectStorageService storage,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var original = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectGroupId == id && p.UserId == userId && p.IsLatest && !p.IsDeleted);
            if (original is null)
                return Results.NotFound();

            var json = await storage.Load(id, userId) ?? original.JsonData;

            var duplicateGroupId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var duplicate = new Project
            {
                Id = Guid.NewGuid(),
                ProjectGroupId = duplicateGroupId,
                UserId = userId,
                Name = $"{original.Name} (Copy)",
                JsonData = json,
                Version = 1,
                IsLatest = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Projects.Add(duplicate);
            try
            {
                await storage.Save(duplicateGroupId, userId, json);
            }
            catch (ProjectStorageException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            await db.SaveChangesAsync();

            return Results.Created($"/api/projects/{duplicate.ProjectGroupId}",
                new { Id = duplicate.ProjectGroupId, duplicate.Name, duplicate.CreatedAt, duplicate.Version });
        });

        // ── Version history ─────────────────────────────────────────

        group.MapGet("/{id:guid}/versions", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectGroupId == id && p.UserId == userId);
            if (project is null)
                return Results.NotFound();

            var versions = await db.Projects
                .Where(p => p.ProjectGroupId == id && p.UserId == userId)
                .OrderByDescending(p => p.Version)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Version,
                    p.IsLatest,
                    p.IsDeleted,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.DeletedAt
                })
                .ToListAsync();

            return Results.Ok(versions);
        });

        // ── Project lock (tab-level concurrency guard) ──────────────────

        group.MapPost("/{id:guid}/lock", async (
            Guid id,
            AppDbContext db,
            McpSessionManager sessions,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectGroupId == id && p.UserId == userId && p.IsLatest && !p.IsDeleted);
            if (project is null)
                return Results.NotFound();

            var lockGuid = await ProjectLockHelper.AcquireLockAsync(db, sessions, project);
            return Results.Ok(new { lockGuid });
        });

        group.MapDelete("/{id:guid}/lock/{lockGuid}", async (
            Guid id,
            string lockGuid,
            AppDbContext db,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var project = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectGroupId == id && p.UserId == userId && p.IsLatest && !p.IsDeleted);
            if (project is null)
                return Results.NotFound();

            var released = await ProjectLockHelper.ReleaseLockAsync(db, project, lockGuid);
            if (!released)
                return Results.Conflict(new { error = "Lock held by another tab" });

            return Results.Ok(new { ok = true });
        });
    }
}

public record CreateProjectRequest(string Name);
public record UpdateProjectRequest(string? Name, string? JsonData, string? LockGuid);
