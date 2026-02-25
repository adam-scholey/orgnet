using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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
    // Disable automatic claim type mapping so "sub" stays as "sub" (not remapped to ClaimTypes.NameIdentifier)
    JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret must be configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Prevent the handler from remapping "sub" -> ClaimTypes.NameIdentifier etc.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "OrgNet",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "OrgNet.Clients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                // Tell the identity system which short claim names to use for Name and Role
                NameClaimType = "name",
                RoleClaimType = "role"
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

    // ── Rate Limiting ──
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // General API: 100 requests per 60s per IP
        options.AddFixedWindowLimiter("GeneralApi", opt =>
        {
            opt.Window = TimeSpan.FromSeconds(60);
            opt.PermitLimit = 100;
            opt.QueueLimit = 10;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        // Auth endpoints: 20 requests per 60s per IP (brute-force protection)
        options.AddFixedWindowLimiter("AuthEndpoints", opt =>
        {
            opt.Window = TimeSpan.FromSeconds(60);
            opt.PermitLimit = 20;
            opt.QueueLimit = 0;
        });

        // File uploads: 10 per 60s per IP
        options.AddFixedWindowLimiter("FileUploads", opt =>
        {
            opt.Window = TimeSpan.FromSeconds(60);
            opt.PermitLimit = 10;
            opt.QueueLimit = 2;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        // Invite creation: 50 per hour per IP
        options.AddFixedWindowLimiter("InviteCreation", opt =>
        {
            opt.Window = TimeSpan.FromHours(1);
            opt.PermitLimit = 50;
            opt.QueueLimit = 0;
        });

        options.OnRejected = async (context, ct) =>
        {
            Log.Warning("Rate limit exceeded for {Path} from {IP}",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress);
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\":\"Too many requests. Please try again later.\"}", ct);
        };
    });

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
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrgNetDbContext>();
        try
        {
            db.Database.EnsureCreated();

            // Create any tables that were added after the initial EnsureCreated.
            // EnsureCreated only works on a brand-new database; this handles schema drift.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Invitations" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "Email" character varying(256) NOT NULL,
                    "Role" integer NOT NULL DEFAULT 0,
                    "Token" character varying(128) NOT NULL,
                    "InvitedByUserId" uuid NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "ExpiresAt" timestamp with time zone NOT NULL,
                    "AcceptedAt" timestamp with time zone,
                    CONSTRAINT "PK_Invitations" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_Invitations_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Invitations_Users_InvitedByUserId" FOREIGN KEY ("InvitedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Invitations_Token" ON "Invitations" ("Token");
                CREATE INDEX IF NOT EXISTS "IX_Invitations_TenantId_Email" ON "Invitations" ("TenantId", "Email");
            """);

            // Appointments table
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Appointments" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "CreatedByUserId" uuid NOT NULL,
                    "AssignedToUserId" uuid,
                    "Title" character varying(300) NOT NULL,
                    "Description" character varying(2000),
                    "Location" character varying(300),
                    "StartsAt" timestamp with time zone NOT NULL,
                    "EndsAt" timestamp with time zone NOT NULL,
                    "IsCancelled" boolean NOT NULL DEFAULT false,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "ModifiedAt" timestamp with time zone,
                    CONSTRAINT "PK_Appointments" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_Appointments_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_Appointments_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_Appointments_Users_AssignedToUserId" FOREIGN KEY ("AssignedToUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Appointments_TenantId_StartsAt_EndsAt" ON "Appointments" ("TenantId", "StartsAt", "EndsAt");
            """);

            // UserModuleAccess table
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "UserModuleAccess" (
                    "Id" uuid NOT NULL,
                    "TenantId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "ModuleId" character varying(200) NOT NULL,
                    "IsGranted" boolean NOT NULL DEFAULT true,
                    "GrantedByUserId" uuid NOT NULL,
                    "GrantedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    CONSTRAINT "PK_UserModuleAccess" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_UserModuleAccess_Tenants" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_UserModuleAccess_Users" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_UserModuleAccess_GrantedBy" FOREIGN KEY ("GrantedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserModuleAccess_Tenant_User_Module" ON "UserModuleAccess" ("TenantId", "UserId", "ModuleId");
            """);

            // Add new columns to UserDevices if they don't exist
            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    ALTER TABLE "UserDevices" ADD COLUMN IF NOT EXISTS "LastIpAddress" text;
                    ALTER TABLE "UserDevices" ADD COLUMN IF NOT EXISTS "RiskScore" integer NOT NULL DEFAULT 0;
                    ALTER TABLE "UserDevices" ADD COLUMN IF NOT EXISTS "RevokedReason" text;
                EXCEPTION WHEN duplicate_column THEN NULL;
                END $$;
            """);

            // Add Sector column to Tenants if it doesn't exist
            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Sector" integer NOT NULL DEFAULT 0;
                EXCEPTION WHEN duplicate_column THEN NULL;
                END $$;
            """);

            Log.Information("Database ready");
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
    app.UseRateLimiter();
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
