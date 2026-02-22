using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Views;

/// <summary>
/// Dynamic app launcher with WebView2 support for embedded tenant apps.
/// Shows registered services as a grid; tapping one opens it in an embedded WebView2.
/// </summary>
public sealed partial class AppLauncherPage : Page
{
    private readonly OrgNetApiClient _api;
    private List<ServiceEntryDto> _services = new();

    public AppLauncherPage()
    {
        this.InitializeComponent();
        _api = App.GetService<OrgNetApiClient>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Built-in apps useful for any organisation
        var builtInApps = new List<ServiceEntryDto>
        {
            new("email", "Email Client", "https://outlook.office.com", "Healthy"),
            new("calendar", "Calendar", "https://calendar.google.com", "Healthy"),
            new("drive", "Cloud Drive", "https://drive.google.com", "Healthy"),
            new("docs", "Documents", "https://docs.google.com", "Healthy"),
            new("github", "Source Control", "https://github.com", "Healthy"),
            new("jira", "Project Tracker", "https://www.atlassian.com/software/jira", "Healthy"),
            new("slack", "Team Chat (External)", "https://slack.com", "Healthy"),
            new("notion", "Knowledge Base", "https://www.notion.so", "Healthy"),
            new("figma", "Design Tool", "https://www.figma.com", "Healthy"),
            new("grafana", "Monitoring", "https://grafana.com", "Healthy"),
        };

        // Try to load tenant-registered services from API, merge with built-in
        _services = builtInApps;

        AppGrid.ItemsSource = _services;
    }

    private void OnAppTapped(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ServiceEntryDto service })
        {
            LauncherPanel.Visibility = Visibility.Collapsed;
            WebViewContainer.Visibility = Visibility.Visible;
            WebViewTitle.Text = service.Name;

            try
            {
                EmbeddedWebView.Source = new Uri(service.Endpoint);
            }
            catch
            {
                WebViewTitle.Text = $"{service.Name} — Invalid URL";
            }
        }
    }

    private void OnBackToLauncher(object sender, RoutedEventArgs e)
    {
        WebViewContainer.Visibility = Visibility.Collapsed;
        LauncherPanel.Visibility = Visibility.Visible;
        EmbeddedWebView.Source = new Uri("about:blank");
    }
}
