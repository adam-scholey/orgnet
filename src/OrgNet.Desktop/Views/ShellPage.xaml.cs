using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OrgNet.Desktop.Services;
using OrgNet.Desktop.ViewModels;

namespace OrgNet.Desktop.Views;

public sealed partial class ShellPage : Page
{
    private readonly ShellViewModel _vm;
    private readonly SignalRService _signalR;

    public ShellPage()
    {
        this.InitializeComponent();
        _vm = App.GetService<ShellViewModel>();
        _signalR = App.GetService<SignalRService>();

        _signalR.OnConnectionChanged += OnConnectionChanged;
        _signalR.OnDeviceRevoked += OnDeviceRevoked;

        // Navigate to Dashboard by default
        ContentFrame.Navigate(typeof(DashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        UpdateConnectionStatus(_signalR.IsConnected);

        // #5 Role-based nav visibility — hide admin-only items for Members
        ApplyRoleBasedNav();

        // #3 Toast notifications — attach global notification bar
        var notificationService = App.GetService<NotificationService>();
        notificationService.Attach(NotificationBar, DispatcherQueue);

        // #15 Keyboard shortcuts
        this.KeyDown += OnGlobalKeyDown;
    }

    private void OnGlobalKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!e.KeyStatus.IsMenuKeyDown) return; // Alt key must be held
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Number1:
                ContentFrame.Navigate(typeof(DashboardPage)); break;
            case Windows.System.VirtualKey.C:
                ContentFrame.Navigate(typeof(ChatPage)); break;
            case Windows.System.VirtualKey.F:
                ContentFrame.Navigate(typeof(FileVaultPage)); break;
            case Windows.System.VirtualKey.N:
                ContentFrame.Navigate(typeof(NotesPage)); break;
            case Windows.System.VirtualKey.T:
                ContentFrame.Navigate(typeof(TaskBoardPage)); break;
            case Windows.System.VirtualKey.A:
                ContentFrame.Navigate(typeof(AnnouncementsPage)); break;
            case Windows.System.VirtualKey.P:
                ContentFrame.Navigate(typeof(ProfilePage)); break;
            case Windows.System.VirtualKey.S:
                ContentFrame.Navigate(typeof(SettingsPage)); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnConnectionChanged(bool connected)
    {
        DispatcherQueue.TryEnqueue(() => UpdateConnectionStatus(connected));
    }

    private void OnDeviceRevoked(Guid deviceId, string reason)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "Device Revoked",
                Content = $"Your device has been revoked by an administrator.\n\nReason: {reason}\n\nYou will be logged out.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();

            // Force logout: clear credentials and navigate to login
            var credentials = App.GetService<ICredentialStore>();
            credentials.Clear();
            var signalR = App.GetService<SignalRService>();
            await signalR.DisconnectAsync();
            var nav = App.GetService<INavigationService>();
            nav.NavigateTo(typeof(LoginPage));
        });
    }

    private void UpdateConnectionStatus(bool connected)
    {
        ConnectionDot.Fill = new SolidColorBrush(connected ? Colors.LimeGreen : Colors.Gray);
        ConnectionText.Text = connected ? "Connected" : "Disconnected";
    }

    private void ApplyRoleBasedNav()
    {
        var credentials = App.GetService<ICredentialStore>();
        var role = credentials.GetUserRole();
        var isAdmin = role is "Owner" or "Admin";

        // Hide admin-only nav items for Members/Moderators
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            var tag = item.Tag?.ToString();
            if (tag is "Modules" or "Devices" or "Users")
                item.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "Modules":
                    ContentFrame.Navigate(typeof(ModulesPage));
                    break;
                case "Devices":
                    ContentFrame.Navigate(typeof(DevicesPage));
                    break;
                case "Users":
                    ContentFrame.Navigate(typeof(UserManagementPage));
                    break;
                case "Chat":
                    ContentFrame.Navigate(typeof(ChatPage));
                    break;
                case "FileVault":
                    ContentFrame.Navigate(typeof(FileVaultPage));
                    break;
                case "Notes":
                    ContentFrame.Navigate(typeof(NotesPage));
                    break;
                case "TaskBoard":
                    ContentFrame.Navigate(typeof(TaskBoardPage));
                    break;
                case "Announcements":
                    ContentFrame.Navigate(typeof(AnnouncementsPage));
                    break;
                case "Appointments":
                    ContentFrame.Navigate(typeof(AppointmentsPage));
                    break;
                case "AppLauncher":
                    ContentFrame.Navigate(typeof(AppLauncherPage));
                    break;
                case "Profile":
                    ContentFrame.Navigate(typeof(ProfilePage));
                    break;
                case "Logout":
                    await _vm.LogoutCommand.ExecuteAsync(null);
                    break;
            }
        }
    }
}
