using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Represents a pending invitation for a user to join a tenant.
/// Contains a secure token that is included in the invite link.
/// Expires after 7 days. Once accepted, the user record is created.
/// </summary>
public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public string Token { get; set; } = string.Empty;
    public Guid InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? AcceptedAt { get; set; }
    public bool IsAccepted => AcceptedAt.HasValue;
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User InvitedBy { get; set; } = null!;
}
