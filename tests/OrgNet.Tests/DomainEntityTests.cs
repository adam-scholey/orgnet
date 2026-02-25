using OrgNet.Domain.Entities;
using OrgNet.Shared.Enums;

namespace OrgNet.Tests;

/// <summary>
/// Regression tests for domain entity defaults and relationships.
/// Ensures entities initialise correctly and maintain expected state.
/// </summary>
public class DomainEntityTests
{
    // ── Tenant ──

    [Fact]
    public void Tenant_DefaultId_IsNotEmpty()
    {
        var tenant = new Tenant();
        Assert.NotEqual(Guid.Empty, tenant.Id);
    }

    [Fact]
    public void Tenant_DefaultPlan_IsFree()
    {
        var tenant = new Tenant();
        Assert.Equal(TenantPlan.Free, tenant.Plan);
    }

    [Fact]
    public void Tenant_DefaultSector_IsOther()
    {
        var tenant = new Tenant();
        Assert.Equal(OrganisationSector.Other, tenant.Sector);
    }

    [Fact]
    public void Tenant_DefaultIsActive_IsTrue()
    {
        var tenant = new Tenant();
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void Tenant_CreatedAt_IsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var tenant = new Tenant();
        Assert.True(tenant.CreatedAt >= before);
        Assert.True(tenant.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Tenant_NavigationCollections_InitialiseEmpty()
    {
        var tenant = new Tenant();
        Assert.NotNull(tenant.Users);
        Assert.Empty(tenant.Users);
        Assert.NotNull(tenant.Modules);
        Assert.Empty(tenant.Modules);
        Assert.NotNull(tenant.Services);
        Assert.Empty(tenant.Services);
    }

    // ── User ──

    [Fact]
    public void User_DefaultId_IsNotEmpty()
    {
        var user = new User();
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void User_DefaultRole_IsMember()
    {
        var user = new User();
        Assert.Equal(UserRole.Member, user.Role);
    }

    [Fact]
    public void User_CreatedAt_IsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var user = new User();
        Assert.True(user.CreatedAt >= before);
    }

    // ── TenantModule ──

    [Fact]
    public void TenantModule_DefaultStatus_IsDisabled()
    {
        var module = new TenantModule();
        Assert.Equal(ModuleStatus.Disabled, module.Status);
    }

    [Fact]
    public void TenantModule_DefaultId_IsNotEmpty()
    {
        var module = new TenantModule();
        Assert.NotEqual(Guid.Empty, module.Id);
    }

    [Fact]
    public void TenantModule_DeactivatedAt_IsNullByDefault()
    {
        var module = new TenantModule();
        Assert.Null(module.DeactivatedAt);
    }

    // ── UserDevice ──

    [Fact]
    public void UserDevice_DefaultTrustLevel_IsPending()
    {
        var device = new UserDevice();
        Assert.Equal(DeviceTrustLevel.Pending, device.TrustLevel);
    }

    [Fact]
    public void UserDevice_DefaultId_IsNotEmpty()
    {
        var device = new UserDevice();
        Assert.NotEqual(Guid.Empty, device.Id);
    }

    // ── RefreshToken ──

    [Fact]
    public void RefreshToken_DefaultIsRevoked_IsFalse()
    {
        var rt = new RefreshToken();
        Assert.False(rt.IsRevoked);
    }

    [Fact]
    public void RefreshToken_IsExpired_WhenPastExpiresAt()
    {
        var rt = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        Assert.True(rt.IsExpired);
    }

    [Fact]
    public void RefreshToken_IsNotExpired_WhenFuture()
    {
        var rt = new RefreshToken { ExpiresAt = DateTime.UtcNow.AddDays(7) };
        Assert.False(rt.IsExpired);
    }

    // ── AuditLog ──

    [Fact]
    public void AuditLog_DefaultId_IsNotEmpty()
    {
        var log = new AuditLog();
        Assert.NotEqual(Guid.Empty, log.Id);
    }

    [Fact]
    public void AuditLog_Timestamp_IsRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var log = new AuditLog();
        Assert.True(log.Timestamp >= before);
    }
}
