using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using OrgNet.Infrastructure.Auth;
using OrgNet.Shared.Constants;

namespace OrgNet.Tests;

/// <summary>
/// Regression tests for JWT token generation and validation.
/// Ensures tokens contain the correct claims, are valid, and expire correctly.
/// </summary>
public class TokenServiceTests
{
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "OrgNet-Test-Secret-Key-Must-Be-At-Least-32-Characters!!",
                ["Jwt:Issuer"] = "OrgNet.Tests",
                ["Jwt:Audience"] = "OrgNet.Tests.Clients",
                ["Jwt:AccessTokenExpiryMinutes"] = "15"
            })
            .Build();

        _tokenService = new TokenService(config);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyString()
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "Test User", "Owner");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateAccessToken_ContainsCorrectClaims()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var displayName = "Alice Smith";
        var role = "Admin";

        var token = _tokenService.GenerateAccessToken(userId, tenantId, displayName, role);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(userId.ToString(), jwt.Claims.First(c => c.Type == OrgNetConstants.ClaimTypes.UserId).Value);
        Assert.Equal(tenantId.ToString(), jwt.Claims.First(c => c.Type == OrgNetConstants.ClaimTypes.TenantId).Value);
        Assert.Equal(displayName, jwt.Claims.First(c => c.Type == "name").Value);
        Assert.Equal(role, jwt.Claims.First(c => c.Type == OrgNetConstants.ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateAccessToken_HasJtiClaim()
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "Test", "Member");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jti);
        Assert.True(Guid.TryParse(jti.Value, out _));
    }

    [Fact]
    public void GenerateAccessToken_ExpiresIn15Minutes()
    {
        var before = DateTime.UtcNow;
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "Test", "Member");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.True(jwt.ValidTo > before.AddMinutes(14));
        Assert.True(jwt.ValidTo < before.AddMinutes(16));
    }

    [Fact]
    public void ValidateAccessToken_ValidToken_ReturnsPrincipal()
    {
        var userId = Guid.NewGuid();
        var token = _tokenService.GenerateAccessToken(userId, Guid.NewGuid(), "Test", "Owner");
        var principal = _tokenService.ValidateAccessToken(token);

        Assert.NotNull(principal);
        // The validator may remap "sub" to the long NameIdentifier URI — check both
        var sub = principal!.FindFirst(OrgNetConstants.ClaimTypes.UserId)?.Value
               ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(userId.ToString(), sub);
    }

    [Fact]
    public void ValidateAccessToken_TamperedToken_ReturnsNull()
    {
        var token = _tokenService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "Test", "Owner");
        var tampered = token + "TAMPERED";
        var result = _tokenService.ValidateAccessToken(tampered);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateAccessToken_GarbageString_ReturnsNull()
    {
        var result = _tokenService.ValidateAccessToken("not-a-real-token");
        Assert.Null(result);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        var token = _tokenService.GenerateRefreshToken();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokens()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _tokenService.GenerateRefreshToken()).ToList();
        Assert.Equal(100, tokens.Distinct().Count());
    }

    [Fact]
    public void GenerateAccessToken_DifferentUsersProduceDifferentTokens()
    {
        var token1 = _tokenService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "User1", "Member");
        var token2 = _tokenService.GenerateAccessToken(Guid.NewGuid(), Guid.NewGuid(), "User2", "Admin");
        Assert.NotEqual(token1, token2);
    }
}
