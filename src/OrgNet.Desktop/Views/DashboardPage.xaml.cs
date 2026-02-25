using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.ViewModels;

namespace OrgNet.Desktop.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel _vm;

    public DashboardPage()
    {
        this.InitializeComponent();
        _vm = App.GetService<DashboardViewModel>();

        _vm.PropertyChanged += (_, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (args.PropertyName == nameof(_vm.TenantName)) TenantNameText.Text = _vm.TenantName;
                if (args.PropertyName == nameof(_vm.TenantPlan)) PlanText.Text = _vm.TenantPlan;
                if (args.PropertyName == nameof(_vm.TenantSector)) SectorText.Text = _vm.TenantSector;
                if (args.PropertyName == nameof(_vm.MemberCount)) MemberCountText.Text = _vm.MemberCount.ToString();
                if (args.PropertyName == nameof(_vm.IsLoading)) LoadingBar.Visibility = _vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            });
        };

        EventsList.ItemsSource = _vm.RecentEvents;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
