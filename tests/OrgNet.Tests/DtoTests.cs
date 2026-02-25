using OrgNet.Shared.DTOs;

namespace OrgNet.Tests;

/// <summary>
/// Regression tests for DTO record types.
/// Ensures record constructors, default values, and equality work correctly.
/// </summary>
public class DtoTests
{
    // ── AuthResponse ──

    [Fact]
    public void AuthResponse_SuccessfulLogin_HasExpectedValues()
    {
        var tenantId = Guid.NewGuid();
        var resp = new AuthResponse(true, "access-token", "refresh-token", "Alice", "Owner", tenantId);

        Assert.True(resp.Success);
        Assert.Equal("access-token", resp.AccessToken);
        Assert.Equal("refresh-token", resp.RefreshToken);
        Assert.Equal("Alice", resp.DisplayName);
        Assert.Equal("Owner", resp.Role);
        Assert.Equal(tenantId, resp.TenantId);
        Assert.Null(resp.Error);
    }

    [Fact]
    public void AuthResponse_FailedLogin_HasError()
    {
        var resp = new AuthResponse(false, "", "", "", "", Guid.Empty, "Invalid credentials");

        Assert.False(resp.Success);
        Assert.Equal("Invalid credentials", resp.Error);
    }

    // ── RegisterOrganisationRequest ──

    [Fact]
    public void RegisterOrganisationRequest_DefaultSector_IsOther()
    {
        var req = new RegisterOrganisationRequest("Acme", "Admin", "admin@acme.com", "Pass123!");
        Assert.Equal("Other", req.Sector);
    }

    [Fact]
    public void RegisterOrganisationRequest_WithSector_StoresCorrectly()
    {
        var req = new RegisterOrganisationRequest("Acme", "Admin", "admin@acme.com", "Pass123!", "Technology");
        Assert.Equal("Technology", req.Sector);
    }

    // ── TenantInfoDto ──

    [Fact]
    public void TenantInfoDto_IncludesSector()
    {
        var dto = new TenantInfoDto(Guid.NewGuid(), "Test Org", "test-org", "Free", "Technology", 5, DateTime.UtcNow);
        Assert.Equal("Technology", dto.Sector);
        Assert.Equal("Free", dto.Plan);
        Assert.Equal(5, dto.MemberCount);
    }

    // ── UpdateTenantRequest ──

    [Fact]
    public void UpdateTenantRequest_IncludesSector()
    {
        var req = new UpdateTenantRequest("New Name", "Business", "Healthcare");
        Assert.Equal("Healthcare", req.Sector);
    }

    [Fact]
    public void UpdateTenantRequest_AllNullable()
    {
        var req = new UpdateTenantRequest(null, null, null);
        Assert.Null(req.Name);
        Assert.Null(req.Plan);
        Assert.Null(req.Sector);
    }

    // ── LoginRequest ──

    [Fact]
    public void LoginRequest_DeviceFingerprint_DefaultsToNull()
    {
        var req = new LoginRequest("user@test.com", "password");
        Assert.Null(req.DeviceFingerprint);
    }

    // ── InviteUserRequest ──

    [Fact]
    public void InviteUserRequest_HasEmailAndRole()
    {
        var req = new InviteUserRequest("bob@test.com", "Admin");
        Assert.Equal("bob@test.com", req.Email);
        Assert.Equal("Admin", req.Role);
    }

    // ── AcceptInviteRequest ──

    [Fact]
    public void AcceptInviteRequest_HasAllFields()
    {
        var req = new AcceptInviteRequest("token123", "Bob", "SecurePass1!");
        Assert.Equal("token123", req.Token);
        Assert.Equal("Bob", req.DisplayName);
        Assert.Equal("SecurePass1!", req.Password);
    }

    // ── DeviceDto ──

    [Fact]
    public void DeviceDto_DefaultRiskScore_IsZero()
    {
        var dto = new DeviceDto(Guid.NewGuid(), "Laptop", "Windows", "Trusted", DateTime.UtcNow, null);
        Assert.Equal(0, dto.RiskScore);
        Assert.Null(dto.LastIpAddress);
    }

    // ── ToggleModuleRequest ──

    [Fact]
    public void ToggleModuleRequest_EnableDisable()
    {
        var enable = new ToggleModuleRequest("chat", true);
        var disable = new ToggleModuleRequest("chat", false);

        Assert.True(enable.Enabled);
        Assert.False(disable.Enabled);
        Assert.Equal("chat", enable.ModuleId);
    }

    // ── Record Equality ──

    [Fact]
    public void AuthResponse_RecordEquality_Works()
    {
        var id = Guid.NewGuid();
        var a = new AuthResponse(true, "t", "r", "User", "Member", id);
        var b = new AuthResponse(true, "t", "r", "User", "Member", id);
        Assert.Equal(a, b);
    }

    [Fact]
    public void LoginRequest_RecordEquality_Works()
    {
        var a = new LoginRequest("test@test.com", "pass");
        var b = new LoginRequest("test@test.com", "pass");
        Assert.Equal(a, b);
    }
}
