using Blazored.LocalStorage;
using OrgNet.Shared.DTOs;

namespace OrgNet.Pwa.Services;

public class AuthService
{
    private readonly ApiClient _api;
    private readonly ILocalStorageService _storage;
    private readonly TokenAuthStateProvider _authState;

    public AuthService(ApiClient api, ILocalStorageService storage, Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider authState)
    {
        _api = api;
        _storage = storage;
        _authState = (TokenAuthStateProvider)authState;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        var result = await _api.PostAsync<AuthResponse>("api/auth/login-by-email", new LoginRequest(email, password));
        if (result is { Success: true })
        {
            await _storage.SetItemAsStringAsync("access_token", result.AccessToken);
            await _storage.SetItemAsStringAsync("refresh_token", result.RefreshToken);
            await _storage.SetItemAsStringAsync("tenant_id", result.TenantId.ToString());
            _authState.NotifyAuthChanged();
            return (true, null);
        }
        return (false, result?.Error ?? "Login failed");
    }

    public async Task LogoutAsync()
    {
        await _storage.RemoveItemAsync("access_token");
        await _storage.RemoveItemAsync("refresh_token");
        await _storage.RemoveItemAsync("tenant_id");
        _authState.NotifyAuthChanged();
    }

    public async Task<string?> GetTokenAsync() => await _storage.GetItemAsStringAsync("access_token");
    public async Task<string?> GetTenantIdAsync() => await _storage.GetItemAsStringAsync("tenant_id");
}
