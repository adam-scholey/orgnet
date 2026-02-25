using OrgNet.Shared.Constants;

namespace OrgNet.Tests;

/// <summary>
/// Regression tests for shared constants.
/// Ensures claim type names and other constants don't accidentally change.
/// </summary>
public class ConstantsTests
{
    [Fact]
    public void ClaimTypes_UserId_IsSub()
    {
        Assert.Equal("sub", OrgNetConstants.ClaimTypes.UserId);
    }

    [Fact]
    public void ClaimTypes_TenantId_IsTenantId()
    {
        Assert.Equal("tenant_id", OrgNetConstants.ClaimTypes.TenantId);
    }

    [Fact]
    public void ClaimTypes_Role_IsRole()
    {
        Assert.Equal("role", OrgNetConstants.ClaimTypes.Role);
    }
}
