namespace OrgNet.Shared.Interfaces;

/// <summary>
/// Provides the current tenant context resolved from the incoming request.
/// Injected as Scoped so each request gets its own tenant resolution.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool IsResolved { get; }
    void SetTenant(Guid tenantId);
}
