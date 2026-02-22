namespace OrgNet.Domain.Entities;

/// <summary>
/// Organisation-wide announcement posted by admins/owners.
/// Broadcast to all tenant members via SignalR.
/// </summary>
public class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User Author { get; set; } = null!;
}
