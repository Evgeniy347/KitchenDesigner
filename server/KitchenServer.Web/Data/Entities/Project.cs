using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace KitchenServer.Web.Data.Entities;

public class Project
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    [Required, MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string JsonData { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }

    /// <summary>
    /// Random GUID set when a browser tab acquires the project lock.
    /// Only the tab holding this GUID may save the project.
    /// </summary>
    [MaxLength(64)]
    public string? LockGuid { get; set; }

    public DateTime? LockAcquiredAt { get; set; }
}
