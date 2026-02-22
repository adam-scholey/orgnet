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
        // For MVP, show placeholder services. In production, these come from the API.
        _services = new List<ServiceEntryDto>
        {
            new("wiki", "Internal Wiki", "https://en.wikipedia.org", "Healthy"),
            new("status", "Status Page", "https://status.github.com", "Healthy"),
        };

        if (_services.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            AppGrid.ItemsSource = _services;
        }
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
