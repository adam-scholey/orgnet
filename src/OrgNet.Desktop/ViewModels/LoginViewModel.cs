using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrgNet.Desktop.Services;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly OrgNetApiClient _api;
    private readonly ICredentialStore _credentials;
    private readonly INavigationService _navigation;
    private readonly SignalRService _signalR;

    public LoginViewModel(OrgNetApiClient api, ICredentialStore credentials,
        INavigationService navigation, SignalRService signalR)
    {
        _api = api;
        _credentials = credentials;
        _navigation = navigation;
        _signalR = signalR;
    }

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _organisationName = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRegisterMode;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _api.LoginByEmailAsync(new LoginRequest(Email, Password));
            if (result is { Success: true })
            {
                _credentials.StoreTokens(result.AccessToken, result.RefreshToken, result.TenantId);
                await _signalR.ConnectAsync();
                _navigation.NavigateTo(typeof(Views.ShellPage));
            }
            else
            {
                ErrorMessage = result?.Error ?? _api.LastError ?? "Login failed";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(OrganisationName) || string.IsNullOrWhiteSpace(Email)
            || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "All fields are required";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _api.RegisterOrganisationAsync(
                new RegisterOrganisationRequest(OrganisationName, DisplayName, Email, Password));

            if (result is { Success: true })
            {
                _credentials.StoreTokens(result.AccessToken, result.RefreshToken, result.TenantId);
                await _signalR.ConnectAsync();
                _navigation.NavigateTo(typeof(Views.ShellPage));
            }
            else
            {
                ErrorMessage = result?.Error ?? _api.LastError ?? "Registration failed";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleMode() => IsRegisterMode = !IsRegisterMode;
}
