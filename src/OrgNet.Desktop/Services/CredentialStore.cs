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

    public string? GetUserRole() => GetClaimFromToken("role");
    public string? GetDisplayName() => GetClaimFromToken("name");

    private string? GetClaimFromToken(string claimType)
    {
        var token = GetAccessToken();
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1];
            // Pad base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4) { case 2: payload += "=="; break; case 3: payload += "="; break; }
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(claimType, out var val) ? val.GetString() : null;
        }
        catch { return null; }
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
