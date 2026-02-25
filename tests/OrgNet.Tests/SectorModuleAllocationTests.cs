using OrgNet.Infrastructure.Auth;
using OrgNet.Shared.Enums;

namespace OrgNet.Tests;

/// <summary>
/// Regression tests for sector-based module auto-allocation.
/// Ensures that when an organisation registers with a specific sector,
/// the correct built-in modules are enabled automatically.
/// </summary>
public class SectorModuleAllocationTests
{
    [Fact]
    public void AllSectors_AlwaysInclude_ChatAndAnnouncements()
    {
        foreach (var sector in Enum.GetValues<OrganisationSector>())
        {
            var modules = AuthService.GetModulesForSector(sector);
            Assert.Contains("chat", modules);
            Assert.Contains("announcements", modules);
        }
    }

    [Theory]
    [InlineData(OrganisationSector.Technology, new[] { "chat", "announcements", "tasks", "files", "notes" })]
    [InlineData(OrganisationSector.Healthcare, new[] { "chat", "announcements", "appointments", "files", "notes" })]
    [InlineData(OrganisationSector.Education, new[] { "chat", "announcements", "notes", "tasks" })]
    [InlineData(OrganisationSector.Finance, new[] { "chat", "announcements", "files", "tasks", "notes" })]
    [InlineData(OrganisationSector.Manufacturing, new[] { "chat", "announcements", "tasks", "files" })]
    [InlineData(OrganisationSector.Retail, new[] { "chat", "announcements", "tasks" })]
    [InlineData(OrganisationSector.Legal, new[] { "chat", "announcements", "files", "notes", "appointments" })]
    [InlineData(OrganisationSector.Construction, new[] { "chat", "announcements", "tasks", "files" })]
    [InlineData(OrganisationSector.Hospitality, new[] { "chat", "announcements", "appointments", "tasks" })]
    [InlineData(OrganisationSector.NonProfit, new[] { "chat", "announcements", "notes", "tasks" })]
    [InlineData(OrganisationSector.Government, new[] { "chat", "announcements", "files", "notes", "tasks", "appointments" })]
    [InlineData(OrganisationSector.Other, new[] { "chat", "announcements", "tasks", "files", "notes", "appointments" })]
    public void Sector_AllocatesCorrectModules(OrganisationSector sector, string[] expectedModules)
    {
        var modules = AuthService.GetModulesForSector(sector);

        foreach (var expected in expectedModules)
        {
            Assert.Contains(expected, modules);
        }
    }

    [Fact]
    public void AllSectors_ReturnNonEmptyList()
    {
        foreach (var sector in Enum.GetValues<OrganisationSector>())
        {
            var modules = AuthService.GetModulesForSector(sector);
            Assert.NotEmpty(modules);
        }
    }

    [Fact]
    public void AllSectors_ReturnNoDuplicates()
    {
        foreach (var sector in Enum.GetValues<OrganisationSector>())
        {
            var modules = AuthService.GetModulesForSector(sector);
            var distinct = modules.Distinct().ToList();
            Assert.True(modules.Count == distinct.Count,
                $"Sector {sector} has duplicate modules: [{string.Join(", ", modules)}]");
        }
    }

    [Fact]
    public void AllModuleIds_AreLowercase()
    {
        foreach (var sector in Enum.GetValues<OrganisationSector>())
        {
            var modules = AuthService.GetModulesForSector(sector);
            foreach (var m in modules)
            {
                Assert.Equal(m, m.ToLowerInvariant());
            }
        }
    }
}
