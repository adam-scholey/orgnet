# OrgNet — Multi-Tenant Organisational Digital Operating System

A Windows-native, enterprise-grade platform for managing organisations with real-time communication, plugin-based extensibility, and secure multi-tenant isolation.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     OrgNet.Desktop (WinUI 3)                    │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │  MVVM    │ │ SignalR   │ │ SQLite   │ │ Plugin Loader     │  │
│  │ ViewModels│ │ Client   │ │ Cache    │ │ (Dynamic DLLs)    │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │Credential│ │ Heartbeat│ │ WebView2 │ │ Navigation        │  │
│  │ Locker   │ │ Service  │ │ Embedded │ │ Service           │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTPS + WSS (JWT)
┌───────────────────────────▼─────────────────────────────────────┐
│                    OrgNet.Api (ASP.NET Core 10)                 │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │ Tenant   │ │ JWT Auth │ │ SignalR   │ │ Security Headers  │  │
│  │Middleware│ │ + Refresh│ │ Hub       │ │ Middleware        │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │Controllers│ │ Audit   │ │ Device   │ │ Plugin Loader     │  │
│  │ (REST)   │ │ Service  │ │ Trust    │ │ (Server-side)     │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│                  OrgNet.Infrastructure                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │ EF Core  │ │ BCrypt   │ │ Redis    │ │ Token Service     │  │
│  │ DbContext│ │ Hashing  │ │ EventBus │ │ (JWT + Refresh)   │  │
│  │ +Filters │ │          │ │ Pub/Sub  │ │                   │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
              ┌─────────────┴──────────────┐
              │  PostgreSQL    Redis        │
              │  (Row-level    (Cache +     │
              │   tenancy)     Pub/Sub)     │
              └────────────────────────────┘
```

## Key Architectural Decisions

| Decision | Rationale |
|---|---|
| **Row-level tenancy** (single DB) | Simpler ops, cross-tenant reporting, EF global query filters enforce isolation |
| **BCrypt** (work factor 12) | GPU brute-force resistant; auto-upgrades legacy hashes on login |
| **Refresh token rotation** | Single-use tokens with replay detection; revokes entire family on reuse |
| **Windows Credential Locker** | DPAPI-backed secure storage; no plaintext tokens on disk |
| **Redis pub/sub** over RabbitMQ | Fire-and-forget for real-time UI; upgrade path to MassTransit available |
| **Plugin AssemblyLoadContext** | Isolated loading; collectible contexts for hot-reload potential |
| **SQLite local cache** | Offline-first rendering; queryable, transactional, consistent with EF Core |

## Solution Structure

```
OrgNet/
├── src/
│   ├── OrgNet.Shared/          # Enums, DTOs, interfaces, constants
│   ├── OrgNet.Domain/          # Entity models (Tenant, User, Device, etc.)
│   ├── OrgNet.Infrastructure/  # EF Core, Auth, Redis, Plugin Loader
│   ├── OrgNet.Api/             # ASP.NET Core Web API + SignalR
│   └── OrgNet.Desktop/         # WinUI 3 MVVM desktop client
├── plugins/
│   └── OrgNet.Plugin.SampleChat/  # Example plugin
├── tests/
│   └── OrgNet.Tests/
├── docker-compose.yml
└── OrgNet.sln
```

## Database Schema (Multi-Tenant)

All tenant-scoped tables include a `TenantId` column with EF Core global query filters:

- **Tenants** — Organisation root (Name, Slug, Plan)
- **Users** — Tenant-scoped (Email, PasswordHash, Role)
- **RefreshTokens** — Single-use with rotation chain
- **UserDevices** — Fingerprint-based trust validation
- **TenantModules** — Per-tenant module activation
- **ServiceRegistrations** — Per-tenant service registry
- **AuditLogs** — Immutable append-only audit trail

## Quick Start

### Prerequisites
- .NET 10 SDK
- PostgreSQL 16+
- Redis 7+ (optional)
- Windows 10/11 (for Desktop client)

### Run with Docker (API + dependencies)
```bash
docker-compose up -d
```

### Run API locally
```bash
cd src/OrgNet.Api
dotnet run
```
API available at `http://localhost:5100`

### Run Desktop client
```bash
cd src/OrgNet.Desktop
dotnet run
```

## API Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register-org` | No | Register new organisation |
| POST | `/api/auth/login` | Header | Login (requires X-Tenant-Id) |
| POST | `/api/auth/refresh` | No | Refresh token rotation |
| POST | `/api/auth/revoke` | Yes | Revoke refresh token |
| GET | `/api/auth/me` | Yes | Current user profile |
| GET | `/api/tenant` | Yes | Tenant info |
| PUT | `/api/tenant` | Owner/Admin | Update tenant |
| GET | `/api/tenant/members` | Yes | List members |
| GET | `/api/modules` | Yes | List modules |
| POST | `/api/modules/toggle` | Owner/Admin | Enable/disable module |
| GET | `/api/devices` | Yes | List user devices |
| POST | `/api/devices/register` | Yes | Register device |
| PUT | `/api/devices/{id}/trust` | Owner/Admin | Trust device |
| PUT | `/api/devices/{id}/revoke` | Owner/Admin | Revoke device |
| GET | `/api/audit` | Owner/Admin | Audit logs |
| GET | `/health` | No | Health check |

## Security Features

- **BCrypt password hashing** (work factor 12)
- **JWT access tokens** (15-min expiry, 30s clock skew)
- **Refresh token rotation** with replay attack detection
- **Security headers** (CSP, X-Frame-Options, HSTS, etc.)
- **Input validation** on all endpoints
- **Row-level tenant isolation** via EF global query filters
- **Device trust validation** with fingerprint verification
- **Audit logging** for all significant actions
- **Windows Credential Locker** for secure token storage

## Plugin System

Plugins implement `IOrgNetPlugin`:

```csharp
public interface IOrgNetPlugin
{
    string ModuleId { get; }
    string Name { get; }
    string Description { get; }
    string Version { get; }
    string? IconUrl { get; }
    void ConfigureServices(IServiceCollection services);
    Task InitialiseAsync(IServiceProvider serviceProvider);
}
```

Place plugin DLLs in the `plugins/` directory. They are discovered and loaded at startup.

## Tech Stack

- **Backend**: ASP.NET Core 10, EF Core, PostgreSQL, Redis, SignalR, Serilog
- **Desktop**: WinUI 3, CommunityToolkit.Mvvm, WebView2, SQLite
- **Security**: BCrypt.Net, JWT Bearer, Windows Credential Locker
- **Infrastructure**: Docker, docker-compose
