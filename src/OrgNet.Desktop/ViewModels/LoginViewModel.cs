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
    [ObservableProperty] private string _sector = "Other";
    [ObservableProperty] private string _inviteToken = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isRegisterMode;
    [ObservableProperty] private bool _isJoinMode;
    [ObservableProperty] private bool _inviteLookedUp;
    [ObservableProperty] private string? _inviteOrgName;
    [ObservableProperty] private string? _inviteRole;
    [ObservableProperty] private string? _inviteEmail;
    [ObservableProperty] private string? _invitedBy;

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
                new RegisterOrganisationRequest(OrganisationName, DisplayName, Email, Password, Sector));

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
    private async Task LookupInviteAsync()
    {
        if (string.IsNullOrWhiteSpace(InviteToken))
        {
            ErrorMessage = "Paste your invite token";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var info = await _api.GetInviteInfoAsync(InviteToken.Trim());
            if (info != null)
            {
                if (info.IsAccepted)
                {
                    ErrorMessage = "This invitation has already been accepted";
                    return;
                }
                if (info.IsExpired)
                {
                    ErrorMessage = "This invitation has expired. Ask your admin for a new one.";
                    return;
                }

                InviteLookedUp = true;
                InviteOrgName = info.OrganisationName;
                InviteRole = info.Role;
                InviteEmail = info.Email;
                InvitedBy = info.InvitedBy;
            }
            else
            {
                ErrorMessage = _api.LastError ?? "Invalid invite token";
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
    private async Task JoinAsync()
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Display name and password are required";
            return;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters";
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await _api.AcceptInviteAsync(
                new AcceptInviteRequest(InviteToken.Trim(), DisplayName, Password));

            if (result is { Success: true })
            {
                _credentials.StoreTokens(result.AccessToken, result.RefreshToken, result.TenantId);
                await _signalR.ConnectAsync();
                _navigation.NavigateTo(typeof(Views.ShellPage));
            }
            else
            {
                ErrorMessage = result?.Error ?? _api.LastError ?? "Failed to join organisation";
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

    public void ResetJoinMode()
    {
        IsJoinMode = false;
        InviteLookedUp = false;
        InviteToken = string.Empty;
        InviteOrgName = null;
        InviteRole = null;
        InviteEmail = null;
        InvitedBy = null;
        ErrorMessage = null;
    }
}
