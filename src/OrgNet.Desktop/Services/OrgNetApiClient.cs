using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
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
    private readonly string _baseUrl;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public OrgNetApiClient(ICredentialStore credentials, IConfiguration configuration)
    {
        _credentials = credentials;
        _baseUrl = configuration["OrgNet:ApiBaseUrl"] ?? "http://localhost:5100";
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl), Timeout = TimeSpan.FromSeconds(30) };
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
        // Prevent concurrent refreshes — refresh tokens are single-use with rotation.
        // A second concurrent call would use an already-revoked token, triggering
        // replay-attack detection and killing the entire session.
        if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10)))
            return null;

        try
        {
            var refreshToken = _credentials.GetRefreshToken();
            if (string.IsNullOrEmpty(refreshToken)) return null;

            // Call refresh directly — do NOT use PostJson (which retries on 401, causing recursion)
            var response = await _http.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest(refreshToken));
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<TokenPair>();
            if (result != null)
            {
                var tenantId = _credentials.GetTenantId() ?? Guid.Empty;
                _credentials.StoreTokens(result.AccessToken, result.RefreshToken, tenantId);
            }
            return result;
        }
        catch
        {
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
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

    // ── Profile ──
    public async Task<object?> GetMeAsync() => await GetJson<object>("api/auth/me");

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

    // ── User Management ──
    public async Task<InviteResponse?> PostInviteAsync(InviteUserRequest request)
        => await PostJson<InviteResponse>("api/tenant/invite", request);

    public async Task<List<InvitationInfoDto>?> GetInvitationsAsync()
        => await GetJson<List<InvitationInfoDto>>("api/tenant/invitations");

    public async Task<InvitationInfoDto?> GetInviteInfoAsync(string token)
        => await GetJson<InvitationInfoDto>($"api/auth/invite-info?token={token}");

    public async Task<AuthResponse?> AcceptInviteAsync(AcceptInviteRequest request)
        => await PostJson<AuthResponse>("api/auth/accept-invite", request);

    // ── Devices ──
    public async Task<List<DeviceDto>?> GetDevicesAsync() => await GetJson<List<DeviceDto>>("api/devices");
    public async Task<List<DeviceDto>?> GetAllDevicesAsync() => await GetJson<List<DeviceDto>>("api/devices/all");
    public async Task<DeviceDto?> RegisterDeviceAsync(RegisterDeviceRequest request)
        => await PostJson<DeviceDto>("api/devices/register", request);
    public async Task<bool> TrustDeviceAsync(Guid deviceId)
    {
        var result = await PutJson<object>($"api/devices/{deviceId}/trust", new { });
        return result != null;
    }
    public async Task<bool> RevokeDeviceAsync(Guid deviceId)
    {
        var result = await PutJson<object>($"api/devices/{deviceId}/revoke", new { });
        return result != null;
    }

    // ── Audit ──
    public async Task<AuditPageResult?> GetAuditLogsAsync(int page = 1) => await GetJson<AuditPageResult>($"api/audit?page={page}");

    // ── File Vault ──
    public async Task<List<OrgFileDto>?> GetFilesAsync() => await GetJson<List<OrgFileDto>>("api/files");
    public async Task<OrgFileDto?> UploadFileAsync(UploadFileRequest request) => await PostJson<OrgFileDto>("api/files/upload", request);
    public async Task<byte[]?> DownloadFileAsync(Guid fileId, string pin)
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.PostAsJsonAsync("api/files/download", new DownloadFileRequest(fileId, pin));
            if (!response.IsSuccessStatusCode) { LastError = $"Download failed: {(int)response.StatusCode}"; return null; }
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex) { LastError = ex.Message; return null; }
    }
    public async Task<bool> DeleteFileAsync(Guid fileId) => await DeleteRequest($"api/files/{fileId}");

    // ── Chat / Messaging ──
    public async Task<List<ChatChannelDto>?> GetChatChannelsAsync() => await GetJson<List<ChatChannelDto>>("api/chat/channels");
    public async Task<List<ChatMessageDto>?> GetChatMessagesAsync(string channel, int take = 50)
        => await GetJson<List<ChatMessageDto>>($"api/chat/messages/{channel}?take={take}");
    public async Task<ChatMessageDto?> SendChatMessageAsync(SendMessageRequest request) => await PostJson<ChatMessageDto>("api/chat/send", request);
    public async Task<bool> DeleteChatMessageAsync(Guid messageId) => await DeleteRequest($"api/chat/{messageId}");

    // ── Appointments ──
    public async Task<List<AppointmentDto>?> GetAppointmentsAsync(string? range = null)
        => await GetJson<List<AppointmentDto>>(range != null ? $"api/appointments?range={range}" : "api/appointments");
    public async Task<AppointmentDto?> CreateAppointmentAsync(CreateAppointmentRequest request)
        => await PostJson<AppointmentDto>("api/appointments", request);
    public async Task<AppointmentDto?> UpdateAppointmentAsync(Guid id, UpdateAppointmentRequest request)
        => await PutJson<AppointmentDto>($"api/appointments/{id}", request);
    public async Task<bool> CancelAppointmentAsync(Guid id) => await DeleteRequest($"api/appointments/{id}");

    // ── Chat Extras ──
    public async Task<ChatMessageDto?> EditChatMessageAsync(Guid messageId, string content)
        => await PutJson<ChatMessageDto>($"api/chat/{messageId}", new EditMessageRequest(content));
    public async Task<List<ChatMessageDto>?> SearchChatMessagesAsync(string channel, string query)
        => await GetJson<List<ChatMessageDto>>($"api/chat/messages/{channel}?take=100");

    // ── Collaboration Notes ──
    public async Task<List<CollabNoteDto>?> GetNotesAsync() => await GetJson<List<CollabNoteDto>>("api/notes");
    public async Task<CollabNoteDto?> CreateNoteAsync(CreateNoteRequest request) => await PostJson<CollabNoteDto>("api/notes", request);
    public async Task<CollabNoteDto?> UpdateNoteAsync(UpdateNoteRequest request) => await PutJson<CollabNoteDto>("api/notes", request);
    public async Task<bool> DeleteNoteAsync(Guid noteId) => await DeleteRequest($"api/notes/{noteId}");

    // ── Task Board ──
    public async Task<List<TaskItemDto>?> GetTasksAsync(string? status = null)
        => await GetJson<List<TaskItemDto>>(status != null ? $"api/tasks?status={status}" : "api/tasks");
    public async Task<TaskBoardSummary?> GetTaskSummaryAsync() => await GetJson<TaskBoardSummary>("api/tasks/summary");
    public async Task<TaskItemDto?> CreateTaskAsync(CreateTaskRequest request) => await PostJson<TaskItemDto>("api/tasks", request);
    public async Task<TaskItemDto?> UpdateTaskAsync(UpdateTaskRequest request) => await PutJson<TaskItemDto>("api/tasks", request);
    public async Task<bool> DeleteTaskAsync(Guid taskId) => await DeleteRequest($"api/tasks/{taskId}");

    // ── Announcements ──
    public async Task<List<AnnouncementDto>?> GetAnnouncementsAsync() => await GetJson<List<AnnouncementDto>>("api/announcements");
    public async Task<AnnouncementDto?> CreateAnnouncementAsync(CreateAnnouncementRequest request) => await PostJson<AnnouncementDto>("api/announcements", request);
    public async Task<bool> DeleteAnnouncementAsync(Guid id) => await DeleteRequest($"api/announcements/{id}");

    // ── Dashboard Stats (#14) ──
    public async Task<DashboardStatsDto?> GetDashboardStatsAsync() => await GetJson<DashboardStatsDto>("api/tenant/stats");

    // ── Export (#16) ──
    public async Task<byte[]?> ExportTasksCsvAsync()
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.GetAsync("api/tenant/export/tasks");
            if (!response.IsSuccessStatusCode) { LastError = $"Export failed: {(int)response.StatusCode}"; return null; }
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex) { LastError = ex.Message; return null; }
    }

    public async Task<byte[]?> ExportAuditCsvAsync()
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.GetAsync("api/tenant/export/audit");
            if (!response.IsSuccessStatusCode) { LastError = $"Export failed: {(int)response.StatusCode}"; return null; }
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex) { LastError = ex.Message; return null; }
    }

    // ── Helpers ──
    public string? LastError { get; set; }

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
                // Try to deserialize as T so callers get structured error (e.g. AuthResponse.Error)
                try
                {
                    var errorResult = await response.Content.ReadFromJsonAsync<T>();
                    if (errorResult != null) return errorResult;
                }
                catch { /* fallback to raw body */ }

                var body = await response.Content.ReadAsStringAsync();
                LastError = ExtractErrorMessage(body, (int)response.StatusCode);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Cannot reach API at {_baseUrl}: {ex.Message}";
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

    /// <summary>Extract a human-readable error from a JSON response body.</summary>
    private static string ExtractErrorMessage(string body, int statusCode)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err) && err.GetString() is string msg)
                return msg;
        }
        catch { /* not JSON or no error field */ }
        return $"Server returned {statusCode}";
    }

    private static bool IsAuthEndpoint(string url) =>
        url.Contains("auth/login") || url.Contains("auth/register") ||
        url.Contains("auth/accept-invite") || url.Contains("auth/refresh");

    private async Task<T?> PostJson<T>(string url, object data)
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.PostAsJsonAsync(url, data);

            // Only attempt token refresh for non-auth endpoints.
            // Auth endpoints return 401 to mean "bad credentials", not "expired session".
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && !IsAuthEndpoint(url))
            {
                var refreshed = await RefreshTokenAsync();
                if (refreshed == null) { LastError = "Session expired — please log in again"; return default; }
                AttachHeaders();
                response = await _http.PostAsJsonAsync(url, data);
            }

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorResult = await response.Content.ReadFromJsonAsync<T>();
                    if (errorResult != null) return errorResult;
                }
                catch { /* fallback to raw body */ }

                var body = await response.Content.ReadAsStringAsync();
                LastError = ExtractErrorMessage(body, (int)response.StatusCode);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Cannot reach API at {_baseUrl}: {ex.Message}";
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

    private async Task<T?> PutJson<T>(string url, object data)
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.PutAsJsonAsync(url, data);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var refreshed = await RefreshTokenAsync();
                if (refreshed == null) { LastError = "Session expired — please log in again"; return default; }
                AttachHeaders();
                response = await _http.PutAsJsonAsync(url, data);
            }

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorResult = await response.Content.ReadFromJsonAsync<T>();
                    if (errorResult != null) return errorResult;
                }
                catch { /* fallback to raw body */ }

                var body = await response.Content.ReadAsStringAsync();
                LastError = ExtractErrorMessage(body, (int)response.StatusCode);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (HttpRequestException ex) { LastError = $"Cannot reach API at {_baseUrl}: {ex.Message}"; return default; }
        catch (TaskCanceledException) { LastError = "Request timed out — is the API running?"; return default; }
        catch (Exception ex) { LastError = ex.Message; return default; }
    }

    private async Task<bool> DeleteRequest(string url)
    {
        LastError = null;
        try
        {
            AttachHeaders();
            var response = await _http.DeleteAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var refreshed = await RefreshTokenAsync();
                if (refreshed == null) { LastError = "Session expired — please log in again"; return false; }
                AttachHeaders();
                response = await _http.DeleteAsync(url);
            }

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Server returned {(int)response.StatusCode}";
                return false;
            }
            return true;
        }
        catch (HttpRequestException ex) { LastError = $"Cannot reach API at {_baseUrl}: {ex.Message}"; return false; }
        catch (TaskCanceledException) { LastError = "Request timed out — is the API running?"; return false; }
        catch (Exception ex) { LastError = ex.Message; return false; }
    }
}
