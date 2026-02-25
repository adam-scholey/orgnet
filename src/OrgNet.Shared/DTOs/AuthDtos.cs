namespace OrgNet.Shared.DTOs;

// Auth
public record LoginRequest(string Email, string Password, string? DeviceFingerprint = null);
public record RegisterOrganisationRequest(string OrganisationName, string AdminDisplayName, string Email, string Password);
public record AuthResponse(bool Success, string AccessToken, string RefreshToken, string DisplayName, string Role, Guid TenantId, string? Error = null);
public record RefreshTokenRequest(string RefreshToken);
public record TokenPair(string AccessToken, string RefreshToken);

// Users
public record UserProfileDto(Guid Id, string DisplayName, string Email, string Role, string? AvatarUrl, DateTime CreatedAt);
public record UpdateProfileRequest(string? DisplayName, string? AvatarUrl);
public record InviteUserRequest(string Email, string Role);
public record InviteResponse(bool Success, string InviteLink, string Token, DateTime ExpiresAt, string? Error = null);
public record AcceptInviteRequest(string Token, string DisplayName, string Password);
public record InvitationInfoDto(Guid Id, string Email, string Role, string OrganisationName, string InvitedBy, DateTime ExpiresAt, bool IsAccepted, bool IsExpired);

// Tenant
public record TenantInfoDto(Guid Id, string Name, string Slug, string Plan, int MemberCount, DateTime CreatedAt);
public record UpdateTenantRequest(string? Name, string? Plan);

// Devices
public record RegisterDeviceRequest(string DeviceName, string Fingerprint, string Platform);
public record DeviceDto(Guid Id, string DeviceName, string Platform, string TrustLevel, DateTime RegisteredAt, DateTime? LastSeenAt, string? LastIpAddress = null, int RiskScore = 0);

// Modules
public record ModuleInfoDto(string ModuleId, string Name, string Description, string Version, string Status, string? IconUrl);
public record ToggleModuleRequest(string ModuleId, bool Enabled);

// Service Registry
public record ServiceEntryDto(string ServiceId, string Name, string Endpoint, string Status);

// Audit
public record AuditLogDto(Guid Id, string Username, string Action, string EntityType, string? EntityId, string? Details, string? IpAddress, DateTime Timestamp);
public record AuditPageResult(List<AuditLogDto> Items, int TotalCount, int Page, int PageSize);

// Admin Dashboard
public record AdminDashboardSummary(
    int TotalUsers, int RecentLogins, int RecentActions, int TotalFiles,
    int TotalMessages24h, int TotalTasks, int ActiveTasks, int UpcomingAppointments,
    int TrustedDevices, int PendingDevices, List<ActivityUserSummary> TopUsers);
public record ActivityUserSummary(string Username, int ActionCount);

// Per-User Module Access
public record UserModuleAccessDto(Guid Id, Guid UserId, string UserDisplayName, string ModuleId, bool IsGranted, string GrantedBy, DateTime GrantedAt);
public record SetUserModuleAccessRequest(Guid UserId, string ModuleId, bool IsGranted);

// SignalR Events
public record TenantEventDto(string EventType, string Source, object? Payload, DateTime Timestamp);
public record NotificationDto(string Title, string Message, string Severity, DateTime Timestamp);
