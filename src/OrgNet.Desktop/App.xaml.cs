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

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // ── Core Services ──
                services.AddSingleton<ICredentialStore, CredentialStore>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<OrgNetApiClient>();
                services.AddSingleton<SignalRService>();
                services.AddSingleton<LocalCacheService>();
                services.AddSingleton<DesktopPluginLoader>();

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

        // Check for stored credentials — auto-login or show login
        var credStore = GetService<ICredentialStore>();
        var token = credStore.GetAccessToken();

        if (!string.IsNullOrEmpty(token))
        {
            rootFrame.Navigate(typeof(Views.ShellPage));
        }
        else
        {
            rootFrame.Navigate(typeof(Views.LoginPage));
        }

        _window.Title = "OrgNet";
        _window.Activate();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
    }
}
