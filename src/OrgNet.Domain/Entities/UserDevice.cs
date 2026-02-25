using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Registered device for a user. Devices must be trusted before they can access tenant data.
/// Trust validation uses a fingerprint (hardware ID + OS info hash).
/// </summary>
public class UserDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DeviceTrustLevel TrustLevel { get; set; } = DeviceTrustLevel.Pending;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? TrustedAt { get; set; }
    public string? LastIpAddress { get; set; }
    public int RiskScore { get; set; } = 0;
    public string? RevokedReason { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
