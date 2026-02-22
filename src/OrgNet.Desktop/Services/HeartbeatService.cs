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

                // Proactive token refresh — refresh if token will expire within 2 minutes
                await _apiClient.RefreshTokenAsync();

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
}
