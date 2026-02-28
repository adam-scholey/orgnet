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
}

