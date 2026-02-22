using Microsoft.EntityFrameworkCore;
using OrgNet.Domain.Entities;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Infrastructure.Data;

/// <summary>
/// Multi-tenant DbContext. Global query filters on every tenant-scoped entity
/// ensure row-level isolation — queries automatically include WHERE TenantId = @current.
/// 
/// Architectural decision: Row-level tenancy in a single database.
/// Pros: Simpler ops, easier cross-tenant reporting for platform admins.
/// Cons: Must be rigorous about filters. EF global filters handle this automatically.
/// </summary>
public class OrgNetDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public OrgNetDbContext(DbContextOptions<OrgNetDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();
    public DbSet<ServiceRegistration> ServiceRegistrations => Set<ServiceRegistration>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Tenant (no global filter — tenants are top-level) ──
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        });

        // ── User (tenant-scoped) ──
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(150).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(u => u.TenantId == _tenantContext.TenantId);
        });

        // ── RefreshToken (tenant-scoped) ──
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Token).IsUnique();
            e.Property(r => r.Token).HasMaxLength(512).IsRequired();
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
        });

        // ── UserDevice (tenant-scoped) ──
        modelBuilder.Entity<UserDevice>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.UserId, d.Fingerprint }).IsUnique();
            e.Property(d => d.DeviceName).HasMaxLength(200).IsRequired();
            e.Property(d => d.Fingerprint).HasMaxLength(512).IsRequired();
            e.Property(d => d.Platform).HasMaxLength(100).IsRequired();
            e.HasOne(d => d.User).WithMany(u => u.Devices).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(d => d.TenantId == _tenantContext.TenantId);
        });

        // ── TenantModule (tenant-scoped) ──
        modelBuilder.Entity<TenantModule>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.TenantId, m.ModuleId }).IsUnique();
            e.Property(m => m.ModuleId).HasMaxLength(200).IsRequired();
            e.HasOne(m => m.Tenant).WithMany(t => t.Modules).HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
        });

        // ── ServiceRegistration (tenant-scoped) ──
        modelBuilder.Entity<ServiceRegistration>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.TenantId, s.ServiceId }).IsUnique();
            e.Property(s => s.ServiceId).HasMaxLength(200).IsRequired();
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.Endpoint).HasMaxLength(500).IsRequired();
            e.HasOne(s => s.Tenant).WithMany(t => t.Services).HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);
        });

        // ── AuditLog (tenant-scoped, append-only) ──
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => new { a.TenantId, a.Timestamp });
            e.Property(a => a.Username).HasMaxLength(150);
            e.Property(a => a.EntityType).HasMaxLength(100);
            e.HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);
        });
    }

    /// <summary>
    /// Automatically stamp TenantId on new tenant-scoped entities before saving.
    /// This is a safety net — services should set TenantId explicitly, but this
    /// catches any missed assignments.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_tenantContext.IsResolved)
        {
            foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
            {
                var tenantIdProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
                if (tenantIdProp != null && tenantIdProp.CurrentValue is Guid id && id == Guid.Empty)
                {
                    tenantIdProp.CurrentValue = _tenantContext.TenantId;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
