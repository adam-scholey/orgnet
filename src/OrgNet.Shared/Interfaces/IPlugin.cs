using Microsoft.Extensions.DependencyInjection;

namespace OrgNet.Shared.Interfaces;

/// <summary>
/// Contract every OrgNet plugin must implement.
/// Plugins are discovered at runtime via reflection and loaded dynamically.
/// </summary>
public interface IOrgNetPlugin
{
    /// <summary>Unique module identifier, e.g. "OrgNet.Chat"</summary>
    string ModuleId { get; }

    /// <summary>Human-readable display name</summary>
    string Name { get; }

    /// <summary>Short description shown in the module marketplace</summary>
    string Description { get; }

    /// <summary>SemVer version string</summary>
    string Version { get; }

    /// <summary>Optional icon URL or embedded resource path</summary>
    string? IconUrl { get; }

    /// <summary>Register plugin services into the host DI container</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>Called once after DI is built — run migrations, seed data, etc.</summary>
    Task InitialiseAsync(IServiceProvider serviceProvider);
}
