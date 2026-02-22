namespace OrgNet.Shared.Constants;

public static class OrgNetConstants
{
    public const string TenantHeaderName = "X-Tenant-Id";
    public const string DeviceHeaderName = "X-Device-Id";

    public static class Roles
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string Moderator = "Moderator";
        public const string Member = "Member";
    }

    public static class Policies
    {
        public const string RequireOwner = "RequireOwner";
        public const string RequireAdmin = "RequireAdmin";
        public const string RequireModerator = "RequireModerator";
    }

    public static class ClaimTypes
    {
        public const string TenantId = "tenant_id";
        public const string DeviceId = "device_id";
        public const string UserId = "sub";
        public const string Role = "role";
    }

    public static class SignalRGroups
    {
        public static string TenantGroup(Guid tenantId) => $"tenant-{tenantId}";
        public static string UserGroup(Guid userId) => $"user-{userId}";
    }

    public static class Modules
    {
        public const string Chat = "OrgNet.Chat";
        public const string FileStorage = "OrgNet.FileStorage";
        public const string TaskBoard = "OrgNet.TaskBoard";
        public const string Analytics = "OrgNet.Analytics";
        public const string Wiki = "OrgNet.Wiki";
    }
}
