using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace OrgNet.Pwa.Services;

public class TokenAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _storage;

    public TokenAuthStateProvider(ILocalStorageService storage) => _storage = storage;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _storage.GetItemAsStringAsync("access_token");
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            if (jwt.ValidTo < DateTime.UtcNow)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyAuthChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
