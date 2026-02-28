using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.ViewModels;

namespace OrgNet.Desktop.Views;

public sealed partial class DevicesPage : Page
{
    private readonly DevicesViewModel _vm;

    public DevicesPage()
    {
        this.InitializeComponent();
        _vm = App.GetService<DevicesViewModel>();
        DevicesList.ItemsSource = _vm.Devices;

        _vm.PropertyChanged += (_, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (args.PropertyName == nameof(_vm.IsLoading))
                    LoadingBar.Visibility = _vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            });
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnApproveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid deviceId)
            await _vm.TrustDeviceCommand.ExecuteAsync(deviceId);
    }

    private async void OnDeclineClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid deviceId)
        {
            var dialog = new ContentDialog
            {
                Title = "Decline Device",
                Content = "This will revoke the device and force the user to log out. Are you sure?",
                PrimaryButtonText = "Decline",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                await _vm.RevokeDeviceCommand.ExecuteAsync(deviceId);
        }
    }
}

