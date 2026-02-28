using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;
    private readonly ICredentialStore _credentials;

    public DevicesViewModel(OrgNetApiClient api, ICredentialStore credentials)
    {
        _api = api;
        _credentials = credentials;
        IsAdmin = _credentials.GetUserRole() is "Owner" or "Admin";
    }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAdmin;

    public System.Collections.ObjectModel.ObservableCollection<DeviceDto> Devices { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // Admins see all tenant devices; regular users see only their own
            var devices = IsAdmin
                ? await _api.GetAllDevicesAsync()
                : await _api.GetDevicesAsync();
            Devices.Clear();
            if (devices != null)
                foreach (var d in devices) Devices.Add(d);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task TrustDeviceAsync(Guid deviceId)
    {
        await _api.TrustDeviceAsync(deviceId);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RevokeDeviceAsync(Guid deviceId)
    {
        await _api.RevokeDeviceAsync(deviceId);
        await LoadAsync();
    }
}
