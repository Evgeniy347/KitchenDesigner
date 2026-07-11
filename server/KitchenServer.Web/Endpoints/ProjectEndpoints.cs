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
                .Where(p => p.UserId == userId && !p.IsArchived)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new { p.Id, p.Name, p.CreatedAt, p.UpdatedAt })
                .ToListAsync();

            return Results.Ok(projects);
        });

        group.MapPost("/", async (CreateProjectRequest req, AppDbContext db, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var project = new Project
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = req.Name,
                JsonData = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Projects.Add(project);
            await db.SaveChangesAsync();

            return Results.Created($"/api/projects/{project.Id}",
                new { project.Id, project.Name, project.CreatedAt, project.UpdatedAt });
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

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (project is null)
                return Results.NotFound();

            var json = await storage.Load(id, userId) ?? project.JsonData;

            return Results.Ok(new
            {
                project.Id,
                project.Name,
                project.CreatedAt,
                project.UpdatedAt,
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

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (project is null)
                return Results.NotFound();

            if (req.Name is not null)
                project.Name = req.Name;

            if (req.JsonData is not null)
            {
                project.JsonData = req.JsonData;
                await storage.Save(id, userId, req.JsonData);
            }

            project.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { project.Id, project.Name, project.UpdatedAt });
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ProjectStorageService storage,
            ClaimsPrincipal user) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null)
                return Results.Unauthorized();

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (project is null)
                return Results.NotFound();

            db.Projects.Remove(project);
            await db.SaveChangesAsync();
            storage.Delete(id, userId);

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

            var original = await db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (original is null)
                return Results.NotFound();

            var json = await storage.Load(id, userId) ?? original.JsonData;

            var duplicate = new Project
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = $"{original.Name} (Copy)",
                JsonData = json,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Projects.Add(duplicate);
            await storage.Save(duplicate.Id, userId, json);
            await db.SaveChangesAsync();

            return Results.Created($"/api/projects/{duplicate.Id}",
                new { duplicate.Id, duplicate.Name, duplicate.CreatedAt });
        });
    }
}

public record CreateProjectRequest(string Name);
public record UpdateProjectRequest(string? Name, string? JsonData);
