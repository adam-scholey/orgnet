namespace OrgNet.Desktop.Services;

/// <summary>
/// Abstraction for secure token storage.
/// Production implementation uses Windows Credential Locker (DPAPI-backed).
/// </summary>
public interface ICredentialStore
{
    void StoreTokens(string accessToken, string refreshToken, Guid tenantId);
    string? GetAccessToken();
    string? GetRefreshToken();
    Guid? GetTenantId();
    string? GetUserRole();
    string? GetDisplayName();
    void Clear();
}
