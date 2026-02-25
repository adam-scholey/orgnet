using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Views;

/// <summary>
/// App launcher supporting three launch modes:
/// - Desktop: launches installed .exe via Process.Start (ShellExecute)
/// - Protocol: launches via registered URI protocol (e.g. ms-teams:, slack:)
/// - Web: opens in embedded WebView2
/// </summary>
public sealed partial class AppLauncherPage : Page
{
    private readonly OrgNetApiClient _api;
    private List<LaunchableApp> _apps = new();

    public AppLauncherPage()
    {
        this.InitializeComponent();
        _api = App.GetService<OrgNetApiClient>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _apps = new List<LaunchableApp>
        {
            // Desktop apps — launched via Process.Start with ShellExecute
            new("Microsoft Teams", LaunchMode.Protocol, "ms-teams:", "msteams.exe",
                "https://www.microsoft.com/en-gb/microsoft-teams/download-app"),
            new("Slack", LaunchMode.Protocol, "slack:", "slack.exe",
                "https://slack.com/downloads/windows"),
            new("Visual Studio Code", LaunchMode.Desktop, "code", "code.cmd",
                "https://code.visualstudio.com/download"),
            new("File Explorer", LaunchMode.Desktop, "explorer.exe", null, null),
            new("Notepad", LaunchMode.Desktop, "notepad.exe", null, null),
            new("Calculator", LaunchMode.Desktop, "calc.exe", null, null),
            new("Windows Terminal", LaunchMode.Protocol, "wt:", "wt.exe",
                "https://aka.ms/terminal"),
            new("PowerShell", LaunchMode.Desktop, "pwsh.exe", "powershell.exe", null),

            // Web apps — opened in embedded WebView2
            new("Email (Outlook)", LaunchMode.Web, "https://outlook.office.com", null, null),
            new("Calendar", LaunchMode.Web, "https://calendar.google.com", null, null),
            new("Cloud Drive", LaunchMode.Web, "https://drive.google.com", null, null),
            new("GitHub", LaunchMode.Web, "https://github.com", null, null),
            new("Figma", LaunchMode.Web, "https://www.figma.com", null, null),
            new("Notion", LaunchMode.Web, "https://www.notion.so", null, null),
        };

        AppGrid.ItemsSource = _apps;
    }

    private async void OnAppTapped(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LaunchableApp app }) return;

        switch (app.Mode)
        {
            case LaunchMode.Desktop:
                await LaunchDesktopApp(app);
                break;

            case LaunchMode.Protocol:
                await LaunchProtocolApp(app);
                break;

            case LaunchMode.Web:
                OpenInWebView(app);
                break;
        }
    }

    private async Task LaunchDesktopApp(LaunchableApp app)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = app.Target,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Primary exe not found — try fallback
            if (!string.IsNullOrEmpty(app.FallbackExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = app.FallbackExe, UseShellExecute = true });
                    return;
                }
                catch { /* fall through to not-installed dialog */ }
            }
            await ShowNotInstalledDialog(app);
        }
        catch (Exception ex)
        {
            await ShowErrorDialog(app.Name, ex.Message);
        }
    }

    private async Task LaunchProtocolApp(LaunchableApp app)
    {
        try
        {
            var launched = await Windows.System.Launcher.LaunchUriAsync(new Uri(app.Target));
            if (!launched)
            {
                // Protocol not registered — app probably not installed
                await ShowNotInstalledDialog(app);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialog(app.Name, ex.Message);
        }
    }

    private void OpenInWebView(LaunchableApp app)
    {
        LauncherPanel.Visibility = Visibility.Collapsed;
        WebViewContainer.Visibility = Visibility.Visible;
        WebViewTitle.Text = app.Name;

        try
        {
            EmbeddedWebView.Source = new Uri(app.Target);
        }
        catch
        {
            WebViewTitle.Text = $"{app.Name} — Invalid URL";
        }
    }

    private async Task ShowNotInstalledDialog(LaunchableApp app)
    {
        var dialog = new ContentDialog
        {
            Title = $"{app.Name} Not Found",
            Content = string.IsNullOrEmpty(app.DownloadUrl)
                ? $"{app.Name} does not appear to be installed on this device."
                : $"{app.Name} does not appear to be installed.\n\nWould you like to download it?",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot
        };

        if (!string.IsNullOrEmpty(app.DownloadUrl))
        {
            dialog.PrimaryButtonText = "Download";
        }

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(app.DownloadUrl))
        {
            Process.Start(new ProcessStartInfo { FileName = app.DownloadUrl, UseShellExecute = true });
        }
    }

    private async Task ShowErrorDialog(string appName, string message)
    {
        await new ContentDialog
        {
            Title = $"Error Launching {appName}",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        }.ShowAsync();
    }

    private void OnBackToLauncher(object sender, RoutedEventArgs e)
    {
        WebViewContainer.Visibility = Visibility.Collapsed;
        LauncherPanel.Visibility = Visibility.Visible;
        EmbeddedWebView.Source = new Uri("about:blank");
    }
}

public enum LaunchMode { Desktop, Protocol, Web }

public record LaunchableApp(
    string Name,
    LaunchMode Mode,
    string Target,         // exe path, protocol URI, or web URL
    string? FallbackExe,   // fallback exe if primary not found
    string? DownloadUrl);  // URL to download if not installed
