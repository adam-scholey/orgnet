using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.ViewModels;

namespace OrgNet.Desktop.Views;

public sealed partial class ModulesPage : Page
{
    private readonly ModulesViewModel _vm;

    public ModulesPage()
    {
        this.InitializeComponent();
        _vm = App.GetService<ModulesViewModel>();
        ModulesList.ItemsSource = _vm.Modules;

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
