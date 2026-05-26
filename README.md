# OrgNet — Multi-Tenant Organisational Digital Workspace

A Windows-native, enterprise-grade platform for managing organisations with real-time communication, encrypted file storage, task management, collaborative notes, appointments, announcements, and plugin-based extensibility — all with secure multi-tenant isolation.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Solution Structure](#solution-structure)
- [Tech Stack](#tech-stack)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Running in a Virtual Machine](#running-in-a-virtual-machine)
- [Database Schema](#database-schema)
- [API Endpoints](#api-endpoints)
- [Built-in Modules](#built-in-modules)
- [Security Features](#security-features)
- [Plugin System](#plugin-system)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Overview

OrgNet is a self-hosted digital workspace that provides organisations with a complete suite of productivity tools, all scoped to a multi-tenant architecture. Each organisation gets an isolated environment with:

- **Real-time chat** with channels, message editing/deletion, and SignalR broadcasting
- **Encrypted file vault** with AES-256 encryption and per-file initialisation vectors
- **Collaborative notes** with edit tracking and pinning
- **Task board** (Kanban-style: Todo / In Progress / Review / Done)
- **Appointments** with attendee management and calendar views
- **Announcements** with pinning and auto-expiry
- **Device trust management** with fingerprint verification
- **Audit logging** for compliance and accountability
- **Plugin system** for extensibility via dynamic DLL loading

The platform consists of a **REST API** (ASP.NET Core 10) and a **native Windows desktop client** (WinUI 3) connected via HTTP and SignalR WebSockets.

---

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
                            │ HTTP + WSS (JWT Bearer)
┌───────────────────────────▼─────────────────────────────────────┐
│                    OrgNet.Api (ASP.NET Core 10)                 │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │ Tenant   │ │ JWT Auth │ │ SignalR   │ │ Security Headers  │  │
│  │Middleware│ │ + Refresh│ │ Hub       │ │ Middleware        │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │11 REST   │ │ Audit    │ │ Device   │ │ Plugin Loader     │  │
│  │Controllers│ │ Service  │ │ Trust    │ │ (Server-side)     │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │ EF Core 10
┌───────────────────────────▼─────────────────────────────────────┐
│                  OrgNet.Infrastructure                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────────────┐  │
│  │ EF Core  │ │ BCrypt   │ │ Redis    │ │ Token Service     │  │
│  │ DbContext│ │ Hashing  │ │ EventBus │ │ (JWT + Refresh)   │  │
│  │ +Filters │ │ (wf 12)  │ │ Pub/Sub  │ │ + Rotation        │  │
│  └──────────┘ └──────────┘ └──────────┘ └───────────────────┘  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
              ┌─────────────┴──────────────┐
              │  PostgreSQL    Redis        │
              │  (Row-level    (Cache +     │
              │   tenancy)     Pub/Sub)     │
              └────────────────────────────┘
```

### Key Architectural Decisions

| Decision | Rationale |
|---|---|
| **Row-level tenancy** (single DB) | Simpler ops, cross-tenant reporting, EF global query filters enforce isolation |
| **BCrypt** (work factor 12) | GPU brute-force resistant; auto-upgrades legacy hashes on login |
| **Refresh token rotation** | Single-use tokens with replay detection; revokes entire family on reuse |
| **Windows Credential Locker** | DPAPI-backed secure storage; no plaintext tokens on disk |
| **AES-256 file encryption** | Per-file IV, encrypted at rest in database, decrypted on download |
| **Redis pub/sub** over RabbitMQ | Fire-and-forget for real-time UI; upgrade path to MassTransit available |
| **Plugin AssemblyLoadContext** | Isolated loading; collectible contexts for hot-reload potential |
| **SQLite local cache** | Offline-first rendering; queryable, transactional, consistent with EF Core |
| **Proactive token refresh** | HeartbeatService refreshes JWT before expiry, preventing session drops |

---

## Solution Structure

```
OrgNet/
├── src/
│   ├── OrgNet.Shared/              # Enums, DTOs, interfaces, constants
│   ├── OrgNet.Domain/              # Entity models (Tenant, User, Device, etc.)
│   ├── OrgNet.Infrastructure/      # EF Core, Auth, Redis, Plugin Loader
│   ├── OrgNet.Api/                 # ASP.NET Core Web API + SignalR
│   │   ├── Controllers/            # 11 REST controllers
│   │   ├── Middleware/              # Tenant, Security Headers
│   │   ├── Hubs/                   # SignalR real-time hub
│   │   └── Program.cs              # App startup & configuration
│   └── OrgNet.Desktop/             # WinUI 3 MVVM desktop client
│       ├── Views/                   # XAML pages (Shell, Login, Dashboard, etc.)
│       ├── ViewModels/              # MVVM ViewModels with CommunityToolkit
│       └── Services/                # API client, SignalR, Credentials, Cache
├── plugins/
│   └── OrgNet.Plugin.SampleChat/   # Example plugin
├── tests/
│   ├── OrgNet.Tests/               # xUnit unit tests
│   └── k6/                         # Performance & load tests
├── docker-compose.yml              # PostgreSQL + Redis + API containers
├── requirements.txt                # Full dependency listing
└── OrgNet.slnx                     # Solution file
```

---

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| **Runtime** | .NET | 10.0 |
| **API Framework** | ASP.NET Core | 10.0 |
| **Desktop Framework** | WinUI 3 (Windows App SDK) | 1.x |
| **ORM** | Entity Framework Core | 10.0 |
| **Database** | PostgreSQL | 16+ |
| **Cache / Pub-Sub** | Redis | 7+ |
| **Real-time** | SignalR | 10.0 |
| **MVVM Toolkit** | CommunityToolkit.Mvvm | 8.4.0 |
| **Password Hashing** | BCrypt.Net-Next | 4.1.0 |
| **JWT** | System.IdentityModel.Tokens.Jwt | 8.16.0 |
| **Logging** | Serilog | 10.0 |
| **Testing** | xUnit + k6 | 2.9.3 / latest |
| **Containerisation** | Docker + docker-compose | latest |

---

## Requirements

### System Prerequisites

| Requirement | Details |
|---|---|
| **.NET 10 SDK** | Download from https://dotnet.microsoft.com/download/dotnet/10.0 |
| **Windows 10 (Build 19041)** or later | Required for the WinUI 3 desktop client |
| **PostgreSQL 16+** | Primary database — install locally or use Docker |
| **Redis 7+** | Optional — used for real-time event bus and caching |
| **Docker Desktop** | Optional — for containerised setup via docker-compose |
| **Git** | For cloning the repository |

> **Note**: All NuGet package dependencies are listed in `requirements.txt` and are restored automatically by `dotnet restore`.

### Hardware Recommendations

| Component | Minimum | Recommended |
|---|---|---|
| **CPU** | 2 cores | 4+ cores |
| **RAM** | 4 GB | 8+ GB |
| **Disk** | 2 GB free | 5+ GB free |
| **OS** | Windows 10 (19041) | Windows 11 |

---

## Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/AdamScholey25/orgnet.git
cd orgnet
```

### 2. Restore NuGet Packages

```bash
dotnet restore
```

### 3. Set Up the Database

**Option A — Docker (recommended)**:
```bash
docker-compose up -d postgres redis
```
This starts PostgreSQL on port `5432` and Redis on port `6379` with default credentials.

**Option B — Local PostgreSQL**:
1. Install PostgreSQL 16+ from https://www.postgresql.org/download/
2. Create a database called `orgnet`:
   ```sql
   CREATE DATABASE orgnet;
   ```
3. Configure the connection string via user secrets:
   ```bash
   cd src/OrgNet.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=orgnet;Username=postgres;Password=YOUR_PASSWORD"
   ```

### 4. Configure JWT Secrets (first run)

```bash
cd src/OrgNet.Api
dotnet user-secrets set "Jwt:Secret" "YourSuperSecretKey-Must-Be-At-Least-32-Characters-Long!!"
dotnet user-secrets set "Jwt:Issuer" "OrgNet"
dotnet user-secrets set "Jwt:Audience" "OrgNet.Clients"
```

### 5. Run the API

```bash
dotnet run --project src/OrgNet.Api
```
The API will start on **http://localhost:5100**. The database schema is created automatically on first run via EF Core auto-migration.

### 6. Run the Desktop Client

In a separate terminal:
```bash
dotnet run --project src/OrgNet.Desktop -f net10.0-windows10.0.19041.0
```

### 7. First Use

1. The desktop client opens with a **Login** screen.
2. Click **Register** to create a new organisation.
3. Enter your organisation name, display name, email, and password.
4. You are logged in as the **Owner** with full admin access.
5. Navigate to **Settings > Invite** to invite team members via email link.

---

## Running in a Virtual Machine

OrgNet can be tested in a Windows VM for isolated evaluation, CI/CD, or demonstration purposes.

### Automated Setup (Recommended)

A PowerShell script `setup.ps1` is included in the root of the repository. It automates the entire setup:

- Checks Windows version compatibility
- Installs .NET 10 SDK, Git, and Docker Desktop via `winget`
- Restores all NuGet packages
- Starts PostgreSQL and Redis via `docker-compose`
- Configures all required user secrets
- Builds both the API and Desktop projects

**Run inside your VM (as Administrator):**
```powershell
# 1. Clone the repo
git clone https://github.com/AdamScholey25/orgnet.git
cd orgnet

# 2. Allow the script to run (one-time for this session)
Set-ExecutionPolicy Bypass -Scope Process -Force

# 3. Run setup
.\setup.ps1
```

**Optional parameters** (use if your PostgreSQL password is different):
```powershell
.\setup.ps1 -DbPassword "MyPassword" -JwtSecret "MyCustomSecretKey-Min-32-Chars!!"
```

After setup completes, follow the on-screen instructions to start the API and Desktop client.

> **Note**: If Docker Desktop is installed for the first time by the script, you may need to **restart the VM** and re-run `.\setup.ps1` to complete the Docker setup and start the containers.

---

### Option 1 — Hyper-V (Windows Pro/Enterprise)

1. **Enable Hyper-V**:
   ```powershell
   Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All
   ```
   Restart when prompted.

2. **Create a Windows VM**:
   - Open **Hyper-V Manager** > **New** > **Virtual Machine**
   - Allocate at least **4 GB RAM** and **2 virtual CPUs**
   - Install **Windows 10 (Build 19041+)** or **Windows 11**
   - Enable **Enhanced Session Mode** for clipboard/file sharing

3. **Inside the VM** — clone the repo and run `setup.ps1` (see Automated Setup above).

### Option 2 — VirtualBox (Free, any Windows edition)

1. **Download VirtualBox**: https://www.virtualbox.org/wiki/Downloads
2. **Download a Windows VM image**: https://developer.microsoft.com/en-us/windows/downloads/virtual-machines/
3. **VM Settings**:
   - **RAM**: 4096 MB minimum (8192 recommended)
   - **CPUs**: 2 minimum
   - **Video Memory**: 128 MB
   - **Storage**: 40 GB dynamically allocated
   - **Network**: NAT (default) — API and desktop both run on localhost inside the VM
   - Enable **3D Acceleration** for WinUI 3 rendering
   - Install **VirtualBox Guest Additions** for shared folders and clipboard
4. **Inside the VM** — clone the repo and run `setup.ps1` (see Automated Setup above).

### Option 3 — VMware Workstation Player (Free for personal use)

1. **Download VMware Player**: https://www.vmware.com/products/workstation-player.html
2. Create a VM with **Windows 10/11**, allocate 4+ GB RAM and 2+ CPUs.
3. Install **VMware Tools** for better graphics and shared folders.
4. **Inside the VM** — clone the repo and run `setup.ps1` (see Automated Setup above).

### VM Network Notes

- Both the API (`localhost:5100`) and Desktop client run **inside the VM** — no port forwarding needed.
- If you want to access the API from the **host machine**, configure port forwarding:
  - **Hyper-V**: Use an External Virtual Switch
  - **VirtualBox**: Settings > Network > Port Forwarding > Host `5100` → Guest `5100`
  - **VMware**: NAT Settings > Port Forwarding > Host `5100` → Guest `5100`
- PostgreSQL and Redis also run inside the VM (via Docker or local install).

### VM Testing Checklist

- [ ] API starts on `http://localhost:5100`
- [ ] Health check responds: `GET http://localhost:5100/health`
- [ ] Register a new organisation via the desktop client
- [ ] Login with the registered credentials
- [ ] Send a chat message and verify real-time delivery
- [ ] Upload a file to the encrypted file vault
- [ ] Create a task on the task board
- [ ] Create a collaborative note
- [ ] Create an appointment
- [ ] Post an announcement
- [ ] Invite a user and accept the invite via browser link
- [ ] Verify audit log entries

---

## Database Schema

All tenant-scoped tables include a `TenantId` column with EF Core global query filters for automatic isolation.

### Core Tables

| Table | Purpose |
|---|---|
| **Tenants** | Organisation root (Name, Slug, Plan, CreatedAt) |
| **Users** | Tenant-scoped user accounts (Email, PasswordHash, Role, DisplayName) |
| **RefreshTokens** | Single-use JWT refresh tokens with rotation chain and replay detection |
| **UserDevices** | Device fingerprint-based trust validation |
| **TenantModules** | Per-tenant module activation flags |
| **UserModuleAccess** | Per-user module access overrides |
| **ServiceRegistrations** | Per-tenant service registry for extensibility |
| **AuditLogs** | Immutable append-only audit trail |

### Module Tables

| Table | Purpose |
|---|---|
| **OrgFiles** | AES-256 encrypted files with per-file IV, shared flag |
| **ChatMessages** | Real-time messages with channel support |
| **CollabNotes** | Shared documents with edit tracking and pinning |
| **TaskItems** | Kanban tasks (Todo/InProgress/Review/Done) with priority and assignee |
| **Appointments** | Calendar events with attendees, location, and notes |
| **Announcements** | Org-wide broadcasts with pinning and auto-expiry |
| **Invitations** | Email-based invite tokens with expiry |

---

## API Endpoints

### Authentication

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register-org` | No | Register new organisation (first user = Owner) |
| POST | `/api/auth/login` | Header | Login with X-Tenant-Id header |
| POST | `/api/auth/login-by-email` | No | Login by email (tenant auto-resolved) |
| POST | `/api/auth/refresh` | No | Refresh token rotation |
| POST | `/api/auth/revoke` | Yes | Revoke refresh token (logout) |
| GET | `/api/auth/me` | Yes | Current user profile |
| GET | `/api/auth/invite-info?token=` | No | View invitation details |
| GET | `/api/auth/accept-invite?token=` | No | Browser-friendly invite acceptance page |
| POST | `/api/auth/accept-invite` | No | Accept invitation (API) |

### Tenant Management

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/tenant` | Yes | Get tenant info |
| PUT | `/api/tenant` | Owner/Admin | Update tenant details |
| GET | `/api/tenant/members` | Yes | List all members |
| POST | `/api/tenant/invite` | Owner/Admin | Send email invitation |
| GET | `/api/tenant/invitations` | Owner/Admin | List all invitations |
| DELETE | `/api/tenant/invitations/{id}` | Owner/Admin | Revoke an invitation |

### Chat

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/chat/channels` | Yes | List channels |
| GET | `/api/chat/messages/{channel}` | Yes | Get messages in channel |
| GET | `/api/chat/messages/since?since=` | Yes | Get messages since timestamp |
| POST | `/api/chat/send` | Yes | Send a message |
| PUT | `/api/chat/{id}` | Yes | Edit own message |
| DELETE | `/api/chat/{id}` | Yes | Delete own message |

### Files (Encrypted Vault)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/files` | Yes | List files |
| POST | `/api/files/upload` | Yes | Upload file (base64) |
| POST | `/api/files/upload-multipart` | Yes | Upload file (multipart, up to 50 MB) |
| POST | `/api/files/download` | Yes | Download and decrypt file |
| DELETE | `/api/files/{id}` | Yes | Delete file |

### Notes

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/notes` | Yes | List notes |
| GET | `/api/notes/{id}` | Yes | Get note by ID |
| POST | `/api/notes` | Yes | Create note |
| PUT | `/api/notes` | Yes | Update note |
| DELETE | `/api/notes/{id}` | Yes | Delete note |

### Tasks

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/tasks` | Yes | List tasks (optional `?status=` filter) |
| GET | `/api/tasks/summary` | Yes | Get task board summary counts |
| POST | `/api/tasks` | Yes | Create task |
| PUT | `/api/tasks` | Yes | Update task |
| DELETE | `/api/tasks/{id}` | Yes | Delete task |

### Appointments

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/appointments` | Yes | List appointments (optional `?from=&to=` filter) |
| POST | `/api/appointments` | Yes | Create appointment |
| PUT | `/api/appointments/{id}` | Yes | Update appointment |
| DELETE | `/api/appointments/{id}` | Yes | Cancel appointment |

### Announcements

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/announcements` | Yes | List active announcements |
| POST | `/api/announcements` | Owner/Admin | Create announcement |
| DELETE | `/api/announcements/{id}` | Owner/Admin | Delete announcement |

### Modules & Devices

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/modules` | Yes | List modules |
| POST | `/api/modules/toggle` | Owner/Admin | Enable/disable module |
| GET | `/api/modules/user-access/{userId}` | Owner/Admin | Get user module access |
| POST | `/api/modules/user-access` | Owner/Admin | Set user module access |
| DELETE | `/api/modules/user-access/{id}` | Owner/Admin | Remove user module override |
| GET | `/api/devices` | Yes | List user devices |
| POST | `/api/devices/register` | Yes | Register device |
| PUT | `/api/devices/{id}/trust` | Owner/Admin | Trust device |
| PUT | `/api/devices/{id}/revoke` | Owner/Admin | Revoke device |
| GET | `/api/audit` | Owner/Admin | Query audit logs |
| GET | `/health` | No | Health check |

---

## Built-in Modules

| Module | Description | Desktop Page |
|---|---|---|
| **Chat** | Real-time messaging with channels, edit/delete, SignalR broadcast | ChatPage |
| **File Vault** | AES-256 encrypted file storage with upload/download | FileVaultPage |
| **Notes** | Collaborative documents with edit tracking and pinning | NotesPage |
| **Task Board** | Kanban board (Todo/InProgress/Review/Done) with priority and assignee | TaskBoardPage |
| **Appointments** | Calendar events with attendees, location, and notes | AppointmentsPage |
| **Announcements** | Org-wide broadcasts with pinning and expiry | AnnouncementsPage |

---

## Security Features

| Feature | Details |
|---|---|
| **Password Hashing** | BCrypt with work factor 12 — GPU brute-force resistant |
| **JWT Access Tokens** | 15-minute expiry, 30-second clock skew tolerance |
| **Refresh Token Rotation** | Single-use tokens with replay attack detection — revokes entire token family on reuse |
| **Security Headers** | CSP, X-Frame-Options, X-Content-Type-Options, HSTS, Referrer-Policy |
| **Input Validation** | Server-side validation on all endpoints with length and format checks |
| **Tenant Isolation** | Row-level multi-tenancy via EF Core global query filters — no cross-tenant data leakage |
| **Device Trust** | Fingerprint-based device registration and admin approval |
| **Audit Logging** | Immutable append-only log of all significant actions |
| **File Encryption** | AES-256 with unique IV per file — encrypted at rest in database |
| **Secure Token Storage** | Windows Credential Locker (DPAPI-backed) — no plaintext tokens on disk |
| **Rate Limiting** | Configurable per-endpoint rate limits (auth, invites, file uploads) |
| **Proactive Token Refresh** | Background service monitors JWT expiry and refreshes before it expires |

---

## Plugin System

Plugins implement the `IOrgNetPlugin` interface:

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

Place plugin DLLs in the `plugins/` directory. They are discovered and loaded at startup using isolated `AssemblyLoadContext` instances. Each plugin can register its own services and initialise resources.

---

## Testing

### Unit Tests (xUnit)

```bash
dotnet test tests/OrgNet.Tests/OrgNet.Tests.csproj
```

### Load Tests (k6)

Requires [k6](https://k6.io) installed:

```bash
# Standard load test
k6 run tests/k6/load-test.js

# Spike test (sudden traffic surge)
k6 run tests/k6/spike-test.js

# Stress test (find breaking point)
k6 run tests/k6/stress-test.js
```

### Manual API Testing (curl / PowerShell)

**Register an organisation:**
```bash
curl -X POST http://localhost:5100/api/auth/register-org \
  -H "Content-Type: application/json" \
  -d '{"organisationName":"Test Org","adminDisplayName":"Admin","email":"admin@test.com","password":"Password123!"}'
```

**Login:**
```bash
curl -X POST http://localhost:5100/api/auth/login-by-email \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@test.com","password":"Password123!"}'
```

**Health check:**
```bash
curl http://localhost:5100/health
```

### Build Verification

```bash
# Build all projects
dotnet build

# Build API only
dotnet build src/OrgNet.Api/OrgNet.Api.csproj

# Build Desktop only
dotnet build src/OrgNet.Desktop/OrgNet.Desktop.csproj
```

---

## Troubleshooting

| Issue | Solution |
|---|---|
| **"Session expired, please log in again"** | Old tokens cached — restart the desktop client. It validates tokens on startup and forces re-login if invalid. |
| **FK constraint error on invite** | Ensure the API is running the latest build. The JWT claim mapping fix ensures `UserId` resolves correctly. |
| **Invite link shows "Page not found"** | Ensure the API is running. The invite link format is `http://localhost:5100/api/auth/accept-invite?token=...` which serves an HTML page. |
| **Desktop client won't build** | Ensure you have .NET 10 SDK and are on Windows 10 Build 19041+. Run `dotnet restore` first. |
| **PostgreSQL connection refused** | Check PostgreSQL is running on port 5432. Verify connection string in user secrets. |
| **Redis connection failed** | Redis is optional. The API works without it but real-time events may not broadcast across instances. |
| **Docker build fails** | Ensure Docker Desktop is running. Try `docker-compose down -v` then `docker-compose up -d`. |

---

## License

This project is proprietary. All rights reserved.
