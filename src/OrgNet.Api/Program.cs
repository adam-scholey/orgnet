using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrgNet.Api.Hubs;
using OrgNet.Api.Middleware;
using OrgNet.Infrastructure;
using OrgNet.Infrastructure.Data;
using OrgNet.Infrastructure.Plugins;
using Serilog;

// ── Bootstrap Serilog ──
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .Enrich.FromLogContext());

    // ── Infrastructure layer (EF, Redis, Auth, Plugins) ──
    builder.Services.AddOrgNetInfrastructure(builder.Configuration);

    // ── JWT Authentication ──
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret must be configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "OrgNet",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "OrgNet.Clients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Allow SignalR to receive the JWT via query string
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // ── CORS ──
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("OrgNetClients", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                ?? ["http://localhost:5173", "http://localhost:5000"];
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // ── Controllers + SignalR ──
    builder.Services.AddControllers();
    builder.Services.AddSignalR()
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.PropertyNamingPolicy = null;
        });

    // ── Health Checks ──
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<OrgNetDbContext>("database");

    // ── Plugin Discovery ──
    var pluginLoader = new PluginLoader(
        LoggerFactory.Create(lb => lb.AddConsole()).CreateLogger<PluginLoader>());

    var pluginDir = builder.Configuration["Plugins:Directory"]
        ?? Path.Combine(AppContext.BaseDirectory, "plugins");
    pluginLoader.DiscoverPlugins(pluginDir);
    pluginLoader.RegisterPluginServices(builder.Services);
    builder.Services.AddSingleton(pluginLoader);

    var app = builder.Build();

    // ── Database Initialisation ──
    // In development, recreate the database to pick up schema changes.
    // In production, use EF Core migrations instead.
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrgNetDbContext>();
        try
        {
            if (app.Environment.IsDevelopment())
            {
                // Drop and recreate to ensure schema matches the model
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
                Log.Information("Database recreated (development mode)");
            }
            else
            {
                db.Database.EnsureCreated();
                Log.Information("Database ready");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database initialisation failed");
            throw;
        }
    }

    // ── Plugin Initialisation ──
    await pluginLoader.InitialisePluginsAsync(app.Services);

    // ── Middleware Pipeline ──
    app.UseSerilogRequestLogging();
    app.UseCors("OrgNetClients");
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseAuthentication();
    app.UseMiddleware<TenantMiddleware>();
    app.UseAuthorization();

    // ── Endpoints ──
    app.MapControllers();
    app.MapHub<OrgNetHub>("/hubs/orgnet");
    app.MapHealthChecks("/health");

    Log.Information("OrgNet API starting on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrgNet API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
