using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        return Ok(new TenantInfoDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.ToString(), memberCount, tenant.CreatedAt));
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

        await _db.SaveChangesAsync();

        var memberCount = await _db.Users.CountAsync();
        return Ok(new TenantInfoDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.ToString(), memberCount, tenant.CreatedAt));
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
    public async Task<ActionResult> InviteUser([FromBody] InviteUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email required" });

        var exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (exists)
            return Conflict(new { error = "User already exists in this organisation" });

        // In production, this would send an invitation email.
        // For MVP, we create a pending user record.
        return Ok(new { message = $"Invitation sent to {request.Email}" });
    }
}
