using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.DTOs;

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
}
