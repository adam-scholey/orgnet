namespace OrgNet.Domain.Entities;

/// <summary>
/// Per-tenant service registry. Each tenant can register internal/external service endpoints.
/// Used by the dynamic app launcher to discover available services.
/// </summary>
public class ServiceRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public bool IsHealthy { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastHealthCheckAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}
