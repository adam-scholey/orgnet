using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Per-user module access control. Allows admins to grant or deny
/// specific module access to individual users, overriding the tenant-level setting.
/// </summary>
public class UserModuleAccess
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string ModuleId { get; set; } = string.Empty;
    public bool IsGranted { get; set; } = true;
    public Guid GrantedByUserId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public User GrantedBy { get; set; } = null!;
}
