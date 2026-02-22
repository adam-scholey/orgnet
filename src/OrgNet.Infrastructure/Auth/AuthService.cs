using Microsoft.EntityFrameworkCore;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;

namespace OrgNet.Infrastructure.Auth;

/// <summary>
/// Handles organisation registration, user login, and secure refresh token rotation.
/// 
/// Architectural decisions:
/// - BCrypt with work factor 12 for password hashing (resistant to GPU brute-force).
/// - Refresh tokens are opaque random strings stored in DB, single-use with rotation.
/// - When a token is refreshed, the old one is revoked and a replacement is issued.
/// - If a revoked token is reused, the entire token family is revoked (replay detection).
/// - First user to register an organisation automatically becomes Owner.
/// </summary>
public class AuthService
{
    private readonly OrgNetDbContext _db;
    private readonly TokenService _tokenService;

    public AuthService(OrgNetDbContext db, TokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterOrganisationAsync(RegisterOrganisationRequest request)
    {
        if (await _db.Tenants.AnyAsync(t => t.Slug == Slugify(request.OrganisationName)))
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Organisation name already taken");

        // Check email globally (ignore tenant filter for registration)
        if (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email))
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Email already registered");

        var tenant = new Tenant
        {
            Name = request.OrganisationName,
            Slug = Slugify(request.OrganisationName),
            Plan = TenantPlan.Free
        };

        var user = new User
        {
            TenantId = tenant.Id,
            DisplayName = request.AdminDisplayName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = UserRole.Owner
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user.Id, tenant.Id, user.DisplayName, user.Role.ToString());
        var refreshToken = await CreateRefreshTokenAsync(user.Id, tenant.Id);

        return new AuthResponse(true, accessToken, refreshToken, user.DisplayName, user.Role.ToString(), tenant.Id);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, Guid tenantId)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.TenantId == tenantId);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Invalid credentials");

        if (!user.IsActive)
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Account is deactivated");

        user.LastLoginAt = DateTime.UtcNow;

        var accessToken = _tokenService.GenerateAccessToken(user.Id, tenantId, user.DisplayName, user.Role.ToString());
        var refreshToken = await CreateRefreshTokenAsync(user.Id, tenantId, request.DeviceFingerprint);

        await _db.SaveChangesAsync();

        return new AuthResponse(true, accessToken, refreshToken, user.DisplayName, user.Role.ToString(), tenantId);
    }

    /// <summary>
    /// Login by email only — looks up the user's tenant automatically.
    /// If the email exists in multiple tenants, returns the first match.
    /// </summary>
    public async Task<AuthResponse> LoginByEmailAsync(LoginRequest request)
    {
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Invalid credentials");

        if (!user.IsActive)
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Account is deactivated");

        user.LastLoginAt = DateTime.UtcNow;

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.TenantId, user.DisplayName, user.Role.ToString());
        var refreshToken = await CreateRefreshTokenAsync(user.Id, user.TenantId, request.DeviceFingerprint);

        await _db.SaveChangesAsync();

        return new AuthResponse(true, accessToken, refreshToken, user.DisplayName, user.Role.ToString(), user.TenantId);
    }

    /// <summary>
    /// Secure refresh token rotation. The old token is revoked and a new pair is issued.
    /// If a revoked token is presented (replay attack), revoke the entire family.
    /// </summary>
    public async Task<TokenPair?> RefreshAsync(string refreshTokenValue)
    {
        var token = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshTokenValue);

        if (token == null)
            return null;

        // Replay detection: if token was already revoked, revoke entire chain
        if (token.IsRevoked)
        {
            await RevokeTokenFamilyAsync(token.UserId, "Replay attack detected");
            return null;
        }

        if (token.IsExpired)
            return null;

        // Rotate: revoke old, issue new
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        token.RevokedAt = DateTime.UtcNow;
        token.RevokedReason = "Rotated";
        token.ReplacedByToken = newRefreshToken;

        var newToken = new RefreshToken
        {
            UserId = token.UserId,
            TenantId = token.TenantId,
            Token = newRefreshToken,
            DeviceFingerprint = token.DeviceFingerprint,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        _db.RefreshTokens.Add(newToken);

        var user = token.User;
        var accessToken = _tokenService.GenerateAccessToken(user.Id, token.TenantId, user.DisplayName, user.Role.ToString());

        await _db.SaveChangesAsync();

        return new TokenPair(accessToken, newRefreshToken);
    }

    public async Task RevokeAsync(string refreshTokenValue, string reason = "User logout")
    {
        var token = await _db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Token == refreshTokenValue);
        if (token != null && token.IsActive)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = reason;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, Guid tenantId, string? deviceFingerprint = null)
    {
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TenantId = tenantId,
            Token = refreshTokenValue,
            DeviceFingerprint = deviceFingerprint,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        _db.RefreshTokens.Add(refreshToken);
        return refreshTokenValue;
    }

    private async Task RevokeTokenFamilyAsync(Guid userId, string reason)
    {
        var activeTokens = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();

        foreach (var t in activeTokens)
        {
            t.RevokedAt = DateTime.UtcNow;
            t.RevokedReason = reason;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Look up an invitation by its secure token. Returns info for the invited user
    /// to see which org they're joining. Does not require authentication.
    /// </summary>
    public async Task<InvitationInfoDto?> GetInvitationByTokenAsync(string token)
    {
        var invite = await _db.Invitations
            .IgnoreQueryFilters()
            .Include(i => i.Tenant)
            .Include(i => i.InvitedBy)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invite == null) return null;

        return new InvitationInfoDto(
            invite.Id, invite.Email, invite.Role.ToString(),
            invite.Tenant.Name, invite.InvitedBy.DisplayName,
            invite.ExpiresAt, invite.IsAccepted, invite.IsExpired);
    }

    /// <summary>
    /// Accept an invitation: validates the token, creates the user in the tenant,
    /// marks the invitation as accepted, and returns auth tokens so the user is
    /// immediately logged in.
    /// </summary>
    public async Task<AuthResponse> AcceptInvitationAsync(AcceptInviteRequest request)
    {
        var invite = await _db.Invitations
            .IgnoreQueryFilters()
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.Token == request.Token);

        if (invite == null)
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "Invalid invitation token");

        if (invite.IsAccepted)
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "This invitation has already been accepted");

        if (invite.IsExpired)
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "This invitation has expired. Ask your admin to send a new one.");

        // Check email not already registered in this tenant
        var existingUser = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == invite.Email && u.TenantId == invite.TenantId);

        if (existingUser != null)
            return new AuthResponse(false, "", "", "", "", Guid.Empty, "An account with this email already exists in this organisation");

        // Create the user
        var user = new User
        {
            TenantId = invite.TenantId,
            DisplayName = request.DisplayName,
            Email = invite.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            Role = invite.Role,
        };

        _db.Users.Add(user);

        // Mark invitation as accepted
        invite.AcceptedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Generate tokens so the user is immediately logged in
        var accessToken = _tokenService.GenerateAccessToken(user.Id, invite.TenantId, user.DisplayName, user.Role.ToString());
        var refreshToken = await CreateRefreshTokenAsync(user.Id, invite.TenantId);
        await _db.SaveChangesAsync();

        return new AuthResponse(true, accessToken, refreshToken, user.DisplayName, user.Role.ToString(), invite.TenantId);
    }

    private static string Slugify(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate("", (current, c) => current + c)
            .Trim('-');
    }
}
