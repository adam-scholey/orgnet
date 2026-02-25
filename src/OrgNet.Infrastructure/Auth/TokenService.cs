using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrgNet.Shared.Constants;

namespace OrgNet.Infrastructure.Auth;

/// <summary>
/// Generates and validates JWT access tokens and opaque refresh tokens.
/// Access tokens are short-lived (15 min); refresh tokens are long-lived (7 days)
/// and support secure rotation (single-use with revocation chain).
/// </summary>
public class TokenService
{
    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _signingKey;

    public TokenService(IConfiguration config)
    {
        _config = config;
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string GenerateAccessToken(Guid userId, Guid tenantId, string displayName, string role)
    {
        var claims = new[]
        {
            new Claim(OrgNetConstants.ClaimTypes.UserId, userId.ToString()),
            new Claim(OrgNetConstants.ClaimTypes.TenantId, tenantId.ToString()),
            new Claim("name", displayName),
            new Claim(OrgNetConstants.ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiryMinutes = int.TryParse(_config["Jwt:AccessTokenExpiryMinutes"], out var m) ? m : 15;

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "OrgNet",
            audience: _config["Jwt:Audience"] ?? "OrgNet.Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"] ?? "OrgNet",
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"] ?? "OrgNet.Clients",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}
