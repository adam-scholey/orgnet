using OrgNet.Shared.Enums;

namespace OrgNet.Tests;

/// <summary>
/// Regression tests for all shared enums.
/// Ensures enum values, counts, and parsing remain stable across refactors.
/// </summary>
public class EnumTests
{
    [Fact]
    public void UserRole_HasExpectedValues()
    {
        Assert.Equal(0, (int)UserRole.Member);
        Assert.Equal(1, (int)UserRole.Moderator);
        Assert.Equal(2, (int)UserRole.Admin);
        Assert.Equal(3, (int)UserRole.Owner);
    }

    [Fact]
    public void UserRole_HasExactly4Values()
    {
        Assert.Equal(4, Enum.GetValues<UserRole>().Length);
    }

    [Fact]
    public void TenantPlan_HasExpectedValues()
    {
        Assert.Equal(0, (int)TenantPlan.Free);
        Assert.Equal(1, (int)TenantPlan.Starter);
        Assert.Equal(2, (int)TenantPlan.Business);
        Assert.Equal(3, (int)TenantPlan.Enterprise);
    }

    [Fact]
    public void DeviceTrustLevel_HasExpectedValues()
    {
        Assert.Equal(0, (int)DeviceTrustLevel.Untrusted);
        Assert.Equal(1, (int)DeviceTrustLevel.Pending);
        Assert.Equal(2, (int)DeviceTrustLevel.Trusted);
        Assert.Equal(3, (int)DeviceTrustLevel.Revoked);
    }

    [Fact]
    public void ModuleStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)ModuleStatus.Disabled);
        Assert.Equal(1, (int)ModuleStatus.Enabled);
        Assert.Equal(2, (int)ModuleStatus.Licensed);
    }

    [Fact]
    public void TaskItemStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)TaskItemStatus.Todo);
        Assert.Equal(1, (int)TaskItemStatus.InProgress);
        Assert.Equal(2, (int)TaskItemStatus.Review);
        Assert.Equal(3, (int)TaskItemStatus.Done);
    }

    [Fact]
    public void TaskItemPriority_HasExpectedValues()
    {
        Assert.Equal(0, (int)TaskItemPriority.Low);
        Assert.Equal(1, (int)TaskItemPriority.Medium);
        Assert.Equal(2, (int)TaskItemPriority.High);
        Assert.Equal(3, (int)TaskItemPriority.Critical);
    }

    [Fact]
    public void OrganisationSector_HasExpectedValues()
    {
        Assert.Equal(0, (int)OrganisationSector.Technology);
        Assert.Equal(1, (int)OrganisationSector.Healthcare);
        Assert.Equal(2, (int)OrganisationSector.Education);
        Assert.Equal(3, (int)OrganisationSector.Finance);
        Assert.Equal(4, (int)OrganisationSector.Manufacturing);
        Assert.Equal(5, (int)OrganisationSector.Retail);
        Assert.Equal(6, (int)OrganisationSector.Legal);
        Assert.Equal(7, (int)OrganisationSector.Construction);
        Assert.Equal(8, (int)OrganisationSector.Hospitality);
        Assert.Equal(9, (int)OrganisationSector.NonProfit);
        Assert.Equal(10, (int)OrganisationSector.Government);
        Assert.Equal(99, (int)OrganisationSector.Other);
    }

    [Fact]
    public void OrganisationSector_HasExactly12Values()
    {
        Assert.Equal(12, Enum.GetValues<OrganisationSector>().Length);
    }

    [Theory]
    [InlineData("Technology", OrganisationSector.Technology)]
    [InlineData("Healthcare", OrganisationSector.Healthcare)]
    [InlineData("Other", OrganisationSector.Other)]
    [InlineData("technology", OrganisationSector.Technology)]
    public void OrganisationSector_ParsesFromString(string input, OrganisationSector expected)
    {
        Assert.True(Enum.TryParse<OrganisationSector>(input, true, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void OrganisationSector_InvalidString_FailsToParse()
    {
        Assert.False(Enum.TryParse<OrganisationSector>("InvalidSector", out _));
    }

    [Fact]
    public void AuditAction_HasAllExpectedValues()
    {
        var values = Enum.GetValues<AuditAction>();
        Assert.Contains(AuditAction.Login, values);
        Assert.Contains(AuditAction.Logout, values);
        Assert.Contains(AuditAction.Create, values);
        Assert.Contains(AuditAction.Update, values);
        Assert.Contains(AuditAction.Delete, values);
        Assert.Contains(AuditAction.RoleChange, values);
        Assert.Contains(AuditAction.ModuleToggle, values);
        Assert.Contains(AuditAction.DeviceRegistered, values);
        Assert.Contains(AuditAction.DeviceRevoked, values);
        Assert.Contains(AuditAction.TokenRefreshed, values);
    }
}
