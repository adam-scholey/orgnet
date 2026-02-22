using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;

namespace OrgNet.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;
    private readonly LocalCacheService _cache;
    private readonly ICredentialStore _credentials;

    public SettingsViewModel(OrgNetApiClient api, LocalCacheService cache, ICredentialStore credentials)
    {
        _api = api;
        _cache = cache;
        _credentials = credentials;
    }

    [ObservableProperty] private string _tenantName = "";
    [ObservableProperty] private string _tenantPlan = "";
    [ObservableProperty] private int _memberCount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var tenant = await _api.GetTenantAsync();
            if (tenant != null)
            {
                TenantName = tenant.Name;
                TenantPlan = tenant.Plan;
                MemberCount = tenant.MemberCount;
            }
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        await _cache.ClearAsync();
        StatusMessage = "Local cache cleared";
    }
}
