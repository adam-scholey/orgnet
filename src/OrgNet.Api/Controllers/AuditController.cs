using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin")]
public class AuditController : ControllerBase
{
    private readonly OrgNetDbContext _db;

    public AuditController(OrgNetDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<AuditPageResult>> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await _db.AuditLogs.CountAsync();
        var items = await _db.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(a.Id, a.Username, a.Action.ToString(), a.EntityType, a.EntityId, a.Details, a.IpAddress, a.Timestamp))
            .ToListAsync();

        return Ok(new AuditPageResult(items, total, page, pageSize));
    }

    /// <summary>Admin dashboard summary — activity stats for the last 24 hours</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<AdminDashboardSummary>> GetDashboardSummary()
    {
        var since = DateTime.UtcNow.AddHours(-24);

        var totalUsers = await _db.Users.CountAsync();
        var recentLogins = await _db.AuditLogs.CountAsync(a => a.Action == AuditAction.Login && a.Timestamp >= since);
        var recentActions = await _db.AuditLogs.CountAsync(a => a.Timestamp >= since);
        var totalFiles = await _db.OrgFiles.CountAsync();
        var totalMessages = await _db.ChatMessages.CountAsync(m => m.SentAt >= since);
        var totalTasks = await _db.TaskItems.CountAsync();
        var activeTasks = await _db.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Done);
        var totalAppointments = await _db.Appointments.CountAsync(a => !a.IsCancelled && a.StartsAt >= DateTime.UtcNow);
        var activeDevices = await _db.UserDevices.CountAsync(d => d.TrustLevel == DeviceTrustLevel.Trusted);
        var pendingDevices = await _db.UserDevices.CountAsync(d => d.TrustLevel == DeviceTrustLevel.Pending);

        // Most active users in last 24h
        var topUsers = await _db.AuditLogs
            .Where(a => a.Timestamp >= since && a.Username != "")
            .GroupBy(a => a.Username)
            .Select(g => new ActivityUserSummary(g.Key, g.Count()))
            .OrderByDescending(u => u.ActionCount)
            .Take(10)
            .ToListAsync();

        return Ok(new AdminDashboardSummary(
            totalUsers, recentLogins, recentActions, totalFiles,
            totalMessages, totalTasks, activeTasks, totalAppointments,
            activeDevices, pendingDevices, topUsers));
    }
}
