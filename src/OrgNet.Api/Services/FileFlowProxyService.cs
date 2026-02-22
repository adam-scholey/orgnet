using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrgNet.Api.Services;

/// <summary>
/// Proxies requests from OrgNet to the CloudFileSystem (FileFlow) API.
/// Handles authentication bridging — OrgNet users get a FileFlow JWT
/// via org-signup or login, stored per-tenant in memory.
/// </summary>
public class FileFlowProxyService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<FileFlowProxyService> _logger;

    // Cache FileFlow JWT per OrgNet tenant ID
    private static readonly Dictionary<Guid, FileFlowSession> _sessions = new();

    public FileFlowProxyService(HttpClient http, IConfiguration config, ILogger<FileFlowProxyService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;

        var baseUrl = config["FileFlow:BaseUrl"] ?? "http://localhost:5155";
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool IsEnabled => _config.GetValue<bool>("FileFlow:Enabled");

    // ── Tenant Sync ──

    /// <summary>
    /// Creates a matching organisation in CloudFileSystem when one is registered in OrgNet.
    /// Returns the FileFlow tenant ID and admin token.
    /// </summary>
    public async Task<FileFlowOrgResult?> CreateOrganisationAsync(string orgName, string adminEmail, string password)
    {
        try
        {
            var request = new
            {
                organisationName = orgName,
                adminUsername = adminEmail,
                adminEmail = adminEmail,
                password = password
            };

            var response = await _http.PostAsJsonAsync("api/auth/org-signup", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("FileFlow org-signup failed: {Status} {Error}", response.StatusCode, error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = result.GetProperty("token").GetString();
            var tenantId = result.GetProperty("tenantId").GetInt32();
            var tenantName = result.TryGetProperty("tenantName", out var tn) ? tn.GetString() : orgName;

            return new FileFlowOrgResult(tenantId, tenantName ?? orgName, token ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create FileFlow organisation for {OrgName}", orgName);
            return null;
        }
    }

    /// <summary>
    /// Login to CloudFileSystem to get a session token for proxied requests.
    /// </summary>
    public async Task<string?> LoginAsync(Guid orgNetTenantId, string email, string password)
    {
        try
        {
            var request = new { username = email, password = password };
            var response = await _http.PostAsJsonAsync("api/auth/login", request);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = result.GetProperty("token").GetString();

            if (token != null)
            {
                _sessions[orgNetTenantId] = new FileFlowSession(token, DateTime.UtcNow.AddDays(30));
            }

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileFlow login failed for tenant {TenantId}", orgNetTenantId);
            return null;
        }
    }

    // ── File Operations ──

    public async Task<JsonElement?> GetFilesAsync(Guid orgNetTenantId)
    {
        var token = GetToken(orgNetTenantId);
        if (token == null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/files");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileFlow GetFiles failed for tenant {TenantId}", orgNetTenantId);
            return null;
        }
    }

    public async Task<JsonElement?> UploadFileAsync(Guid orgNetTenantId, string fileName, string base64Content, string encryptionPin)
    {
        var token = GetToken(orgNetTenantId);
        if (token == null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/upload");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { fileName, base64Content, encryptionPin });

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileFlow UploadFile failed for tenant {TenantId}", orgNetTenantId);
            return null;
        }
    }

    public async Task<byte[]?> DownloadFileAsync(Guid orgNetTenantId, int fileId, string encryptionPin)
    {
        var token = GetToken(orgNetTenantId);
        if (token == null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/files/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { fileId, encryptionPin });

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileFlow DownloadFile failed for tenant {TenantId}", orgNetTenantId);
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(Guid orgNetTenantId, int fileId)
    {
        var token = GetToken(orgNetTenantId);
        if (token == null) return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/files/{fileId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FileFlow DeleteFile failed for tenant {TenantId}", orgNetTenantId);
            return false;
        }
    }

    // ── Helpers ──

    public void StoreSession(Guid orgNetTenantId, string token)
    {
        _sessions[orgNetTenantId] = new FileFlowSession(token, DateTime.UtcNow.AddDays(30));
    }

    public bool HasSession(Guid orgNetTenantId) => _sessions.ContainsKey(orgNetTenantId);

    private string? GetToken(Guid orgNetTenantId)
    {
        if (_sessions.TryGetValue(orgNetTenantId, out var session) && session.ExpiresAt > DateTime.UtcNow)
            return session.Token;
        return null;
    }
}

public record FileFlowOrgResult(int FileFlowTenantId, string TenantName, string Token);
public record FileFlowSession(string Token, DateTime ExpiresAt);
