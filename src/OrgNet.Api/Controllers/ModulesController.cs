using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Infrastructure.Plugins;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModulesController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly PluginLoader _pluginLoader;

    public ModulesController(OrgNetDbContext db, ITenantContext tenantContext, PluginLoader pluginLoader)
    {
        _db = db;
        _tenantContext = tenantContext;
        _pluginLoader = pluginLoader;
    }

    /// <summary>List all available modules with their activation status for this tenant</summary>
    [HttpGet]
    public async Task<ActionResult<List<ModuleInfoDto>>> GetModules()
    {
        var tenantModules = await _db.TenantModules.ToListAsync();

        var modules = _pluginLoader.LoadedPlugins.Select(p =>
        {
            var tm = tenantModules.FirstOrDefault(m => m.ModuleId == p.ModuleId);
            return new ModuleInfoDto(
                p.ModuleId, p.Name, p.Description, p.Version,
                tm?.Status.ToString() ?? ModuleStatus.Disabled.ToString(),
                p.IconUrl);
        }).ToList();

        return Ok(modules);
    }

    /// <summary>Enable or disable a module for this tenant</summary>
    [HttpPost("toggle")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> ToggleModule([FromBody] ToggleModuleRequest request)
    {
        var plugin = _pluginLoader.LoadedPlugins.FirstOrDefault(p => p.ModuleId == request.ModuleId);
        if (plugin == null)
            return NotFound(new { error = $"Module '{request.ModuleId}' not found" });

        var existing = await _db.TenantModules.FirstOrDefaultAsync(m => m.ModuleId == request.ModuleId);

        if (existing != null)
        {
            existing.Status = request.Enabled ? ModuleStatus.Enabled : ModuleStatus.Disabled;
            existing.DeactivatedAt = request.Enabled ? null : DateTime.UtcNow;
        }
        else
        {
            _db.TenantModules.Add(new TenantModule
            {
                TenantId = _tenantContext.TenantId,
                ModuleId = request.ModuleId,
                Status = request.Enabled ? ModuleStatus.Enabled : ModuleStatus.Disabled
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = $"Module '{request.ModuleId}' {(request.Enabled ? "enabled" : "disabled")}" });
    }

    // ── Per-User Module Access ──

    /// <summary>List all per-user module access grants for a specific user</summary>
    [HttpGet("user-access/{userId}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<List<UserModuleAccessDto>>> GetUserModuleAccess(Guid userId)
    {
        var grants = await _db.UserModuleAccess
            .Where(a => a.UserId == userId)
            .Include(a => a.User)
            .Include(a => a.GrantedBy)
            .Select(a => new UserModuleAccessDto(a.Id, a.UserId, a.User.DisplayName, a.ModuleId, a.IsGranted, a.GrantedBy.DisplayName, a.GrantedAt))
            .ToListAsync();

        return Ok(grants);
    }

    /// <summary>Grant or revoke a specific module for a specific user</summary>
    [HttpPost("user-access")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<UserModuleAccessDto>> SetUserModuleAccess([FromBody] SetUserModuleAccessRequest request)
    {
        var adminId = Guid.Parse(User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId)!);

        var existing = await _db.UserModuleAccess
            .FirstOrDefaultAsync(a => a.UserId == request.UserId && a.ModuleId == request.ModuleId);

        if (existing != null)
        {
            existing.IsGranted = request.IsGranted;
            existing.GrantedByUserId = adminId;
            existing.GrantedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new UserModuleAccess
            {
                TenantId = _tenantContext.TenantId,
                UserId = request.UserId,
                ModuleId = request.ModuleId,
                IsGranted = request.IsGranted,
                GrantedByUserId = adminId,
            };
            _db.UserModuleAccess.Add(existing);
        }

        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(request.UserId);
        var admin = await _db.Users.FindAsync(adminId);
        return Ok(new UserModuleAccessDto(
            existing.Id, existing.UserId, user?.DisplayName ?? "",
            existing.ModuleId, existing.IsGranted, admin?.DisplayName ?? "", existing.GrantedAt));
    }

    /// <summary>Remove a per-user module access override (reverts to tenant-level setting)</summary>
    [HttpDelete("user-access/{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> RemoveUserModuleAccess(Guid id)
    {
        var grant = await _db.UserModuleAccess.FindAsync(id);
        if (grant == null) return NotFound();

        _db.UserModuleAccess.Remove(grant);
        await _db.SaveChangesAsync();
        return Ok(new { message = "User module access override removed" });
    }
}
