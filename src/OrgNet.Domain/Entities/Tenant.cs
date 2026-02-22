using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Root aggregate for multi-tenancy. Every organisation is a Tenant.
/// All tenant-scoped entities carry a TenantId FK filtered by EF global query filters.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<TenantModule> Modules { get; set; } = new List<TenantModule>();
    public ICollection<ServiceRegistration> Services { get; set; } = new List<ServiceRegistration>();
}
