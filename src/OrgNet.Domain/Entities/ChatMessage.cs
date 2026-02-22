namespace OrgNet.Domain.Entities;

/// <summary>
/// Real-time chat message within a tenant channel.
/// Channels are scoped per-tenant. Messages broadcast via SignalR.
/// </summary>
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SenderId { get; set; }
    public string Channel { get; set; } = "general";
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
