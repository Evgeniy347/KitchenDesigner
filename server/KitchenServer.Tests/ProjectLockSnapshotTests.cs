using System.Text.Json;
using KitchenServer.Web.Data.Entities;

namespace KitchenServer.Tests;

/// <summary>
/// Snapshot-style tests verifying the Project entity's lock fields
/// serialize correctly to JSON (shape stability).
/// </summary>
public class ProjectLockSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Project_Default_LockFieldsOmittedWhenNull()
    {
        var project = new Project
        {
            Id = Guid.Parse("2d2d7781-aeda-4985-8d65-4c28e9db9165"),
            UserId = "user1",
            Name = "My Kitchen"
        };

        var json = JsonSerializer.Serialize(project, JsonOpts);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // When null, WithIgnoreCondition=WhenWritingNull omits the properties entirely.
        Assert.False(root.TryGetProperty("lockGuid", out _));
        Assert.False(root.TryGetProperty("lockAcquiredAt", out _));
    }

    [Fact]
    public void Project_Locked_HasGuidAndTimestamp()
    {
        var lockTime = new DateTime(2026, 7, 12, 15, 30, 0, DateTimeKind.Utc);
        var project = new Project
        {
            Id = Guid.Parse("2d2d7781-aeda-4985-8d65-4c28e9db9165"),
            UserId = "user1",
            Name = "My Kitchen",
            LockGuid = "a1b2c3d4e5f6",
            LockAcquiredAt = lockTime
        };

        var json = JsonSerializer.Serialize(project, JsonOpts);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("a1b2c3d4e5f6", root.GetProperty("lockGuid").GetString());
        Assert.Equal(lockTime, root.GetProperty("lockAcquiredAt").GetDateTime());
    }

    [Fact]
    public void Project_LockFields_RoundTrip()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            UserId = "user1",
            Name = "Kitchen",
            LockGuid = "abc123",
            LockAcquiredAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(project, JsonOpts);
        var deserialized = JsonSerializer.Deserialize<Project>(json, JsonOpts)!;

        Assert.Equal(project.LockGuid, deserialized.LockGuid);
        Assert.Equal(project.LockAcquiredAt, deserialized.LockAcquiredAt);
    }

    [Fact]
    public void Project_Snapshot_DefaultProject()
    {
        var project = new Project
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            UserId = "user1",
            Name = "Snapshot Kitchen",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(project, JsonOpts).Replace("\r\n", "\n");

        var expected = """
        {
          "id": "00000000-0000-0000-0000-000000000001",
          "projectGroupId": "00000000-0000-0000-0000-000000000000",
          "userId": "user1",
          "name": "Snapshot Kitchen",
          "jsonData": "",
          "version": 1,
          "isLatest": true,
          "isDeleted": false,
          "createdAt": "2026-01-01T00:00:00Z",
          "updatedAt": "2026-01-01T00:00:00Z",
          "isArchived": false,
          "isExample": false
        }
        """;

        Assert.Equal(expected.Trim(), json);
    }

    [Fact]
    public void Project_Snapshot_LockedProject()
    {
        var project = new Project
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            UserId = "user1",
            Name = "Snapshot Kitchen",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LockGuid = "a1b2c3d4e5f6g7h8",
            LockAcquiredAt = new DateTime(2026, 7, 12, 15, 30, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(project, JsonOpts).Replace("\r\n", "\n");

        var expected2 = """
        {
          "id": "00000000-0000-0000-0000-000000000001",
          "projectGroupId": "00000000-0000-0000-0000-000000000000",
          "userId": "user1",
          "name": "Snapshot Kitchen",
          "jsonData": "",
          "version": 1,
          "isLatest": true,
          "isDeleted": false,
          "createdAt": "2026-01-01T00:00:00Z",
          "updatedAt": "2026-01-01T00:00:00Z",
          "isArchived": false,
          "isExample": false,
          "lockGuid": "a1b2c3d4e5f6g7h8",
          "lockAcquiredAt": "2026-07-12T15:30:00Z"
        }
        """;

        Assert.Equal(expected2.Trim(), json);
    }

    [Fact]
    public void Project_Snapshot_LockTakenMessage()
    {
        // Verify the WebSocket notification payload shape.
        var notification = new { type = "lock_taken", newLockGuid = "abc123" };
        var json = JsonSerializer.Serialize(notification, JsonOpts).Replace("\r\n", "\n");

        var expected = """
        {
          "type": "lock_taken",
          "newLockGuid": "abc123"
        }
        """;

        Assert.Equal(expected.Trim(), json);
    }
}
