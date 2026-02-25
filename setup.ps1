# ============================================================================
# OrgNet — Automated Setup Script for Windows (PowerShell)
# ============================================================================
# Run this script inside your VM (or host machine) to install all prerequisites
# and configure the project ready to run.
#
# Usage (run as Administrator in PowerShell):
#   Set-ExecutionPolicy Bypass -Scope Process -Force
#   .\setup.ps1
# ============================================================================

param(
    [string]$DbPassword = "postgres",
    [string]$JwtSecret  = "OrgNet-SuperSecret-Key-Change-In-Production-Min-32-Chars!!"
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}
function Write-OK($msg) {
    Write-Host "    [OK] $msg" -ForegroundColor Green
}
function Write-Skip($msg) {
    Write-Host "    [SKIP] $msg already installed" -ForegroundColor Yellow
}

Write-Host @"

  ____  ____   ____  _   _      _   
 / __ \|  _ \ / __ \| \ | |  __| |_ 
| |  | | |_) | |  | |  \| | / _  __|
| |  | |  _ <| |  | | . ` | \  | |_ 
| |__| | |_) | |__| | |\  |  \_  _|
 \____/|____/ \____/|_| \_|    |_|  

  OrgNet Setup Script
  -------------------

"@ -ForegroundColor Blue

# ── 1. Check Windows Version ─────────────────────────────────────────────────
Write-Step "Checking Windows version..."
$build = [System.Environment]::OSVersion.Version.Build
if ($build -lt 19041) {
    Write-Host "    [ERROR] Windows Build $build detected. WinUI 3 requires Build 19041 (Windows 10 20H1) or later." -ForegroundColor Red
    exit 1
}
Write-OK "Windows Build $build — compatible"

# ── 2. Check / Install winget ────────────────────────────────────────────────
Write-Step "Checking winget availability..."
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Write-Host "    winget not found. Please install 'App Installer' from the Microsoft Store." -ForegroundColor Red
    Start-Process "ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1"
    Read-Host "    Press Enter after installing App Installer to continue"
}
Write-OK "winget available"

# ── 3. Install .NET 10 SDK ───────────────────────────────────────────────────
Write-Step "Checking .NET 10 SDK..."
$dotnet = dotnet --list-sdks 2>$null | Where-Object { $_ -match "^10\." }
if ($dotnet) {
    Write-Skip ".NET 10 SDK ($($dotnet[0].Split()[0]))"
} else {
    Write-Host "    Installing .NET 10 SDK..." -ForegroundColor White
    winget install --id Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements --silent
    Write-OK ".NET 10 SDK installed"
}

# ── 4. Install Git ───────────────────────────────────────────────────────────
Write-Step "Checking Git..."
if (Get-Command git -ErrorAction SilentlyContinue) {
    Write-Skip "Git ($(git --version))"
} else {
    Write-Host "    Installing Git..." -ForegroundColor White
    winget install --id Git.Git --accept-source-agreements --accept-package-agreements --silent
    Write-OK "Git installed"
}

# ── 5. Install Docker Desktop ────────────────────────────────────────────────
Write-Step "Checking Docker Desktop..."
if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Skip "Docker ($(docker --version))"
} else {
    Write-Host "    Installing Docker Desktop..." -ForegroundColor White
    Write-Host "    NOTE: Docker Desktop requires a restart after installation." -ForegroundColor Yellow
    winget install --id Docker.DockerDesktop --accept-source-agreements --accept-package-agreements --silent
    Write-OK "Docker Desktop installed — you may need to restart before continuing"
}

# ── 6. Restore NuGet Packages ────────────────────────────────────────────────
Write-Step "Restoring NuGet packages..."
$sln = Join-Path $PSScriptRoot "OrgNet.slnx"
if (Test-Path $sln) {
    dotnet restore $sln
    Write-OK "NuGet packages restored"
} else {
    Write-Host "    [WARN] Solution file not found at $sln — skipping restore" -ForegroundColor Yellow
    Write-Host "    Run 'dotnet restore' manually from the project root." -ForegroundColor Yellow
}

# ── 7. Start Database via Docker Compose ─────────────────────────────────────
Write-Step "Starting PostgreSQL and Redis via Docker Compose..."
$compose = Join-Path $PSScriptRoot "docker-compose.yml"
if (Test-Path $compose) {
    try {
        docker compose -f $compose up -d postgres redis
        Write-OK "PostgreSQL and Redis containers started"
    } catch {
        Write-Host "    [WARN] Docker Compose failed: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "    Ensure Docker Desktop is running and try: docker compose up -d postgres redis" -ForegroundColor Yellow
    }
} else {
    Write-Host "    [WARN] docker-compose.yml not found — skipping container startup" -ForegroundColor Yellow
}

# ── 8. Configure User Secrets ────────────────────────────────────────────────
Write-Step "Configuring OrgNet API user secrets..."
$apiProject = Join-Path $PSScriptRoot "src\OrgNet.Api\OrgNet.Api.csproj"
if (Test-Path $apiProject) {
    $connStr = "Host=localhost;Port=5432;Database=orgnet;Username=postgres;Password=$DbPassword"
    dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connStr --project $apiProject
    dotnet user-secrets set "Jwt:Secret" $JwtSecret --project $apiProject
    dotnet user-secrets set "Jwt:Issuer" "OrgNet" --project $apiProject
    dotnet user-secrets set "Jwt:Audience" "OrgNet.Clients" --project $apiProject
    Write-OK "User secrets configured"
} else {
    Write-Host "    [WARN] API project not found at $apiProject — skipping secrets" -ForegroundColor Yellow
}

# ── 9. Build Projects ────────────────────────────────────────────────────────
Write-Step "Building OrgNet.Api..."
dotnet build (Join-Path $PSScriptRoot "src\OrgNet.Api\OrgNet.Api.csproj") --no-restore
Write-OK "API build successful"

Write-Step "Building OrgNet.Desktop..."
dotnet build (Join-Path $PSScriptRoot "src\OrgNet.Desktop\OrgNet.Desktop.csproj") --no-restore
Write-OK "Desktop build successful"

# ── Done ─────────────────────────────────────────────────────────────────────
Write-Host @"

============================================================
  Setup Complete!
============================================================

  To run OrgNet:

  1. Start the API (Terminal 1):
     dotnet run --project src\OrgNet.Api\OrgNet.Api.csproj

  2. Start the Desktop Client (Terminal 2):
     dotnet run --project src\OrgNet.Desktop\OrgNet.Desktop.csproj ``
       -f net10.0-windows10.0.19041.0

  API will be available at: http://localhost:5100
  Health check:             http://localhost:5100/health

  First use: Register a new organisation from the login screen.
============================================================

"@ -ForegroundColor Green
