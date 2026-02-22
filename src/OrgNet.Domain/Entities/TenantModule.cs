using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Tracks which modules/plugins are activated for a given tenant.
/// Admins toggle modules on/off; the plugin loader checks this at startup.
/// </summary>
public class TenantModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ModuleId { get; set; } = string.Empty;
    public ModuleStatus Status { get; set; } = ModuleStatus.Disabled;
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }
    public string? Configuration { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}
