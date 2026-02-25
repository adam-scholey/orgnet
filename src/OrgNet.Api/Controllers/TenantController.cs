using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TenantController(OrgNetDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<TenantInfoDto>> GetTenant()
    {
        var tenant = await _db.Tenants.FindAsync(_tenantContext.TenantId);
        if (tenant == null) return NotFound();

        var memberCount = await _db.Users.CountAsync();
        return Ok(new TenantInfoDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.ToString(), tenant.Sector.ToString(), memberCount, tenant.CreatedAt));
    }

    [HttpPut]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<TenantInfoDto>> UpdateTenant([FromBody] UpdateTenantRequest request)
    {
        var tenant = await _db.Tenants.FindAsync(_tenantContext.TenantId);
        if (tenant == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
            tenant.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Plan) && Enum.TryParse<OrgNet.Shared.Enums.TenantPlan>(request.Plan, out var plan))
            tenant.Plan = plan;

        if (!string.IsNullOrWhiteSpace(request.Sector) && Enum.TryParse<OrgNet.Shared.Enums.OrganisationSector>(request.Sector, out var sector))
            tenant.Sector = sector;

        await _db.SaveChangesAsync();

        var memberCount = await _db.Users.CountAsync();
        return Ok(new TenantInfoDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.ToString(), tenant.Sector.ToString(), memberCount, tenant.CreatedAt));
    }

    [HttpGet("members")]
    public async Task<ActionResult<List<UserProfileDto>>> GetMembers()
    {
        var members = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserProfileDto(u.Id, u.DisplayName, u.Email, u.Role.ToString(), u.AvatarUrl, u.CreatedAt))
            .ToListAsync();
        return Ok(members);
    }

    [HttpPost("invite")]
    [Authorize(Roles = "Owner,Admin")]
    [EnableRateLimiting("InviteCreation")]
    public async Task<ActionResult<InviteResponse>> InviteUser([FromBody] InviteUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new InviteResponse(false, "", "", DateTime.MinValue, "Valid email required"));

        // Check if user already exists in this tenant
        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
            return Conflict(new InviteResponse(false, "", "", DateTime.MinValue, "User already exists in this organisation"));

        // Check for existing pending invite
        var pending = await _db.Invitations.FirstOrDefaultAsync(i => i.Email == request.Email && i.AcceptedAt == null);
        if (pending != null && !pending.IsExpired)
            return Conflict(new InviteResponse(false, "", "", DateTime.MinValue, "A pending invitation already exists for this email"));

        // Parse role
        if (!Enum.TryParse<OrgNet.Shared.Enums.UserRole>(request.Role, true, out var role))
            role = OrgNet.Shared.Enums.UserRole.Member;

        var userIdClaim = User.FindFirst(OrgNet.Shared.Constants.OrgNetConstants.ClaimTypes.UserId)?.Value;
        var invitedByUserId = Guid.TryParse(userIdClaim, out var uid) ? uid : Guid.Empty;

        // Generate secure invite token
        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var invitation = new OrgNet.Domain.Entities.Invitation
        {
            TenantId = _tenantContext.TenantId,
            Email = request.Email,
            Role = role,
            Token = token,
            InvitedByUserId = invitedByUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        // Build invite link — in production this would be emailed
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var inviteLink = $"{baseUrl}/api/auth/accept-invite?token={token}";

        return Ok(new InviteResponse(true, inviteLink, token, invitation.ExpiresAt));
    }

    [HttpGet("invitations")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<List<InvitationInfoDto>>> GetInvitations()
    {
        var tenant = await _db.Tenants.FindAsync(_tenantContext.TenantId);
        var invitations = await _db.Invitations
            .Include(i => i.InvitedBy)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationInfoDto(
                i.Id, i.Email, i.Role.ToString(),
                tenant!.Name,
                i.InvitedBy.DisplayName,
                i.ExpiresAt, i.AcceptedAt.HasValue,
                DateTime.UtcNow > i.ExpiresAt))
            .ToListAsync();

        return Ok(invitations);
    }

    [HttpDelete("invitations/{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> RevokeInvitation(Guid id)
    {
        var invite = await _db.Invitations.FindAsync(id);
        if (invite == null) return NotFound();
        if (invite.AcceptedAt.HasValue)
            return BadRequest(new { error = "Cannot revoke an accepted invitation" });

        _db.Invitations.Remove(invite);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Invitation revoked" });
    }
}
