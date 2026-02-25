using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using OrgNet.Desktop.ViewModels;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Views;

public sealed partial class AppointmentsPage : Page
{
    private readonly AppointmentsViewModel _vm;

    public AppointmentsPage()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<AppointmentsViewModel>();
        Loaded += async (_, _) => await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        LoadingBar.Visibility = Visibility.Visible;
        await _vm.LoadAppointmentsAsync();
        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private void UpdateUI()
    {
        ErrorBar.IsOpen = !string.IsNullOrEmpty(_vm.ErrorMessage);
        if (ErrorBar.IsOpen) ErrorBar.Message = _vm.ErrorMessage!;

        AppointmentListView.ItemsSource = _vm.Appointments;
        CountText.Text = $"{_vm.Appointments.Count} appointment(s)";
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        await LoadPageAsync();
    }

    private async void OnCreateClicked(object sender, RoutedEventArgs e)
    {
        _vm.NewTitle = TitleBox.Text?.Trim() ?? "";
        _vm.NewDescription = DescriptionBox.Text?.Trim() ?? "";
        _vm.NewLocation = LocationBox.Text?.Trim() ?? "";
        _vm.NewStartDate = StartDatePicker.Date;
        _vm.NewStartTime = StartTimePicker.Time;
        _vm.NewEndDate = EndDatePicker.Date;
        _vm.NewEndTime = EndTimePicker.Time;

        LoadingBar.Visibility = Visibility.Visible;
        await _vm.CreateAppointmentAsync();

        if (string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            TitleBox.Text = "";
            DescriptionBox.Text = "";
            LocationBox.Text = "";
        }

        UpdateUI();
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private async void OnCancelAppointmentClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid appointmentId)
        {
            var dialog = new ContentDialog
            {
                Title = "Cancel Appointment",
                Content = "Are you sure you want to cancel this appointment?",
                PrimaryButtonText = "Cancel It",
                CloseButtonText = "Keep",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _vm.CancelAppointmentAsync(appointmentId);
                UpdateUI();
            }
        }
    }

    private async void OnRangeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RangeCombo.SelectedItem is ComboBoxItem item && item.Tag is string range)
        {
            _vm.SelectedRange = range;
            await LoadPageAsync();
        }
    }
}
