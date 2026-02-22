using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgNet.Infrastructure.Auth;
using OrgNet.Infrastructure.Services;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AuditService _auditService;
    private readonly ITenantContext _tenantContext;

    public AuthController(AuthService authService, AuditService auditService, ITenantContext tenantContext)
    {
        _authService = authService;
        _auditService = auditService;
        _tenantContext = tenantContext;
    }

    /// <summary>Register a new organisation. First user becomes Owner.</summary>
    [HttpPost("register-org")]
    public async Task<ActionResult<AuthResponse>> RegisterOrganisation([FromBody] RegisterOrganisationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrganisationName) || request.OrganisationName.Length < 2 || request.OrganisationName.Length > 200)
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Organisation name must be 2-200 characters"));

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Valid email required"));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 || request.Password.Length > 128)
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Password must be 8-128 characters"));

        if (string.IsNullOrWhiteSpace(request.AdminDisplayName) || request.AdminDisplayName.Length > 150)
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Display name required (max 150 chars)"));

        var result = await _authService.RegisterOrganisationAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Login to an existing organisation</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Email and password required"));

        if (!_tenantContext.IsResolved)
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Tenant context required — include X-Tenant-Id header"));

        var result = await _authService.LoginAsync(request, _tenantContext.TenantId);

        if (result.Success)
        {
            await _auditService.LogAsync(null, request.Email, AuditAction.Login, "User",
                details: "Login successful", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }

    /// <summary>Login by email — tenant is resolved automatically from the user's account</summary>
    [HttpPost("login-by-email")]
    public async Task<ActionResult<AuthResponse>> LoginByEmail([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Email and password required"));

        var result = await _authService.LoginByEmailAsync(request);

        if (result.Success)
        {
            await _auditService.LogAsync(null, request.Email, AuditAction.Login, "User",
                details: "Login successful (by email)", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        if (!result.Success) return Unauthorized(result);
        return Ok(result);
    }

    /// <summary>Refresh an access token using a valid refresh token</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenPair>> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token required" });

        var result = await _authService.RefreshAsync(request.RefreshToken);
        if (result == null) return Unauthorized(new { error = "Invalid or expired refresh token" });
        return Ok(result);
    }

    /// <summary>Revoke a refresh token (logout)</summary>
    [HttpPost("revoke")]
    [Authorize]
    public async Task<ActionResult> Revoke([FromBody] RefreshTokenRequest request)
    {
        await _authService.RevokeAsync(request.RefreshToken);

        var userId = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        var name = User.Identity?.Name ?? "";
        await _auditService.LogAsync(
            Guid.TryParse(userId, out var uid) ? uid : null,
            name, AuditAction.Logout, "User",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { message = "Token revoked" });
    }

    /// <summary>Get current user profile</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            UserId = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId),
            TenantId = User.FindFirstValue(OrgNetConstants.ClaimTypes.TenantId),
            DisplayName = User.Identity?.Name,
            Role = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role)
        });
    }
}
