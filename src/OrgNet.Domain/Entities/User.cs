using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Tenant-scoped user. Each user belongs to exactly one tenant.
/// Passwords hashed with BCrypt. Refresh tokens stored per-device for rotation.
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Member;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<UserDevice> Devices { get; set; } = new List<UserDevice>();
}
