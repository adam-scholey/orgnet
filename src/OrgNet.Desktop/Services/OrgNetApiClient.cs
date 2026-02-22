using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Typed HTTP client for the OrgNet API.
/// Automatically attaches JWT and tenant headers to every request.
/// Handles token refresh transparently when a 401 is received.
/// </summary>
public class OrgNetApiClient
{
    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;
    private const string BaseUrl = "http://localhost:5100";

    public OrgNetApiClient(ICredentialStore credentials)
    {
        _credentials = credentials;
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    private void AttachHeaders()
    {
        var token = _credentials.GetAccessToken();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tenantId = _credentials.GetTenantId();
        if (tenantId.HasValue)
        {
            _http.DefaultRequestHeaders.Remove(OrgNetConstants.TenantHeaderName);
            _http.DefaultRequestHeaders.Add(OrgNetConstants.TenantHeaderName, tenantId.Value.ToString());
        }
    }

    // ── Auth ──
    public async Task<AuthResponse?> RegisterOrganisationAsync(RegisterOrganisationRequest request)
        => await PostJson<AuthResponse>("api/auth/register-org", request);

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        AttachHeaders();
        return await PostJson<AuthResponse>("api/auth/login", request);
    }

    public async Task<AuthResponse?> LoginByEmailAsync(LoginRequest request)
        => await PostJson<AuthResponse>("api/auth/login-by-email", request);

    public async Task<TokenPair?> RefreshTokenAsync()
    {
        var refreshToken = _credentials.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken)) return null;

        var result = await PostJson<TokenPair>("api/auth/refresh", new RefreshTokenRequest(refreshToken));
        if (result != null)
        {
            var tenantId = _credentials.GetTenantId() ?? Guid.Empty;
            _credentials.StoreTokens(result.AccessToken, result.RefreshToken, tenantId);
        }
        return result;
    }

    public async Task RevokeTokenAsync()
    {
        var refreshToken = _credentials.GetRefreshToken();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            AttachHeaders();
            await _http.PostAsJsonAsync("api/auth/revoke", new RefreshTokenRequest(refreshToken));
        }
    }

    // ── Tenant ──
    public async Task<TenantInfoDto?> GetTenantAsync() => await GetJson<TenantInfoDto>("api/tenant");
    public async Task<List<UserProfileDto>?> GetMembersAsync() => await GetJson<List<UserProfileDto>>("api/tenant/members");

    // ── Modules ──
    public async Task<List<ModuleInfoDto>?> GetModulesAsync() => await GetJson<List<ModuleInfoDto>>("api/modules");
    public async Task<bool> ToggleModuleAsync(string moduleId, bool enabled)
    {
        AttachHeaders();
        var response = await _http.PostAsJsonAsync("api/modules/toggle", new ToggleModuleRequest(moduleId, enabled));
        return response.IsSuccessStatusCode;
    }

    // ── Devices ──
    public async Task<List<DeviceDto>?> GetDevicesAsync() => await GetJson<List<DeviceDto>>("api/devices");
    public async Task<DeviceDto?> RegisterDeviceAsync(RegisterDeviceRequest request)
        => await PostJson<DeviceDto>("api/devices/register", request);

    // ── Audit ──
    public async Task<AuditPageResult?> GetAuditLogsAsync(int page = 1) => await GetJson<AuditPageResult>($"api/audit?page={page}");

    // ── Helpers ──
    public string? LastError { get; private set; }

    private async Task<T?> GetJson<T>(string url)
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var refreshed = await RefreshTokenAsync();
                if (refreshed == null) { LastError = "Session expired — please log in again"; return default; }
                AttachHeaders();
                response = await _http.GetAsync(url);
            }

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Server returned {(int)response.StatusCode}";
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Cannot reach API at {BaseUrl}: {ex.Message}";
            return default;
        }
        catch (TaskCanceledException)
        {
            LastError = "Request timed out — is the API running?";
            return default;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return default;
        }
    }

    private async Task<T?> PostJson<T>(string url, object data)
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.PostAsJsonAsync(url, data);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                LastError = $"Server returned {(int)response.StatusCode}: {body}";
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Cannot reach API at {BaseUrl}: {ex.Message}";
            return default;
        }
        catch (TaskCanceledException)
        {
            LastError = "Request timed out — is the API running?";
            return default;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return default;
        }
    }
}
