namespace OrgNet.Shared.Enums;

public enum UserRole
{
    Member = 0,
    Moderator = 1,
    Admin = 2,
    Owner = 3
}

public enum TenantPlan
{
    Free = 0,
    Starter = 1,
    Business = 2,
    Enterprise = 3
}

public enum DeviceTrustLevel
{
    Untrusted = 0,
    Pending = 1,
    Trusted = 2,
    Revoked = 3
}

public enum ModuleStatus
{
    Disabled = 0,
    Enabled = 1,
    Licensed = 2
}

public enum AuditAction
{
    Login,
    Logout,
    Create,
    Update,
    Delete,
    RoleChange,
    ModuleToggle,
    DeviceRegistered,
    DeviceRevoked,
    TokenRefreshed
}
