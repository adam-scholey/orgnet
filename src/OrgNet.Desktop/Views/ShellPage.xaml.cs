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

        // Navigate to Dashboard by default
        ContentFrame.Navigate(typeof(DashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        UpdateConnectionStatus(_signalR.IsConnected);
    }

    private void OnConnectionChanged(bool connected)
    {
        DispatcherQueue.TryEnqueue(() => UpdateConnectionStatus(connected));
    }

    private void UpdateConnectionStatus(bool connected)
    {
        ConnectionDot.Fill = new SolidColorBrush(connected ? Colors.LimeGreen : Colors.Gray);
        ConnectionText.Text = connected ? "Connected" : "Disconnected";
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
                case "AppLauncher":
                    ContentFrame.Navigate(typeof(AppLauncherPage));
                    break;
                case "Logout":
                    await _vm.LogoutCommand.ExecuteAsync(null);
                    break;
            }
        }
    }
}
