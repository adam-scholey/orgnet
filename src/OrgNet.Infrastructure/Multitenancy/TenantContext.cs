using OrgNet.Shared.Interfaces;

namespace OrgNet.Infrastructure.Multitenancy;

/// <summary>
/// Scoped service holding the resolved TenantId for the current request.
/// Set by TenantMiddleware early in the pipeline; consumed by OrgNetDbContext
/// global query filters and any service that needs tenant isolation.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }
    public bool IsResolved { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
        IsResolved = true;
    }
}
