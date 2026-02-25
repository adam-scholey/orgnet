using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrgNet.Infrastructure.Auth;
using OrgNet.Infrastructure.Services;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AuthEndpoints")]
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

    /// <summary>
    /// Get invitation details by token. Public endpoint — the invited user
    /// uses this to see which org they're joining before accepting.
    /// </summary>
    [HttpGet("invite-info")]
    public async Task<ActionResult<InvitationInfoDto>> GetInviteInfo([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Token required" });

        var invite = await _authService.GetInvitationByTokenAsync(token);
        if (invite == null)
            return NotFound(new { error = "Invitation not found or expired" });

        return Ok(invite);
    }

    /// <summary>
    /// Browser-friendly invite acceptance page.
    /// When a user clicks/pastes the invite link, this serves an HTML form.
    /// </summary>
    [HttpGet("accept-invite")]
    public async Task<ContentResult> AcceptInvitePage([FromQuery] string token)
    {
        var invite = string.IsNullOrWhiteSpace(token) ? null : await _authService.GetInvitationByTokenAsync(token);

        string html;
        if (invite == null || invite.IsExpired || invite.IsAccepted)
        {
            var reason = invite == null ? "Invitation not found." : invite.IsAccepted ? "This invitation has already been accepted." : "This invitation has expired.";
            html = $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>OrgNet — Invitation</title>
            <style>*{margin:0;padding:0;box-sizing:border-box}body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;display:flex;align-items:center;justify-content:center;min-height:100vh}.card{background:#1e293b;border-radius:12px;padding:2.5rem;max-width:440px;width:90%;text-align:center;box-shadow:0 8px 32px rgba(0,0,0,.3)}h1{font-size:1.5rem;margin-bottom:1rem;color:#f8fafc}.error{color:#f87171;font-size:1.1rem;margin-bottom:1rem}</style>
            </head><body><div class="card"><h1>OrgNet</h1><p class="error">{{reason}}</p><p>Please contact your organisation administrator for a new invite.</p></div></body></html>
            """;
        }
        else
        {
            var safeToken = System.Net.WebUtility.HtmlEncode(token);
            html = $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>OrgNet — Join {{invite.OrganisationName}}</title>
            <style>
            *{margin:0;padding:0;box-sizing:border-box}
            body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#0f172a;color:#e2e8f0;display:flex;align-items:center;justify-content:center;min-height:100vh}
            .card{background:#1e293b;border-radius:12px;padding:2.5rem;max-width:440px;width:90%;box-shadow:0 8px 32px rgba(0,0,0,.3)}
            h1{font-size:1.5rem;margin-bottom:.5rem;color:#f8fafc;text-align:center}
            .sub{text-align:center;color:#94a3b8;margin-bottom:1.5rem;font-size:.95rem}
            .info{background:#334155;border-radius:8px;padding:1rem;margin-bottom:1.5rem;font-size:.9rem;line-height:1.6}
            .info strong{color:#60a5fa}
            label{display:block;font-size:.85rem;color:#94a3b8;margin-bottom:.3rem;margin-top:1rem}
            input{width:100%;padding:.7rem .9rem;border:1px solid #475569;border-radius:8px;background:#0f172a;color:#f8fafc;font-size:1rem;outline:none;transition:border .2s}
            input:focus{border-color:#3b82f6}
            button{width:100%;padding:.75rem;border:none;border-radius:8px;background:#3b82f6;color:#fff;font-size:1rem;font-weight:600;cursor:pointer;margin-top:1.5rem;transition:background .2s}
            button:hover{background:#2563eb}
            button:disabled{background:#475569;cursor:not-allowed}
            .msg{text-align:center;margin-top:1rem;font-size:.9rem;min-height:1.3rem}
            .msg.ok{color:#34d399}.msg.err{color:#f87171}
            </style>
            </head><body><div class="card">
            <h1>OrgNet</h1>
            <p class="sub">You've been invited to join an organisation</p>
            <div class="info">
                <strong>Organisation:</strong> {{invite.OrganisationName}}<br>
                <strong>Invited by:</strong> {{invite.InvitedBy}}<br>
                <strong>Role:</strong> {{invite.Role}}<br>
                <strong>Email:</strong> {{invite.Email}}
            </div>
            <form id="f" onsubmit="return doAccept(event)">
                <input id="tk" type="hidden" value="{{safeToken}}">
                <label for="name">Display Name</label>
                <input id="name" type="text" required minlength="1" maxlength="150" placeholder="Your name">
                <label for="pw">Password</label>
                <input id="pw" type="password" required minlength="8" maxlength="128" placeholder="Choose a password (min 8 chars)">
                <button id="btn" type="submit">Join Organisation</button>
            </form>
            <p id="msg" class="msg"></p>
            </div>
            <script>
            async function doAccept(e){
                e.preventDefault();
                const btn=document.getElementById('btn'),msg=document.getElementById('msg');
                const tk=document.getElementById('tk').value;
                btn.disabled=true; msg.className='msg'; msg.textContent='Joining...';
                try{
                    const res=await fetch('/api/auth/accept-invite',{
                        method:'POST',headers:{'Content-Type':'application/json'},
                        body:JSON.stringify({token:tk,displayName:document.getElementById('name').value,password:document.getElementById('pw').value})
                    });
                    const data=await res.json();
                    if(data.success){
                        msg.className='msg ok';
                        msg.textContent='Welcome! You have joined '+data.displayName+'. You can now log in from the OrgNet desktop app.';
                        document.getElementById('f').style.display='none';
                    }else{
                        msg.className='msg err'; msg.textContent=data.error||'Failed to accept invite';
                        btn.disabled=false;
                    }
                }catch(ex){
                    msg.className='msg err'; msg.textContent='Network error: '+ex.message;
                    btn.disabled=false;
                }
            }
            </script>
            </body></html>
            """;
        }

        return new ContentResult { Content = html, ContentType = "text/html", StatusCode = 200 };
    }

    /// <summary>
    /// Accept an invitation — creates the user account in the tenant.
    /// Public endpoint: the invited user provides the token, their display name, and a password.
    /// Returns an AuthResponse so they are immediately logged in.
    /// </summary>
    [HttpPost("accept-invite")]
    public async Task<ActionResult<AuthResponse>> AcceptInvite([FromBody] AcceptInviteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Invite token required"));

        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 150)
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Display name required (max 150 chars)"));

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8 || request.Password.Length > 128)
            return BadRequest(new AuthResponse(false, "", "", "", "", Guid.Empty, "Password must be 8-128 characters"));

        var result = await _authService.AcceptInvitationAsync(request);

        if (result.Success)
        {
            await _auditService.LogAsync(null, request.DisplayName, AuditAction.Login, "User",
                details: "Accepted invitation and joined organisation",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
