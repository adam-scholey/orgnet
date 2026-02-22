using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;

    public DevicesViewModel(OrgNetApiClient api) => _api = api;

    [ObservableProperty] private bool _isLoading;

    public System.Collections.ObjectModel.ObservableCollection<DeviceDto> Devices { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var devices = await _api.GetDevicesAsync();
            Devices.Clear();
            if (devices != null)
                foreach (var d in devices) Devices.Add(d);
        }
        finally { IsLoading = false; }
    }
}
