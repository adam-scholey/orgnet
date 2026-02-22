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
    public DbSet<OrgFile> OrgFiles => Set<OrgFile>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CollabNote> CollabNotes => Set<CollabNote>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

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

        // ── OrgFile (tenant-scoped, encrypted file vault) ──
        modelBuilder.Entity<OrgFile>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FileName).HasMaxLength(255).IsRequired();
            e.Property(f => f.ContentType).HasMaxLength(100);
            e.HasOne(f => f.UploadedBy).WithMany().HasForeignKey(f => f.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Tenant).WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(f => f.TenantId == _tenantContext.TenantId);
        });

        // ── ChatMessage (tenant-scoped, real-time messaging) ──
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.TenantId, m.Channel, m.SentAt });
            e.Property(m => m.Channel).HasMaxLength(100).IsRequired();
            e.Property(m => m.Content).HasMaxLength(4000).IsRequired();
            e.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Tenant).WithMany().HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
        });

        // ── CollabNote (tenant-scoped, shared documents) ──
        modelBuilder.Entity<CollabNote>(e =>
        {
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(300).IsRequired();
            e.HasOne(n => n.CreatedBy).WithMany().HasForeignKey(n => n.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(n => n.Tenant).WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(n => n.TenantId == _tenantContext.TenantId);
        });

        // ── TaskItem (tenant-scoped, kanban board) ──
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(300).IsRequired();
            e.Property(t => t.Description).HasMaxLength(2000);
            e.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(t => t.TenantId == _tenantContext.TenantId);
        });

        // ── Announcement (tenant-scoped, org-wide broadcasts) ──
        modelBuilder.Entity<Announcement>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(300).IsRequired();
            e.Property(a => a.Body).HasMaxLength(5000).IsRequired();
            e.HasOne(a => a.Author).WithMany().HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(a => a.TenantId == _tenantContext.TenantId);
        });

        // ── Invitation (tenant-scoped, pending user invites) ──
        modelBuilder.Entity<Invitation>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.Token).IsUnique();
            e.HasIndex(i => new { i.TenantId, i.Email });
            e.Property(i => i.Email).HasMaxLength(256).IsRequired();
            e.Property(i => i.Token).HasMaxLength(128).IsRequired();
            e.HasOne(i => i.Tenant).WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.InvitedBy).WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(i => i.TenantId == _tenantContext.TenantId);
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
