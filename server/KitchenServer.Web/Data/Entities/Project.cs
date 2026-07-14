using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace KitchenServer.Web.Data.Entities;

public class Project
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable identifier shared across all versions of the same project.</summary>
    public Guid ProjectGroupId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [Required, MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string JsonData { get; set; } = string.Empty;

    /// <summary>Monotonically incremented version number. First version is 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>True for the current (newest) version of the project.</summary>
    public bool IsLatest { get; set; } = true;

    /// <summary>Soft-delete flag. Deleted projects are hidden but never physically removed.</summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }

    public bool IsArchived { get; set; }

    /// <summary>True for the built-in example project that gets auto-seeded for new users.</summary>
    public bool IsExample { get; set; }

    /// <summary>
    /// Random GUID set when a browser tab acquires the project lock.
    /// Only the tab holding this GUID may save the project.
    /// </summary>
    [MaxLength(64)]
    public string? LockGuid { get; set; }

    public DateTime? LockAcquiredAt { get; set; }
}
