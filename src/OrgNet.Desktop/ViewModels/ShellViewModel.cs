using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;

namespace OrgNet.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ICredentialStore _credentials;
    private readonly SignalRService _signalR;
    private readonly OrgNetApiClient _api;

    public ShellViewModel(INavigationService navigation, ICredentialStore credentials,
        SignalRService signalR, OrgNetApiClient api)
    {
        _navigation = navigation;
        _credentials = credentials;
        _signalR = signalR;
        _api = api;

        _signalR.OnConnectionChanged += connected => IsConnected = connected;
    }

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _selectedNavItem = "Dashboard";

    [RelayCommand]
    private void NavigateTo(string page)
    {
        SelectedNavItem = page;
        var pageType = page switch
        {
            "Dashboard" => typeof(Views.DashboardPage),
            "Modules" => typeof(Views.ModulesPage),
            "Devices" => typeof(Views.DevicesPage),
            "Settings" => typeof(Views.SettingsPage),
            _ => typeof(Views.DashboardPage)
        };
        _navigation.NavigateTo(pageType);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _api.RevokeTokenAsync();
        await _signalR.DisconnectAsync();
        _credentials.Clear();
        _navigation.NavigateTo(typeof(Views.LoginPage));
    }
}
