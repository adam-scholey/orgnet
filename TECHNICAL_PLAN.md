# OrgNet Digital Workspace — Technical Improvement Plan

## Executive Summary

After full codebase analysis, here are the 7 reported problems, their root causes, and fix strategies. Each fix is implemented in the corresponding commit.

---

## 1. FILE UPLOADS — Fail/Hang

**Root Cause**: `FilesController.Upload` accepts `[FromBody] UploadFileRequest` with `Base64Content`. The entire file is loaded into memory as a base64 string (~33% larger than raw), encrypted in-memory, then stored as `byte[]` in PostgreSQL. For files >10MB this causes:
- HTTP request timeout (default 30s in `OrgNetApiClient`)
- Memory pressure / OOM on server
- No progress feedback to desktop client

**Fix Strategy**:
- Add `IFormFile` multipart upload endpoint alongside base64 (keeps backward compat)
- Add server-side file size limit (50MB default, configurable per tenant plan)
- Add file type validation (allowlist by extension + magic bytes)
- Stream encryption instead of buffering entire file
- Desktop client: add upload progress via `HttpClient` progress handler
- Add retry with exponential backoff in `OrgNetApiClient`

---

## 2. INVITE LINK — Reliability/Validation Issues

**Root Cause**: Race condition in `TenantController.InviteUser` — the check for existing pending invites and the insert are not atomic. Also:
- Expired invites are never cleaned up (query filter check happens at read time only)
- `accept-invite` endpoint is tenant-free but `Invitation` has a tenant query filter, so lookups by token may fail
- No rate limiting on invite creation

**Fix Strategy**:
- Use `IgnoreQueryFilters()` when looking up invitations by token
- Add unique constraint on (TenantId, Email) for non-accepted invitations
- Add expired invite cleanup in a background service
- Add revocation endpoint
- Add rate limiting (max 50 invites/hour per tenant)

---

## 3. MESSAGING — Delivery/Sync Problems

**Root Cause**: `ChatController.Send` saves to DB then broadcasts via SignalR, but:
- No message ordering guarantee (concurrent sends can arrive out of order)
- No offline message queue — if client disconnects during send, message is lost from their view
- No read receipts or delivery confirmation
- SignalR reconnection doesn't re-fetch missed messages
- No pagination cursor — `Take(50)` with `OrderByDescending` returns newest 50 but desktop client has no way to load older messages

**Fix Strategy**:
- Add sequence number per channel for ordering
- Add `LastReadAt` per user per channel for unread counts
- Add SignalR reconnection handler on desktop that fetches messages since last known timestamp
- Add cursor-based pagination
- Add typing indicators via SignalR

---

## 4. APPOINTMENT SCHEDULING — State Consistency

**Root Cause**: No `Appointment` entity exists yet. Need to build from scratch with:
- Optimistic concurrency (row version) to prevent double-booking
- Time slot validation with overlap detection
- Timezone-aware storage (UTC + user timezone)

**Fix Strategy**:
- New `Appointment` entity with `RowVersion` for concurrency
- Overlap detection query before insert
- Transaction isolation level `Serializable` for booking operations
- Calendar sync considerations (iCal export)

---

## 5. APP LAUNCHER — Opens Web Instead of Desktop Apps

**Root Cause**: `AppLauncherPage.xaml.cs` uses `EmbeddedWebView.Source = new Uri(service.Endpoint)` — every app opens in a WebView2. For installed desktop apps, this is wrong.

**Fix Strategy**:
- Detect if service is a desktop app (new `LaunchMode` enum: Web, Desktop, Protocol)
- Desktop mode: use `Process.Start` with `ShellExecute` to launch installed apps
- Protocol mode: use `Windows.System.Launcher.LaunchUriAsync` for protocol activation (e.g., `ms-teams:`, `slack:`)
- Fallback: if app not found, show "Not installed" dialog with download link
- Keep WebView2 for web-only services

---

## 6. ADMIN MONITORING + MODULE PRIVILEGES

**Root Cause**: `ModulesController` only manages tenant-level module on/off. No per-user feature access. `AuditController` returns logs but no real-time dashboard.

**Fix Strategy**:
- Add `UserModuleAccess` entity for per-user module permissions
- Add claims-based authorization middleware checking user + tenant module access
- Add real-time admin dashboard events via SignalR (user login, file upload, etc.)
- Add activity summary endpoint (active users, top modules, recent actions)

---

## 7. DEVICE TRUST — Needs Validation & Testing Strategy

**Root Cause**: `DeviceService` stores fingerprint as opaque string. No validation of fingerprint format, no risk scoring, no hardware attestation.

**Fix Strategy**:
- Validate fingerprint format (SHA-256 hex, 64 chars)
- Add `RiskScore` field based on: new device, unusual IP, multiple devices per user
- Add `LastIpAddress` tracking
- Desktop fingerprint: combine MachineGuid + BIOS serial + OS install date (avoid MAC address — too volatile)
- Local testing: use multiple Windows user profiles or VM snapshots with different MachineGuids

---

## Security Hardening

- JWT: ClockSkew already 30s (good), add token blacklist on revoke
- CSRF: Not applicable (API-only, no cookies)
- Rate limiting: Add `AspNetCoreRateLimit` middleware
- Secrets: `appsettings.json` has placeholder passwords (good), use User Secrets or env vars
- Encryption at rest: Files already AES-256 encrypted (good)
- Add `SecurityHeadersMiddleware` already exists (good)

---

## Implementation Order

1. File upload fix (highest user impact)
2. Messaging reliability
3. Invite link fix
4. App launcher desktop mode
5. Appointment scheduling (new feature)
6. Admin monitoring + module privileges
7. Device trust improvements
