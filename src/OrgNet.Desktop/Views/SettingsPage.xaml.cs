using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OrgNet.Desktop.Services;
using OrgNet.Desktop.ViewModels;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace OrgNet.Desktop.Views;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel _vm;

    public SettingsPage()
    {
        this.InitializeComponent();
        _vm = App.GetService<SettingsViewModel>();

        _vm.PropertyChanged += (_, args) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (args.PropertyName == nameof(_vm.TenantName)) TenantNameText.Text = _vm.TenantName;
                if (args.PropertyName == nameof(_vm.TenantPlan)) PlanText.Text = _vm.TenantPlan;
                if (args.PropertyName == nameof(_vm.TenantSector)) SectorText.Text = _vm.TenantSector;
                if (args.PropertyName == nameof(_vm.MemberCount)) MemberCountText.Text = _vm.MemberCount.ToString();
                if (args.PropertyName == nameof(_vm.IsLoading)) LoadingBar.Visibility = _vm.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                if (args.PropertyName == nameof(_vm.StatusMessage)) StatusText.Text = _vm.StatusMessage ?? "";
            });
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnClearCache(object sender, RoutedEventArgs e)
    {
        await _vm.ClearCacheCommand.ExecuteAsync(null);
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            var theme = tag switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (App.MainWindow?.Content is FrameworkElement root)
                root.RequestedTheme = theme;
        }
    }

    private async void OnExportTasks(object sender, RoutedEventArgs e)
    {
        var api = App.GetService<OrgNetApiClient>();
        ExportStatusText.Text = "Exporting tasks...";
        var bytes = await api.ExportTasksCsvAsync();
        if (bytes != null)
            await SaveExportFile("tasks-export.csv", bytes);
        else
            ExportStatusText.Text = api.LastError ?? "Export failed";
    }

    private async void OnExportAudit(object sender, RoutedEventArgs e)
    {
        var api = App.GetService<OrgNetApiClient>();
        ExportStatusText.Text = "Exporting audit log...";
        var bytes = await api.ExportAuditCsvAsync();
        if (bytes != null)
            await SaveExportFile("audit-export.csv", bytes);
        else
            ExportStatusText.Text = api.LastError ?? "Export failed";
    }

    private async Task SaveExportFile(string fileName, byte[] bytes)
    {
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeChoices.Add("CSV", new[] { ".csv" });
            picker.SuggestedFileName = fileName;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await FileIO.WriteBytesAsync(file, bytes);
                ExportStatusText.Text = $"Saved to {file.Path}";
            }
            else
            {
                ExportStatusText.Text = "Export cancelled";
            }
        }
        catch (Exception ex)
        {
            ExportStatusText.Text = $"Save failed: {ex.Message}";
        }
    }
}
