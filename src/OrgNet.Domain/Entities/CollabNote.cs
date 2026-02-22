namespace OrgNet.Domain.Entities;

/// <summary>
/// Shared collaborative note/document within the organisation.
/// Supports versioning via LastEditedAt and editor tracking.
/// </summary>
public class CollabNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? LastEditedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastEditedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
