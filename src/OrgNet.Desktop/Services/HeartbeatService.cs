using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Background IHostedService that runs a periodic heartbeat and sync cycle.
/// 
/// Architectural decisions:
/// - Runs on a 30-second interval while the app is open.
/// - Refreshes the JWT access token before expiry (proactive, not reactive).
/// - Syncs tenant metadata to local SQLite cache for offline-first rendering.
/// - Reports device "last seen" to the backend for trust validation.
/// - Uses IHostedService so it starts/stops with the application lifecycle.
/// </summary>
public class HeartbeatService : BackgroundService
{
    private readonly ICredentialStore _credentials;
    private readonly OrgNetApiClient _apiClient;
    private readonly LocalCacheService _cache;
    private readonly ILogger<HeartbeatService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(2);

    public HeartbeatService(ICredentialStore credentials, OrgNetApiClient apiClient,
        LocalCacheService cache, ILogger<HeartbeatService> logger)
    {
        _credentials = credentials;
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);

                var token = _credentials.GetAccessToken();
                if (string.IsNullOrEmpty(token))
                    continue;

                // Only refresh if the access token is close to expiry
                if (IsTokenExpiringSoon(token))
                {
                    _logger.LogDebug("Access token expiring soon — refreshing");
                    await _apiClient.RefreshTokenAsync();
                }

                // Sync tenant info to local cache
                var tenant = await _apiClient.GetTenantAsync();
                if (tenant != null)
                {
                    await _cache.SetAsync("tenant_name", tenant.Name);
                    await _cache.SetAsync("tenant_plan", tenant.Plan);
                    await _cache.SetAsync("tenant_members", tenant.MemberCount.ToString());
                }

                _logger.LogDebug("Heartbeat sync completed");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat cycle failed — will retry");
            }
        }

        _logger.LogInformation("HeartbeatService stopped");
    }

    /// <summary>
    /// Parse the JWT payload to check if the "exp" claim is within RefreshThreshold of now.
    /// Returns true if the token is about to expire or cannot be parsed.
    /// </summary>
    private bool IsTokenExpiringSoon(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1];
            // Fix base64url padding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            payload = payload.Replace('-', '+').Replace('_', '/');

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expProp))
            {
                var exp = DateTimeOffset.FromUnixTimeSeconds(expProp.GetInt64());
                return exp - DateTimeOffset.UtcNow < RefreshThreshold;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }
}
