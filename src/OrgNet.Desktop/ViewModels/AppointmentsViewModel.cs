using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class AppointmentsViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public AppointmentsViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _selectedRange = "week";

    // Create form fields
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private string _newLocation = string.Empty;
    [ObservableProperty] private DateTimeOffset _newStartDate = DateTimeOffset.Now;
    [ObservableProperty] private TimeSpan _newStartTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private DateTimeOffset _newEndDate = DateTimeOffset.Now;
    [ObservableProperty] private TimeSpan _newEndTime = DateTime.Now.AddHours(1).TimeOfDay;

    public ObservableCollection<AppointmentDto> Appointments { get; } = new();
    public ObservableCollection<AppointmentDto> UpcomingAppointments { get; } = new();

    [RelayCommand]
    public async Task LoadAppointmentsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var appointments = await _api.GetAppointmentsAsync(SelectedRange);
            Appointments.Clear();
            UpcomingAppointments.Clear();
            if (appointments != null)
            {
                foreach (var a in appointments)
                {
                    Appointments.Add(a);
                    if (!a.IsCancelled && a.StartsAt > DateTime.UtcNow)
                        UpcomingAppointments.Add(a);
                }
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task CreateAppointmentAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            ErrorMessage = "Title is required";
            return;
        }

        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var startsAt = NewStartDate.Date.Add(NewStartTime).ToUniversalTime();
            var endsAt = NewEndDate.Date.Add(NewEndTime).ToUniversalTime();

            if (endsAt <= startsAt)
            {
                ErrorMessage = "End time must be after start time";
                IsLoading = false;
                return;
            }

            var request = new CreateAppointmentRequest(
                NewTitle.Trim(),
                string.IsNullOrWhiteSpace(NewDescription) ? null : NewDescription.Trim(),
                string.IsNullOrWhiteSpace(NewLocation) ? null : NewLocation.Trim(),
                startsAt,
                endsAt);

            var result = await _api.CreateAppointmentAsync(request);
            if (result != null)
            {
                Appointments.Insert(0, result);
                if (!result.IsCancelled && result.StartsAt > DateTime.UtcNow)
                    UpcomingAppointments.Insert(0, result);

                // Reset form
                NewTitle = string.Empty;
                NewDescription = string.Empty;
                NewLocation = string.Empty;
                NewStartDate = DateTimeOffset.Now;
                NewStartTime = DateTime.Now.TimeOfDay;
                NewEndDate = DateTimeOffset.Now;
                NewEndTime = DateTime.Now.AddHours(1).TimeOfDay;
            }
            else
            {
                ErrorMessage = _api.LastError ?? "Failed to create appointment";
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task CancelAppointmentAsync(Guid appointmentId)
    {
        ErrorMessage = null;
        try
        {
            var result = await _api.CancelAppointmentAsync(appointmentId);
            if (result)
                await LoadAppointmentsAsync();
            else
                ErrorMessage = _api.LastError ?? "Failed to cancel appointment";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
