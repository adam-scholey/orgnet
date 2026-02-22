using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrgNet.Infrastructure.Auth;
using OrgNet.Infrastructure.Data;
using OrgNet.Infrastructure.Eventing;
using OrgNet.Infrastructure.Multitenancy;
using OrgNet.Infrastructure.Plugins;
using OrgNet.Infrastructure.Services;
using OrgNet.Shared.Interfaces;
using StackExchange.Redis;

namespace OrgNet.Infrastructure;

/// <summary>
/// Clean Architecture: Infrastructure layer registers its own services.
/// The API layer calls this single method — no leaky abstractions.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddOrgNetInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // ── PostgreSQL + EF Core ──
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured");

        services.AddDbContext<OrgNetDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(OrgNetDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            }));

        // ── Multi-tenancy ──
        services.AddScoped<ITenantContext, TenantContext>();

        // ── Auth ──
        services.AddSingleton<TokenService>();
        services.AddScoped<AuthService>();

        // ── Services ──
        services.AddScoped<AuditService>();
        services.AddScoped<DeviceService>();

        // ── Redis (optional — graceful fallback if not configured) ──
        var redisConnection = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            try
            {
                var redis = ConnectionMultiplexer.Connect(redisConnection);
                services.AddSingleton<IConnectionMultiplexer>(redis);
            }
            catch
            {
                services.AddSingleton<IConnectionMultiplexer>(sp => null!);
            }
        }
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(sp => null!);
        }

        services.AddSingleton<IEventBus, RedisEventBus>();

        // ── Plugin Loader ──
        services.AddSingleton<PluginLoader>();

        return services;
    }
}
