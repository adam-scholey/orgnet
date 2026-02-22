using Windows.Security.Credentials;

namespace OrgNet.Desktop.Services;

/// <summary>
/// Secure JWT storage using Windows Credential Locker (PasswordVault).
/// 
/// Architectural decision: Windows Credential Locker over plaintext files.
/// - Backed by DPAPI — encrypted at rest, tied to the Windows user profile.
/// - Survives app restarts without re-authentication.
/// - Cannot be read by other applications or users on the same machine.
/// - Automatically cleared when the Windows user profile is deleted.
/// </summary>
public class CredentialStore : ICredentialStore
{
    private const string ResourceName = "OrgNet";
    private const string AccessTokenKey = "AccessToken";
    private const string RefreshTokenKey = "RefreshToken";
    private const string TenantIdKey = "TenantId";

    private readonly PasswordVault _vault = new();

    public void StoreTokens(string accessToken, string refreshToken, Guid tenantId)
    {
        Clear();
        _vault.Add(new PasswordCredential(ResourceName, AccessTokenKey, accessToken));
        _vault.Add(new PasswordCredential(ResourceName, RefreshTokenKey, refreshToken));
        _vault.Add(new PasswordCredential(ResourceName, TenantIdKey, tenantId.ToString()));
    }

    public string? GetAccessToken() => RetrievePassword(AccessTokenKey);
    public string? GetRefreshToken() => RetrievePassword(RefreshTokenKey);

    public Guid? GetTenantId()
    {
        var value = RetrievePassword(TenantIdKey);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public void Clear()
    {
        try
        {
            var credentials = _vault.FindAllByResource(ResourceName);
            foreach (var cred in credentials)
                _vault.Remove(cred);
        }
        catch (Exception)
        {
            // No credentials stored yet — safe to ignore
        }
    }

    private string? RetrievePassword(string userName)
    {
        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
