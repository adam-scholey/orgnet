namespace OrgNet.Domain.Entities;

/// <summary>
/// Calendar appointment with double-booking prevention via optimistic concurrency.
/// Times stored in UTC. RowVersion enables conflict detection on concurrent updates.
/// </summary>
public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    /// <summary>PostgreSQL xmin system column — mapped by EF Core for optimistic concurrency</summary>
    public uint xmin { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? AssignedTo { get; set; }
}
