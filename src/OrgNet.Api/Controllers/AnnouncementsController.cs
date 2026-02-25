using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrgNet.Api.Hubs;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementsController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHubContext<OrgNetHub> _hub;

    public AnnouncementsController(OrgNetDbContext db, ITenantContext tenantContext, IHubContext<OrgNetHub> hub)
    {
        _db = db;
        _tenantContext = tenantContext;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnnouncementDto>>> GetAnnouncements()
    {
        var announcements = await _db.Announcements
            .Include(a => a.Author)
            .Where(a => a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishedAt)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.IsPinned, a.Author.DisplayName, a.PublishedAt, a.ExpiresAt))
            .ToListAsync();

        return Ok(announcements);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<AnnouncementDto>>> SearchAnnouncements([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<AnnouncementDto>());

        var query = q.ToLower();
        var results = await _db.Announcements
            .Include(a => a.Author)
            .Where(a => a.Title.ToLower().Contains(query) || a.Body.ToLower().Contains(query))
            .OrderByDescending(a => a.PublishedAt)
            .Take(50)
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Body, a.IsPinned, a.Author.DisplayName, a.PublishedAt, a.ExpiresAt))
            .ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<AnnouncementDto>> Create([FromBody] CreateAnnouncementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Body is required" });

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var announcement = new Announcement
        {
            TenantId = _tenantContext.TenantId,
            AuthorId = userId,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            IsPinned = request.IsPinned,
            ExpiresAt = request.ExpiresAt
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync();

        var dto = new AnnouncementDto(announcement.Id, announcement.Title, announcement.Body, announcement.IsPinned,
            user.DisplayName, announcement.PublishedAt, announcement.ExpiresAt);

        // Broadcast to all tenant members via SignalR
        await _hub.Clients
            .Group(OrgNetConstants.SignalRGroups.TenantGroup(_tenantContext.TenantId))
            .SendAsync("ReceiveAnnouncement", dto);

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var announcement = await _db.Announcements.FindAsync(id);
        if (announcement == null) return NotFound();

        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Announcement deleted" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
