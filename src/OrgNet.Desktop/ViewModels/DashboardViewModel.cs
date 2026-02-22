using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;
    private readonly SignalRService _signalR;
    private readonly LocalCacheService _cache;

    public DashboardViewModel(OrgNetApiClient api, SignalRService signalR, LocalCacheService cache)
    {
        _api = api;
        _signalR = signalR;
        _cache = cache;

        _signalR.OnTenantEvent += evt =>
        {
            RecentEvents.Insert(0, evt);
            if (RecentEvents.Count > 50) RecentEvents.RemoveAt(RecentEvents.Count - 1);
            OnPropertyChanged(nameof(RecentEvents));
        };
    }

    [ObservableProperty] private string _tenantName = "Loading...";
    [ObservableProperty] private string _tenantPlan = "";
    [ObservableProperty] private int _memberCount;
    [ObservableProperty] private bool _isLoading;

    public System.Collections.ObjectModel.ObservableCollection<TenantEventDto> RecentEvents { get; } = new();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // Try cache first for instant rendering
            var cachedName = await _cache.GetAsync("tenant_name");
            if (!string.IsNullOrEmpty(cachedName))
                TenantName = cachedName;

            // Then fetch fresh data
            var tenant = await _api.GetTenantAsync();
            if (tenant != null)
            {
                TenantName = tenant.Name;
                TenantPlan = tenant.Plan;
                MemberCount = tenant.MemberCount;

                await _cache.SetAsync("tenant_name", tenant.Name);
                await _cache.SetAsync("tenant_plan", tenant.Plan);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
