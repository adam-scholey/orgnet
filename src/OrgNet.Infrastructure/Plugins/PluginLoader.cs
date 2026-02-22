using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Infrastructure.Plugins;

/// <summary>
/// Discovers and loads IOrgNetPlugin implementations from DLL assemblies at runtime.
/// 
/// Architectural decisions:
/// - Each plugin is loaded into its own AssemblyLoadContext for isolation.
/// - Plugins are discovered from a configurable directory (default: ./plugins).
/// - The loader scans for types implementing IOrgNetPlugin and instantiates them.
/// - Plugin services are registered into the host DI container during startup.
/// - Module activation is checked per-tenant at runtime, not at load time.
/// </summary>
public class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<IOrgNetPlugin> _loadedPlugins = new();

    public IReadOnlyList<IOrgNetPlugin> LoadedPlugins => _loadedPlugins.AsReadOnly();

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scan a directory for plugin DLLs and load all IOrgNetPlugin implementations.
    /// </summary>
    public void DiscoverPlugins(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger.LogWarning("Plugin directory '{Dir}' does not exist — skipping plugin discovery", pluginDirectory);
            return;
        }

        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
        _logger.LogInformation("Scanning {Count} DLLs in '{Dir}' for plugins", dllFiles.Length, pluginDirectory);

        foreach (var dll in dllFiles)
        {
            try
            {
                var loadContext = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dll), isCollectible: true);
                var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(dll));

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IOrgNetPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                foreach (var pluginType in pluginTypes)
                {
                    if (Activator.CreateInstance(pluginType) is IOrgNetPlugin plugin)
                    {
                        _loadedPlugins.Add(plugin);
                        _logger.LogInformation("Loaded plugin: {Name} v{Version} ({ModuleId})",
                            plugin.Name, plugin.Version, plugin.ModuleId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin from '{Dll}'", dll);
            }
        }
    }

    /// <summary>
    /// Register all discovered plugin services into the DI container.
    /// Called during application startup before the host is built.
    /// </summary>
    public void RegisterPluginServices(IServiceCollection services)
    {
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                plugin.ConfigureServices(services);
                _logger.LogInformation("Registered services for plugin: {Name}", plugin.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register services for plugin: {Name}", plugin.Name);
            }
        }
    }

    /// <summary>
    /// Initialise all loaded plugins (run migrations, seed data, etc.).
    /// Called after the DI container is built.
    /// </summary>
    public async Task InitialisePluginsAsync(IServiceProvider serviceProvider)
    {
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                await plugin.InitialiseAsync(serviceProvider);
                _logger.LogInformation("Initialised plugin: {Name}", plugin.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialise plugin: {Name}", plugin.Name);
            }
        }
    }
}
