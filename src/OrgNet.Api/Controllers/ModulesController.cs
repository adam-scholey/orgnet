using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Infrastructure.Plugins;
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
}
