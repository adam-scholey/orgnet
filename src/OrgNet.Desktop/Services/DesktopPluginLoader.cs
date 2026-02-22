using System.Reflection;
using System.Runtime.Loader;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Desktop-side plugin loader. Discovers and loads IOrgNetPlugin implementations
/// from a local plugins directory. Used by the dynamic app launcher to show
/// available modules and their UI entry points.
/// 
/// Architectural decision: Separate from backend PluginLoader because the desktop
/// only needs metadata and UI hooks — it doesn't run server-side plugin services.
/// </summary>
public class DesktopPluginLoader
{
    private readonly List<IOrgNetPlugin> _plugins = new();
    public IReadOnlyList<IOrgNetPlugin> Plugins => _plugins.AsReadOnly();

    public void DiscoverPlugins(string? pluginDirectory = null)
    {
        var dir = pluginDirectory ?? Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(dir)) return;

        foreach (var dll in Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories))
        {
            try
            {
                var context = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dll), isCollectible: true);
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));

                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IOrgNetPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IOrgNetPlugin plugin)
                        _plugins.Add(plugin);
                }
            }
            catch
            {
                // Skip invalid plugin DLLs
            }
        }
    }
}
