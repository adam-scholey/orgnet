using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.Enums;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Infrastructure.Services;

/// <summary>
/// Append-only audit trail. Every significant action is logged with tenant isolation.
/// </summary>
public class AuditService
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AuditService(OrgNetDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task LogAsync(Guid? userId, string username, AuditAction action,
        string entityType, string? entityId = null, string? details = null, string? ipAddress = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenantContext.TenantId,
            UserId = userId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        });
        await _db.SaveChangesAsync();
    }
}
