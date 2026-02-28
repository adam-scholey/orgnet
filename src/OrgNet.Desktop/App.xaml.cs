using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml.Navigation;
using OrgNet.Desktop.Services;
using OrgNet.Desktop.ViewModels;

namespace OrgNet.Desktop;

/// <summary>
/// Application entry point with Microsoft.Extensions.Hosting DI container.
/// 
/// Architectural decisions:
/// - IHost provides DI, configuration, and hosted service lifecycle.
/// - All services are registered here — ViewModels, API client, SignalR, etc.
/// - Background services (heartbeat, sync) run as IHostedService.
/// - Credential Locker is used for secure JWT storage (no plaintext on disk).
/// </summary>
public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public static IServiceProvider Services => Host.Services;
    public static T GetService<T>() where T : class => Host.Services.GetRequiredService<T>();
    public static Window MainWindow { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        this.InitializeComponent();

        // Search multiple directories for appsettings.json (handles x64/win-x64 output paths)
        var searchDirs = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "win-x64"),
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };
        var appSettingsDir = searchDirs.FirstOrDefault(d => File.Exists(Path.Combine(d, "appsettings.json")))
                             ?? AppContext.BaseDirectory;

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.Sources.Clear();
                config.SetBasePath(appSettingsDir);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                // ── Core Services ──
                services.AddSingleton<ICredentialStore, CredentialStore>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<OrgNetApiClient>();
                services.AddSingleton<SignalRService>();
                services.AddSingleton<LocalCacheService>();
                services.AddSingleton<DesktopPluginLoader>();
                services.AddSingleton<NotificationService>();

                // ── Background Services ──
                services.AddHostedService<HeartbeatService>();

                // ── ViewModels ──
                services.AddTransient<LoginViewModel>();
                services.AddTransient<ShellViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<ModulesViewModel>();
                services.AddTransient<DevicesViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<FileVaultViewModel>();
                services.AddTransient<ChatViewModel>();
                services.AddTransient<NotesViewModel>();
                services.AddTransient<TaskBoardViewModel>();
                services.AddTransient<AnnouncementsViewModel>();
                services.AddTransient<AppointmentsViewModel>();
            })
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs e)
    {
        // Start hosted services (heartbeat, sync)
        await Host.StartAsync();

        // Initialise local SQLite cache
        var cache = GetService<LocalCacheService>();
        await cache.InitialiseAsync();

        _window = new Window();
        MainWindow = _window;

        if (_window.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            _window.Content = rootFrame;
        }

        // Set up navigation service with the root frame
        var nav = GetService<INavigationService>();
        if (nav is NavigationService navService)
            navService.SetFrame(rootFrame);

        // Check for stored credentials — validate before auto-login
        var credStore = GetService<ICredentialStore>();
        var token = credStore.GetAccessToken();
        var goToShell = false;

        if (!string.IsNullOrEmpty(token))
        {
            // Quick validation: try calling /api/auth/me to confirm the token is still valid
            try
            {
                var api = GetService<OrgNetApiClient>();
                var me = await api.GetMeAsync();
                goToShell = me != null;
            }
            catch
            {
                goToShell = false;
            }

            if (!goToShell)
            {
                // Token is invalid/expired and refresh failed — force re-login
                credStore.Clear();
                // Clear any stale error so it doesn't show on the login page
                var api2 = GetService<OrgNetApiClient>();
                api2.LastError = null;
            }
            else
            {
                // Auto-login succeeded — connect SignalR and register device
                try
                {
                    var signalR = GetService<SignalRService>();
                    _ = signalR.ConnectAsync();
                }
                catch { /* best-effort */ }

                try
                {
                    var machineName = Environment.MachineName;
                    var osVersion = Environment.OSVersion.ToString();
                    var raw = $"{machineName}|{osVersion}|{Environment.UserName}";
                    var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
                    var fingerprint = Convert.ToHexString(hash);
                    var winVer = Environment.OSVersion.Version;
                    var platform = winVer.Major >= 10 && winVer.Build >= 22000 ? "Windows 11" : $"Windows {winVer.Major}";
                    var api = GetService<OrgNetApiClient>();
                    _ = api.RegisterDeviceAsync(new Shared.DTOs.RegisterDeviceRequest(machineName, fingerprint, platform));
                }
                catch { /* best-effort */ }
            }
        }

        rootFrame.Navigate(goToShell ? typeof(Views.ShellPage) : typeof(Views.LoginPage));

        _window.Title = "OrgNet";
        _window.Activate();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
    }
}
